// CapitalShip.cs
// Remake (option "Capital ship enhancements", Settings.CapitalShips): one capital ship of the orbit's traffic (the Terran
// battleship, the carrier, the Vossk battleship) built with the option on (SpawnSpec.capitalEnhanced). Added by Traffic next
// to its NpcShip. The rules are CapitalShips' (see there):
//   provoked    the player's first hit on a friendly or neutral one says "Hold your fire!" (Traffic.FriendTurnedEnemy); 3
//               hits or 0.5 % of its hull turn the race hostile (Traffic.AlarmAllFriends: its turrets, escorts and the local
//               fighters, the station remembers); a hostile one is provoked by the first hit
//   Inflicts    the carrier, attacked by NPCs or by a provoking player in the last 20 s, launches 5 Inflicts from its deck
//               pads (the approach points of docking set 5) every 20 s, 15 in all, hostile to the player once provoked (and
//               to the other players who shot it, its multiplayer aggressors)
//   death       the carrier / Vossk battleship: explosions along its hull through its 8 s of dying (NpcShip, deathMs) before
//               the big blast; its turrets go with it; the player's kill is a delict of 20 with the race, the Vossk one counts
//               as a battleship destroyed (medal 39), and its crate starts the hour before the next one has loot
//   fleet battle (SpawnSpec.fleetBattle) it closes in on the enemy capital ship (battleFoe) at 0.2 u/ms to 50 000 units, its
//               turrets parented to it so they ride along; the system race's one calls the battle 6 s in; at a death the
//               winners call it, and a player who helped (the kill or 5 % of the hull) is paid while the winners aren't hostile; no docking at the carrier meanwhile
//   missiles    every 12-15 s a salvo of 4 homing missiles, each race its own (Terran: Intelli Jet 37, fast; Vossk: S'koonn 38,
//               the Vossk-made one (attr 60), slower and harder; the item's speed, damage relative to 100, 10 s of flight)
//               launched upward from its turret mounts with the player's smoke trails: in a fleet battle at the enemy capital
//               ship, else at the nearest hostile ship within 40 000 units of its hull (the player only when hostile to it);
//               the NPC gun damage x10 on a big ship's hull, x3 on a fighter (x0.2 a stray hit on a player it isn't after);
//               a boost shakes them off: the salvo at the player loses its lock (the missiles fly straight on) and stops

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using UnityEngine;

namespace GoF2Remake.World
{
    public class CapitalShip : MonoBehaviour
    {
        const float M = 0.05f;

        NpcShip ship;
        Traffic traffic;
        CombatAssets assets;
        int playerHits, playerDamage, playerDamageTotal;
        bool warned, provoked, looted;
        float sinceAttackMs = float.MaxValue, waveMs;
        int launched;
        Database db;
        float blastMs;
        readonly List<Vector3> pads = new List<Vector3>();   // ship-local Unity
        NpcShip foe;
        bool partsRide, battleCalled;
        float battleMs;

        /// <summary>The ships it launched (the carrier's Inflicts).</summary>
        public int Launched => launched;
        public bool Provoked => provoked;
        /// <summary>A fleet battle's capital ship whose enemy capital ship still stands (no docking meanwhile).</summary>
        public bool InBattle => ship != null && ship.Spec.fleetBattle
                                && (foe == null ? traffic.FleetBattleRaging : foe.Target.Alive && !foe.Gone && foe.Current == NpcShip.State.Fly);

        public static CapitalShip Attach(NpcShip host, Traffic owner, Database db)
        {
            var c = host.gameObject.AddComponent<CapitalShip>();
            c.ship = host;
            c.traffic = owner;
            c.db = db;
            c.assets = CombatAssets.Load();
            host.Target.Damaged += c.OnDamaged;
            host.Target.Died += c.OnDied;
            if (CapitalShips.Killable(host.Spec))
            {
                var loot = CapitalShips.RollLoot(db);
                c.looted = loot.Count > 0;
                host.SetLoot(loot);
            }
            if (host.Spec.capital == SpawnSpec.CapitalCarrier)
                foreach (var p in SpacePoints.Set(CapitalShips.DeckPoints))
                    if (p.type == SpacePoints.Approach) c.pads.Add(SpacePoints.ToLocal(p.engine));
            return c;
        }

        void OnDestroy()
        {
            if (ship == null || ship.Target == null) return;
            ship.Target.Damaged -= OnDamaged;
            ship.Target.Died -= OnDied;
        }

