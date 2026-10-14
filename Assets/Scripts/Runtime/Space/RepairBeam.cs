// RepairBeam.cs
// The automatic repair and transfusion beams (Reference/research/combat_equipment.md 3.4): RepairBeam(item, sort)
// 0xb3fb8 / update 0xb41c4 / render 0xb49dc, one per mounted sort (both can coexist). No button, no HUD element.
//   repair (sort 37: 207 Nirai SPP-C1, 208 SPP-M50)       heals damaged friendly NPCs (hull only): dt * 0.03 * attr54 / 100
//   transfusion (sort 41: 222 Crimson Drain, 223 Pandora Leech)  drains hostile ships dt * 0.01 * attr54 / 100 (whole points
//                     through Player::damage, so kill credit like a gun) and gives the player's shield the same (only
//                     while it isn't full and a shield is mounted)
// Every 2500 ms the attr 55 slots are refilled from the ships within attr 53 units, keeping the most damaged ones (the
// transfusion skips cloaked ones). Beam meshes 19092 / 19093 from the player to each target, scaled to the distance;
// loop sounds 2271 / 2272 / 2267 / 2268 while any slot is filled.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Visuals;
using GoF2Remake.World;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class RepairBeam : MonoBehaviour
    {
        const float M = 0.05f, RetargetMs = 2500f;

        bool transfusion;
        float rangeUnits, rate;
        NpcShip[] slots;
        Transform[] beams;
        float[] acc;
        float retargetMs;
        Target player;
        Hitpoints playerHp;
        Traffic traffic;
        AudioSource loop;

        /// <summary>PlayerEgo::setShip: a beam object per mounted sort 37 / 41.</summary>
        public static void AttachAll(GameObject player, Database db, Target target, Traffic traffic)
        {
            foreach (int sort in new[] { 37, 41 })
            {
                var item = Shop.FirstMounted(db, sort);
                if (item == null) continue;
                var b = player.AddComponent<RepairBeam>();
                b.Setup(item, sort == 41, target, traffic);
            }
        }

        void Setup(ItemData item, bool drain, Target target, Traffic level)
        {
            transfusion = drain;
            rangeUnits = item.Attr(53, 15000);
            rate = item.Attr(54, 60) / 100f;
            int n = Mathf.Max(1, item.Attr(55, 1));
            slots = new NpcShip[n];
            beams = new Transform[n];
            acc = new float[n];
            player = target;
            playerHp = target.hitpoints;
            traffic = level;
            var assets = CombatAssets.Load();
            var prefab = assets == null ? null : drain ? assets.transfusionBeam : assets.repairBeam;
            if (prefab != null)
                for (int i = 0; i < n; i++)
                {
                    var go = Instantiate(prefab);
                    go.name = drain ? "Transfusion beam" : "Repair beam";
                    GunRig.StripForFx(go);
                    go.SetActive(false);
                    beams[i] = go.transform;
                }
            AudioClip clip = null;
            if (assets != null)
                clip = item.index switch { 207 => assets.repairLoop1, 208 => assets.repairLoop2, 222 => assets.drainLoop1, _ => assets.drainLoop2 };
            if (clip != null)
            {
                loop = gameObject.AddComponent<AudioSource>();
                loop.clip = GoF2Remake.Modding.ModSounds.Get(clip);
                loop.loop = true;
                loop.playOnAwake = false;
                loop.spatialBlend = 0f;
            }
        }

        void OnDestroy()
        {
            if (beams != null) foreach (var b in beams) if (b != null) Destroy(b.gameObject);
        }

        bool Candidate(NpcShip s)
        {
            if (s == null || s.Gone || s.Current != NpcShip.State.Fly || s.Asleep || !s.Target.Alive) return false;
            if ((s.transform.position - transform.position).magnitude / M >= rangeUnits) return false;
            if (transfusion)
                return s.Target.hostileToPlayer && !s.Target.untargetable && playerHp != null && playerHp.maxShield > 0
                       && playerHp.shield < playerHp.maxShield;
            return s.Target.friendToPlayer && s.Hp.hull < s.Hp.maxHull;
        }

        /// <summary>The slot rule: a free slot, else replace the healthiest ship that is healthier than this one.</summary>
        void Retarget()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = null;
            if (traffic == null || !player.Alive) return;
            foreach (var s in traffic.Ships)
            {
                if (!Candidate(s)) continue;
                int free = System.Array.IndexOf(slots, null);
                if (free >= 0) { slots[free] = s; continue; }
                int worst = -1;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].Hp.hull > s.Hp.hull && (worst < 0 || slots[i].Hp.hull > slots[worst].Hp.hull)) worst = i;
                if (worst >= 0) slots[worst] = s;
            }
        }

        void Update()
        {
            float dtMs = Time.deltaTime * 1000f * TimeExtender.PlayerFactor;
            if (dtMs <= 0f) return;
            retargetMs -= dtMs;
            if (retargetMs < 0f) { retargetMs = RetargetMs; Retarget(); }
            bool any = false;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s != null && (!player.Alive || s.Gone || !s.Target.Alive || s.Current != NpcShip.State.Fly)) slots[i] = s = null;
                var beam = beams[i];
                if (s == null) { if (beam != null && beam.gameObject.activeSelf) beam.gameObject.SetActive(false); continue; }
                any = true;
                float amount = dtMs * (transfusion ? 0.01f : 0.03f) * rate;
                if (transfusion)
                {
                    acc[i] += amount;
                    while (acc[i] >= 1f && s.Target.Alive) { acc[i] -= 1f; s.Target.Damage(1f, false); }
                    if (playerHp != null && playerHp.shield < playerHp.maxShield)
                        playerHp.shield = Mathf.Min(playerHp.maxShield, playerHp.shield + amount);
                }
                else
                {
                    acc[i] += amount;   // Player::heal: a fractional accumulator, hull only
                    int whole = (int)acc[i];
                    if (whole > 0) { acc[i] -= whole; s.Hp.hull = Mathf.Min(s.Hp.maxHull, s.Hp.hull + whole); s.Target.hp = s.Hp.hull; }
                }
                if (beam == null) continue;
                if (!beam.gameObject.activeSelf) beam.gameObject.SetActive(true);
                var d = s.transform.position - transform.position;
                if (d.sqrMagnitude < 1e-6f) continue;
                beam.SetPositionAndRotation(transform.position, Quaternion.LookRotation(d, transform.up));
                beam.localScale = new Vector3(1f, 1f, d.magnitude / M);
            }
            if (loop == null) return;
            if (any && !loop.isPlaying) { loop.volume = 0.7f * Settings.SfxVolume; loop.Play(); }
            else if (!any && loop.isPlaying) loop.Stop();
        }
    }
}
