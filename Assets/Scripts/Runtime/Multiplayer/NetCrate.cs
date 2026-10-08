// NetCrate.cs
// Multiplayer: an orbit authority's crate (a destroyed ship's container) shown to the other players in that orbit (NetOrbit
// asks NetState to spawn one per crate; the host spawns it owned by that player). For the others it is a real Crate (the
// race's container model, the same loot, `remote`: no drift or expiry of its own) while they are in that orbit, so their
// radar marks it and their tractor beam locks and pulls it; it follows the owner's crate until their own beam pulls it.
// One player gets it: the host keeps the claim (the first player whose beam starts pulling; Crate.PullStarted). The others'
// crates are claimedByOther (their radar leaves it, a beam already on it lets go), and the claimant's capture waits until
// the claim is confirmed (captureBlocked). Another player's capture destroys the owner's crate (and so everyone's); the
// owner's own capture or the crate's expiry despawns it (NetOrbit). A claimant who leaves loses the claim. Only a player
// flying in the crate's orbit may claim it (NetGuard), the claims are rate limited (NetRateLimit), the copies take only
// items that exist from the owner's loot, and only the server may tell the owner to remove it.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    public sealed class NetCrate : NetworkBehaviour
    {
        const ulong NoClaim = ulong.MaxValue;

        static readonly NetworkVariableReadPermission Read = NetworkVariableReadPermission.Everyone;
        static readonly NetworkVariableWritePermission Owner = NetworkVariableWritePermission.Owner;

        readonly NetworkVariable<int> station = new NetworkVariable<int>(-1);   // server-written at spawn
        readonly NetworkVariable<int> localId = new NetworkVariable<int>(-1);
        readonly NetworkVariable<ulong> claimant = new NetworkVariable<ulong>(NoClaim);   // server-written
        readonly NetworkVariable<Vector3> position = new NetworkVariable<Vector3>(default, Read, Owner);
        readonly NetworkVariable<Quaternion> rotation = new NetworkVariable<Quaternion>(Quaternion.identity, Read, Owner);
        readonly NetworkVariable<int> race = new NetworkVariable<int>(-1, Read, Owner);
        readonly NetworkVariable<int> look = new NetworkVariable<int>(-1, Read, Owner);   // Crate.look; -1 = not written yet
        readonly NetworkVariable<FixedString512Bytes> loot = new NetworkVariable<FixedString512Bytes>(default, Read, Owner);   // "item:amount,..."
        readonly NetworkVariable<bool> fromFriend = new NetworkVariable<bool>(false, Read, Owner);
        readonly NetworkVariable<bool> missionCrate = new NetworkVariable<bool>(false, Read, Owner);
        readonly NetworkVariable<bool> missionLoot = new NetworkVariable<bool>(false, Read, Owner);   // only the owner's squad takes it

        readonly NetSmoothing smoothing = new NetSmoothing();
        Crate crate;          // the owner: the real crate; the others: the copy (while in the orbit)
        bool reported, wasPulling, adopted;

        /// <summary>A Recovery / Salvage container (a squadmate's takeover of the mission builds its own, FreelanceOrbit).</summary>
        public bool IsMissionCrate => missionCrate.Value;
        /// <summary>Its pose (Unity) as the owner last wrote it.</summary>
        public Vector3 WorldPosition => position.Value;

        /// <summary>Taken over by this player (their own crate now stands in for it): no copy here any more.</summary>
        public void MarkAdopted()
        {
            adopted = true;
            if (crate != null && !IsOwner) Destroy(crate.gameObject);
            crate = null;
        }

        /// <summary>NetState: a squadmate took the mission over and built its own container: the owner's goes.</summary>
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void TakenOverRpc()
        {
            if (crate != null) Destroy(crate.gameObject);
        }
        int pendingStation = -1, pendingId = -1;

        public int Station => station.Value;
        /// <summary>Host: when it was spawned (NetState's sweep waits for its owner's position to arrive).</summary>
        public float SpawnedAt { get; private set; }

        /// <summary>Host, before spawning: the orbit and the owner's crate id (written in OnNetworkSpawn).</summary>
        public void Init(int stationIndex, int id)
        {
            pendingStation = stationIndex;
            pendingId = id;
        }

        public override void OnNetworkSpawn()
        {
            DontDestroyOnLoad(gameObject);
            if (IsServer)
            {
                station.Value = pendingStation;
                localId.Value = pendingId;
                SpawnedAt = Time.unscaledTime;
            }
            if (!IsOwner) return;
            crate = NetOrbit.Current != null && NetOrbit.Current.Station == station.Value ? NetOrbit.Current.Crate(localId.Value) : null;
            if (crate == null)
            {
                // Captured or gone before this spawned (NetOrbit had dropped its id already): nothing to show.
                var state = NetState.Instance;
                if (state != null && state.IsSpawned) state.DespawnRpc(NetworkObjectId);
                return;
            }
            NetOrbit.Current.Register(this, crate);
            position.Value = crate.transform.position;
            rotation.Value = crate.transform.rotation;
            race.Value = crate.race;
            look.Value = crate.look;
            loot.Value = Encode(crate.loot);
            fromFriend.Value = crate.fromFriend;
            missionCrate.Value = crate.missionCrate;
            missionLoot.Value = crate.missionLoot;
            crate.PullStarted = () => ClaimRpc();
            if (crate.pulled) ClaimRpc();   // the owner's beam (an auto tractor) had it before the spawn
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner && crate != null) Destroy(crate.gameObject);
        }

        /// <summary>A beam started pulling it: the claim, if nobody has it yet.</summary>
        [Rpc(SendTo.Server)]
        void ClaimRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (!NetRateLimit.Allow(who, NetRateLimit.Kind.Claim)) return;
            // Only a player flying in the crate's orbit (a claim from elsewhere would hold every crate of the session back).
            if (!NetGuard.InOrbit(who, station.Value)) return;
            // Another player's mission loot: only the mission's team (the owner's squad) may claim it.
            if (missionLoot.Value && !NetSquad.SameClient(who, NetSquad.Find(OwnerClientId))) return;
            if (claimant.Value == NoClaim) claimant.Value = who;
        }

        /// <summary>The claimant's beam let go (or they left the orbit): anyone may take it again.</summary>
        [Rpc(SendTo.Server)]
        void ReleaseRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (NetRateLimit.Allow(who, NetRateLimit.Kind.Claim) && claimant.Value == who) claimant.Value = NoClaim;
        }

        /// <summary>The claimant's capture: the owner's crate is gone (and with it everyone's).</summary>
        [Rpc(SendTo.Server)]
        void CapturedRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (NetRateLimit.Allow(who, NetRateLimit.Kind.Claim) && claimant.Value == who) RemoveRpc();
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void RemoveRpc()
        {
            if (crate != null) Destroy(crate.gameObject);
        }

        /// <summary>A copy for this player (in the crate's orbit): built once the owner's values are here.</summary>
        void BuildCopy()
        {
            if (crate != null || look.Value < 0) return;   // a shot asteroid's crate has race -1
            var assets = CombatAssets.Load();
            var prefab = assets != null ? assets.CrateModel(look.Value, race.Value) : null;
            var go = prefab != null ? Instantiate(prefab, position.Value, rotation.Value) : new GameObject();
            go.name = "Crate (other player's)";
            crate = go.AddComponent<Crate>();
            crate.remote = true;
            crate.race = race.Value;
            crate.look = look.Value;
            crate.fromFriend = fromFriend.Value;
            crate.missionCrate = missionCrate.Value;
            crate.missionLoot = missionLoot.Value;
            foreach (var s in Decode(loot.Value.ToString())) crate.loot.Add(s);
            crate.PullStarted = () => ClaimRpc();
            crate.CapturedHere = () =>
            {
                // Captured here (CombatRadar): the owner removes its crate, once.
                if (reported || claimant.Value != NetworkManager.LocalClientId) return;
                reported = true;
                CapturedRpc();
            };
            smoothing.Push(position.Value);
            smoothing.Snap();
            ApplyClaim(NetworkManager.LocalClientId);   // the claim rules from the first frame (an auto tractor grabs at once)
        }

        void Update()
        {
            if (!IsSpawned) return;
            ulong me = NetworkManager.LocalClientId;
            // A claimant who left (the session or the crate's orbit: docked, jumped, dead) loses the claim.
            if (IsServer && claimant.Value != NoClaim && !NetOrbit.InOrbit(claimant.Value, station.Value)) claimant.Value = NoClaim;
            if (IsOwner)
            {
                if (crate == null) return;   // NetOrbit asks for the despawn
                ApplyClaim(me);
                TrackPull(me);
                if ((position.Value - crate.transform.position).sqrMagnitude > 0.0001f) position.Value = crate.transform.position;
                if (Quaternion.Angle(rotation.Value, crate.transform.rotation) > 0.5f) rotation.Value = crate.transform.rotation;
                return;
            }
            var local = NetPlayer.Local;
            bool here = local != null && local.InSpace && local.Station == station.Value;
            if (!here || adopted)
            {
                // Not in its orbit (any more): no copy (built again on coming back while it still exists), no claim.
                if (crate != null) Destroy(crate.gameObject);
                crate = null;
                if (wasPulling || claimant.Value == me) { wasPulling = false; if (claimant.Value == me) ReleaseRpc(); }
                return;
            }
            if (crate == null)
            {
                // The copy is gone (a capture reported itself, CapturedHere) or never built.
                if (!reported) BuildCopy();
                return;
            }
            ApplyClaim(me);
            TrackPull(me);
            if (!crate.pulled)
            {
                if (position.Value != lastPosition) { smoothing.Push(position.Value); lastPosition = position.Value; }
                smoothing.Apply(crate.transform, rotation.Value);
            }
        }

        Vector3 lastPosition;

        void ApplyClaim(ulong me)
        {
            // Another player's mission loot is out of reach: only the owner's squad (the mission's team) takes it.
            bool outsider = crate.missionLoot && !NetSquad.SameClient(OwnerClientId, NetPlayer.Local);
            bool otherClaims = claimant.Value != NoClaim && claimant.Value != me;
            crate.claimedByOther = outsider || otherClaims;
            // The owner's own crate: only another player's claim holds it back (its capture needs no confirmation).
            crate.captureBlocked = outsider || (IsOwner ? otherClaims : claimant.Value != me);
        }

        /// <summary>This player's beam let go of it without capturing it: the claim goes back.</summary>
        void TrackPull(ulong me)
        {
            bool pulling = crate.pulled;
            if (wasPulling && !pulling && claimant.Value == me) ReleaseRpc();
            wasPulling = pulling;
        }

        static string Encode(List<ItemStack> stacks)
        {
            var sb = new StringBuilder();
            foreach (var s in stacks)
            {
                if (s.amount <= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(s.item).Append(':').Append(s.amount);
                if (sb.Length > 480) break;
            }
            return sb.ToString();
        }

        /// <summary>The owner's loot (its own game's word): only items that exist, at most MaxLootAmount each (a modified owner
        /// can't hand out unknown item ids that break the taker's cargo code).</summary>
        static IEnumerable<ItemStack> Decode(string text)
        {
            foreach (var part in text.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int item) && int.TryParse(kv[1], out int amount) && amount > 0 && NetGuard.Item(item))
                    yield return new ItemStack(item, Mathf.Min(amount, MaxLootAmount));
            }
        }

        const int MaxLootAmount = 1000;
    }
}