        void OnDamaged(Target t, int dmg, bool byNpc)
        {
            if (byNpc) { sinceAttackMs = 0f; return; }   // an NPC (or, in multiplayer, another player: NpcShip.OnRemoteHit)
            playerDamageTotal += dmg;
            if (provoked) { sinceAttackMs = 0f; return; }
            if (ship.Target.hostileToPlayer) { Provoke(); return; }
            playerHits++;
            playerDamage += dmg;
            if (!warned) { warned = true; traffic.FriendTurnedEnemy(ship.Race); }   // "Hold your fire!"
            if (playerHits >= 3 || playerDamage >= Mathf.Max(50, ship.Hp.maxHull / 200)) Provoke();
        }

        void Provoke()
        {
            provoked = true;
            sinceAttackMs = 0f;
            ship.turnedEnemy = true;
            traffic.AlarmAllFriends(ship.Race, true);
        }

        void Update()
        {
            if (ship == null) return;
            float dtMs = Time.deltaTime * 1000f;
            if (dtMs <= 0f) return;
            if (ship.Current == NpcShip.State.Dying) { UpdateDeath(dtMs); return; }
            if (ship.Gone || !ship.Target.Alive || ship.Current != NpcShip.State.Fly) return;
            if (sinceAttackMs < float.MaxValue) sinceAttackMs += dtMs;
            if (ship.Spec.fleetBattle) UpdateBattle(dtMs);
            UpdateMissiles(dtMs);
            if (ship.Spec.capital != SpawnSpec.CapitalCarrier || launched >= CapitalShips.CarrierInflicts || pads.Count == 0) return;
            if (sinceAttackMs >= CapitalShips.UnderAttackMs) return;
            waveMs -= dtMs;
            if (waveMs > 0f) return;
            waveMs = CapitalShips.WaveMs;
            Launch();
        }

        /// <summary>One wave of Inflicts off the deck pads, climbing out along the carrier's heading.</summary>
        void Launch()
        {
            int n = Mathf.Min(CapitalShips.InflictsPerWave, CapitalShips.CarrierInflicts - launched);
            bool hostile = ship.alwaysEnemy || ship.turnedEnemy;
            var up = ship.transform.up;
            var dir = (ship.transform.forward + up * 0.35f).normalized;
            for (int i = 0; i < n; i++)
            {
                var at = ship.transform.TransformPoint(pads[(launched + i) % pads.Count]) + up * (i / pads.Count) * 1000f * M;
                var spec = new SpawnSpec
                {
                    group = NpcGroup.Escort, race = ship.Race, ship = CapitalShips.Inflict, position = new Vector3(at.x, at.y, -at.z) / M,
                    alwaysEnemy = hostile, noLoot = true, capitalPart = true, capitalHost = ship.Spec, capitalEnhanced = true,
                };
                var s = traffic.SpawnShip(spec);
                s.Place(at, dir);
                foreach (var a in ship.aggressors) s.aggressors.Add(a);
            }
            launched += n;
            traffic.ConnectPlayers();
            if (hostile)
                traffic.Warn(Localization.Extra("carrierLaunch", "The carrier is launching its Inflicts!"));
        }

        /// <summary>The fleet battle: its turrets ride along, it closes in on the enemy capital ship, the opening call.</summary>
        void UpdateBattle(float dtMs)
        {
            if (!partsRide)
            {
                partsRide = true;
                foreach (var s in traffic.Ships) if (s.Spec.capitalHost == ship.Spec && s.IsTurret) s.transform.SetParent(ship.transform, true);
            }
            foe ??= traffic.Ships.Find(s => s.Spec == ship.Spec.battleFoe);
            battleMs += dtMs;
            if (!battleCalled && battleMs >= CapitalShips.BattleRadioMs && ship.Race == traffic.SystemRace)
            {
                battleCalled = true;
                traffic.RaceRadio(ship.Race == 1
                    ? Localization.Extra("battleCallVossk", "A Terran warship in Vossk space! All ships, destroy it!")
                    : Localization.Extra("battleCallTerran", "All Terran ships: the Vossk battleship is in range. Bring it down!"), ship.Race);
            }
            if (foe == null || !foe.Target.Alive || foe.Current != NpcShip.State.Fly) return;
            var d = foe.transform.position - ship.transform.position;
            float gap = d.magnitude / M - CapitalShips.BattleHoldUnits;
            if (gap <= 0f) return;
            // Both close in, so each covers half the gap.
            ship.transform.position += d.normalized * Mathf.Min(CapitalShips.BattleSpeed * dtMs, gap / 2f) * M;
        }

        // ---- missiles ------------------------------------------------------------------------------------------

        Gun missiles;
        GunRig missileRig;
        WeaponFx missileFx;
        bool missilesTried;
        readonly List<Vector3> launchers = new List<Vector3>();   // ship-local Unity
        Target missileTarget;
        float salvoMs = 4000f, salvoGapMs;
        int salvoLeft, launcherIndex, missileBase;

        void SetupMissiles()
        {
            missilesTried = true;
            var item = db != null ? db.Item(ship.Race == 1 ? CapitalShips.VosskMissile : CapitalShips.TerranMissile) : null;
            if (item == null) return;
            // The race's own missile: its speed (attr 13) and its damage (attr 9) against the Intelli Jet's 100.
            missileBase = Mathf.Max(1, Mathf.RoundToInt(NpcTables.GunDamage(ship.Spec, false, true, out _) * item.Attr(9, 100) / 100f));
            float speed = item.Attr(13, 0) > 0 ? item.Attr(13, 0) : CapitalShips.MissileSpeed;
            missiles = new Gun(item, missileBase, CapitalShips.MissileGapMs, CapitalShips.MissilesPerSalvo, CapitalShips.MissileLifetimeMs, speed)
            {
                owner = ship.Target, homingDelayMs = 1000f,
            };
            missileFx = WeaponFx.Load(item.index);
            var root = new GameObject("Capital ship missiles").transform;
            root.SetParent(traffic.transform, false);
            missileRig = new GunRig(missiles, missileFx, root, null, 2);
            missileRig.EnableTrails();   // RocketGun::setRadar's smoke, the player's (NPC rockets have none in the original)
            missiles.Hit += OnMissileHit;
            ship.ExtraGuns.Add(missiles);   // multiplayer: the other players see them (NetProxy's shot sender)
            // Launch from the turret mounts (a vertical launcher beside each), else from the top of the hull.
            foreach (var s in traffic.Ships)
                if (s.Spec.capitalHost == ship.Spec && s.IsTurret)
                    launchers.Add(ship.transform.InverseTransformPoint(s.transform.position) + Vector3.up * 400f * M);
            if (launchers.Count == 0) launchers.Add(Vector3.up * 3000f * M);
        }

        void OnMissileHit(int b, Target hit, Vector3 point)
        {
            bool big = hit.boxes != null && hit.boxes.Length > 0;
            float dmg = missileBase * (big ? CapitalShips.MissileCapitalFactor : CapitalShips.MissileFactor);
            if (hit.isPlayer && !ship.Target.hostileToPlayer) dmg *= 0.2f;   // a stray hit (NpcShip.GunHit)
            hit.Damage(dmg, true, missiles.bullets[b].velocity);
            missileRig.ShowImpact(point);
        }

        void UpdateMissiles(float dtMs)
        {
            if (!missilesTried) SetupMissiles();
            if (missiles == null) return;
            // A boost breaks the lock: the salvo at the player flies straight on and the rest of it is called off.
            if (missileTarget != null && missileTarget.isPlayer && PlayerBoosting())
            {
                bool inFlight = false;
                for (int i = 0; i < missiles.bullets.Length; i++) if (missiles.IsActive(i)) inFlight = true;
                missileTarget = null;
                salvoLeft = 0;
                if (inFlight) traffic.Warn(Localization.Extra("missilesEvaded", "Missiles evaded!"));
            }
            var lockTarget = missileTarget != null && missileTarget.Alive && !missileTarget.cloaked ? missileTarget : null;
            missiles.Update(dtMs, ship.enemies, lockTarget);
            missileRig.UpdateVisuals(dtMs, Camera.main, ship.transform.up);
            if (salvoLeft > 0)
            {
                salvoGapMs -= dtMs;
                if (salvoGapMs > 0f) return;
                salvoGapMs = CapitalShips.MissileGapMs;
                if (lockTarget == null) { salvoLeft = 0; return; }
                FireMissile(lockTarget);
                salvoLeft--;
                return;
            }
            salvoMs -= dtMs;
            if (salvoMs > 0f) return;
            salvoMs = CapitalShips.SalvoMs + Random.Range(0f, CapitalShips.SalvoJitterMs);
            missileTarget = PickMissileTarget();
            if (missileTarget != null) { salvoLeft = CapitalShips.MissilesPerSalvo; salvoGapMs = 0f; }
        }

        ShipController playerShip;

        bool PlayerBoosting()
        {
            if (playerShip == null && traffic.Player != null) playerShip = traffic.Player.GetComponent<ShipController>();
            return playerShip != null && playerShip.Model != null && playerShip.Model.IsBoosting;
        }

        /// <summary>One missile up out of a launcher, leaning toward the target (it homes after 1000 ms).</summary>
        void FireMissile(Target target)
        {
            var from = ship.transform.TransformPoint(launchers[launcherIndex++ % launchers.Count]);
            var toward = (target.transform.position - from).normalized;
            var dir = (ship.transform.up * 0.85f + toward * 0.5f).normalized;
            if (missiles.TryFire(from, Quaternion.LookRotation(dir, ship.transform.forward), false) < 0) return;
            missileRig.OnShot();
            Sfx.PlayAt(missileFx != null ? missileFx.Shot : null, from, 0.8f);
        }

        /// <summary>The fleet battle's enemy capital ship while it stands, else the nearest hostile ship within reach of its hull
        /// (the player only while hostile to it).</summary>
        Target PickMissileTarget()
        {
            if (foe != null && foe.Target.Alive && foe.Current == NpcShip.State.Fly && !foe.Gone) return foe.Target;
            Target best = null;
            float bestD = CapitalShips.MissileRangeUnits * M;
            foreach (var e in ship.enemies)
            {
                if (e == null || !e.Targetable || e.cloaked) continue;
                bool hostile = e.isPlayer ? ship.Target.hostileToPlayer : e.isShip && e.race >= 0 && Standing.RacesHostile(e.race, ship.Race);
                if (!hostile) continue;
                float d = (ship.Target.NearestPoint(e.transform.position) - e.transform.position).magnitude;
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        void OnDied(Target t)
        {
            blastMs = 0f;
            missileRig?.HideAll();
            missiles?.RemoveAll();
            if (ship.Spec.fleetBattle) BattleOver(t);
            if (CapitalShips.Killable(ship.Spec)) traffic.DestroyCapitalTurrets(ship.Spec);
            if (t.killedByNpc) return;
            if (CapitalShips.Killable(ship.Spec))
            {
                Standing.ApplyDelict(ship.Race, 20);
                if (ship.Spec.capital == SpawnSpec.CapitalVossk) Session.BattleshipsDestroyed++;   // a battleship (1667) too
                if (looted) Session.CapitalLootReadyAt = Session.PlaySeconds + CapitalShips.LootCooldownSeconds;
            }
        }

        /// <summary>A fleet battle's capital ship died: the winners call it and pay a player who helped.</summary>
        void BattleOver(Target t)
        {
            var winner = ship.Spec.battleFoe;
            if (winner == null) return;
            traffic.RaceRadio(winner.race == 1
                ? Localization.Extra("battleWonVossk", "The Terran warship is breaking up. Victory to the Vossk!")
                : Localization.Extra("battleWonTerran", "The Vossk battleship is going down! Well fought, pilots."), winner.race);
            var winnerShip = foe ?? traffic.Ships.Find(s => s.Spec == winner);
            bool helped = !t.killedByNpc || playerDamageTotal >= ship.Hp.maxHull / 20;
            bool winnersHostile = Standing.IsEnemy(winner.race) || (winnerShip != null && (winnerShip.turnedEnemy || winnerShip.alwaysEnemy));
            if (helped && !winnersHostile) traffic.PayBounty(CapitalShips.BattleBounty);
        }

        /// <summary>The carrier / Vossk battleship breaking up: an explosion somewhere on its hull every 0.4-0.9 s.</summary>
        void UpdateDeath(float dtMs)
        {
            if (!CapitalShips.Killable(ship.Spec)) return;
            blastMs -= dtMs;
            if (blastMs > 0f) return;
            blastMs = Random.Range(400f, 900f);
            var boxes = ship.Target.boxes;
            if (boxes == null || boxes.Length == 0) return;
            var b = boxes[Random.Range(0, boxes.Length)];
            var local = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
            Explosion.Spawn(0, ship.transform.TransformPoint(local), Random.onUnitSphere, Random.Range(3f, 6f),
                            assets != null ? CombatAssets.Pick(assets.explosionMid) : null, true);
        }
    }
}
