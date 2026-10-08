// CapitalShips.cs
// Remake (players' suggestion; option "Capital ship enhancements", Settings.CapitalShips, off by default): the free-flight
// capital ships (TrafficPlan.AddCapitalShip: the Terran battleship, the carrier past step 103, the Vossk battleship past 140;
// npc_combat_specials.md 2) fight back. Plain C# rules; CapitalShip (on the host) runs them in the orbit.
//   escorts     3 + rank / 7 (+1 on Hard / Extreme) of the dearer half of the race's fighters (NpcTables.StrongFighter),
//               hull x1.5, gun x1.3, looping round the host
//   turrets     (1000 + 150 x rank) x difficulty hull, gun x(1 + 0.04 x rank)
//   carrier / Vossk battleship   killable (the original's are indestructible decor at 9 999 999): the battleship's hull x8/5
//               / x6/5 (Level::createShip kind 1 for ship 14: 25 x the fighter formula), EMP x3 a freighter's, the
//               collision boxes (2005 / 2006) as hit boxes, 8 s of explosions along the hull, then an x14 blast; their
//               turrets go with them; a crate of two pieces of tech level 5+ equipment, rare goods and energy cells, at most
//               once an hour of play (Session.CapitalLootReadyAt); destroying one by the player is a delict of 20 with the race
//   provoked    the player's first hit on a friendly or neutral one: "Hold your fire!" (426-428); 3 hits or 0.5 % of its hull:
//               the race turns hostile (Traffic.AlarmAllFriends, the station remembers it); a hostile one at once
//   Inflicts    the carrier launches 5 Inflicts (ship 5) from its deck pads every 20 s, 15 in all, while it was attacked
//               (by NPCs, or by the player once provoked) in the last 20 s; they share its hostility
//   resupply    the carrier is a docking target (ObjectDocking.Resupply, the deck pads of step 102's docking set 5) for a
//               pilot the Terrans trust (standing 71+) flying a Terran ship, or anyone with a Terran signature; docked, a shop
//               window like the hangar's (UI.CarrierShopWindow) sells the repair of hull and armor, rounds for each mounted
//               secondary (up to 50) and energy cells (the hold's room) at the station's prices x1.25
//   fleet battle 3 % of the free-flight orbits in Terran and Vossk systems past step 103 (TrafficPlan.AddFleetBattle): a
//               Terran carrier or battleship and the Vossk battleship side by side 80 000 units apart, each with its turrets
//               and escorts and a wing of 4 + rank / 5 fighters looping round the other; the two close in at 0.2 u/ms to
//               50 000 (their turrets ride along) and the enhanced turrets reach and aim at the nearest point of an enemy
//               hull (Target.NearestPoint), their shots flying 3600 ms; the system race calls the battle 6 s in, the side
//               that wins calls it; battle music while both stand; the player picks a side through the standing (or by
//               shooting one: provoked as above), and helping destroy one (its kill or 5 % of its hull) while the winners
//               aren't hostile pays 30 000 + 3000 x rank
//   missiles    salvos of homing missiles (CapitalShip): at the enemy capital ship in a fleet battle, else at hostile ships
//               within 2 km of the hull; the player's boost shakes off a salvo aimed at them

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.UI;
using UnityEngine;

namespace GoF2Remake.World
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class CapitalShips
    {
        public const int Inflict = 5;
        public const int CarrierInflicts = 15, InflictsPerWave = 5;
        public const float WaveMs = 20000f, UnderAttackMs = 20000f;
        public const float DeathMs = 8000f, DeathScale = 14f;
        public const float LootCooldownSeconds = 3600f;
        /// <summary>docks_hd set 5: the carrier's deck pads (step 102's drop-off points; approach points 2000 units above).</summary>
        public const int DeckPoints = 5;
        public const float TurretLifetimeMs = 3600f;
        // Missiles (CapitalShip): each race its own, the Terrans' Intelli Jet (37) and the Vossk-made S'koonn (38, attr 60);
        // salvos of 4, 250 ms apart, every 12-15 s; the item's own speed (18 / 10 u/ms; MissileSpeed without one) and 10 s of
        // flight.
        public const int TerranMissile = 37, VosskMissile = 38, MissilesPerSalvo = 4;
        public const float MissileGapMs = 250f, SalvoMs = 12000f, SalvoJitterMs = 3000f, MissileSpeed = 8f, MissileLifetimeMs = 10000f;
        public const float MissileRangeUnits = 40000f, MissileCapitalFactor = 10f, MissileFactor = 3f;
        public const int FleetBattleChance = 3;
        public const float BattleSeparationUnits = 80000f, BattleHoldUnits = 50000f, BattleSpeed = 0.2f;   // units, u/ms
        public const float BattleRadioMs = 6000f;
        public static int BattleBounty => 30000 + 3000 * Rank;
        /// <summary>The carrier's name (lock plate, docking, its shop window).</summary>
        public static string CarrierName => Localization.Extra("capitalCarrier", "Carrier");
        const float ResupplyMarkup = 1.25f;
        const int RepairPerPoint = 10;

        static int Rank => Mathf.Min(Session.Rank, 20);

        /// <summary>A killable host (the carrier or the Vossk battleship; the Terran battleship was always killable).</summary>
        public static bool Killable(SpawnSpec s) => s != null && s.capitalEnhanced && (s.capital == SpawnSpec.CapitalCarrier || s.capital == SpawnSpec.CapitalVossk);

        /// <summary>TrafficPlan.AddCapitalShip with the option on: list[first] is the host, its turrets follow it.</summary>
        public static void Enhance(Database db, List<SpawnSpec> list, int first)
        {
            var host = list[first];
            host.capitalEnhanced = true;
            int turrets = list.Count;
            for (int i = first + 1; i < turrets; i++)
            {
                list[i].hitpoints = (int)((1000 + 150 * Rank) * Session.DifficultyFactor);
                list[i].gunFactor = 1f + 0.04f * Rank;
                list[i].capitalEnhanced = true;
            }
            if (host.capital == SpawnSpec.CapitalCarrier || host.capital == SpawnSpec.CapitalVossk)
            {
                int battleship = NpcTables.Hull(1, 14);
                host.hitpoints = host.capital == SpawnSpec.CapitalCarrier ? battleship * 8 / 5 : battleship * 6 / 5;
                host.empPoints = NpcTables.Emp(1) * 3;
                host.deathMs = DeathMs;
                host.explosionScale = DeathScale;
                host.noLoot = true;   // its crate is CapitalShip's (RollLoot)
                if (host.capital == SpawnSpec.CapitalCarrier)
                {
                    host.name = CarrierName;   // not text 1512 "Carrier": a medal (the hauler), "Spediteur" / "Курьер" in the other languages
                    host.dockingType = ObjectDocking.Resupply;
                    host.spacePoints = DeckPoints;
                }
            }
            // The escorts: a loop round the host, outside its hull (the carrier and the Vossk battleship reach ~35 000 units).
            float r = host.capital == SpawnSpec.CapitalBattleship ? 0.6f : 1f;
            var loop = new Route(true);
            loop.points.Add(host.position + new Vector3(30000, 6000, 0) * r);
            loop.points.Add(host.position + new Vector3(0, 9000, 50000) * r);
            loop.points.Add(host.position + new Vector3(-30000, 6000, 0) * r);
            loop.points.Add(host.position + new Vector3(0, 9000, -50000) * r);
            int escorts = 3 + Rank / 7 + (Session.Difficulty >= Session.DifficultyHard ? 1 : 0);
            for (int i = 0; i < escorts; i++)
            {
                int ship = NpcTables.StrongFighter(db, host.race);
                var route = new Route(true);   // the loop from its own corner (NpcShip.Setup clones it from index 0)
                for (int k = 0; k < loop.points.Count; k++) route.points.Add(loop.points[(i + 1 + k) % loop.points.Count]);
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Escort, race = host.race, ship = ship, route = route, capitalPart = true, capitalHost = host, capitalEnhanced = true,
                    position = loop.points[i % loop.points.Count] + new Vector3(Random.Range(-3000, 3000), Random.Range(-3000, 3000), Random.Range(-3000, 3000)),
                    hitpoints = (int)(NpcTables.Hull(0, ship) * 1.5f), gunItem = NpcTables.GunItem(host.race), gunFactor = 1.3f,
                });
            }
        }

        // ---- loot ------------------------------------------------------------------------------------------------

        public static bool LootReady => Session.PlaySeconds >= Session.CapitalLootReadyAt;

        static readonly int[] RareGoods = { 113, 108, 107, 102, 101 };   // Implants, Vossk Organs, Organs, Rare Plants / Animals

        /// <summary>Two pieces of tech level 5+ weapons or equipment (a secondary 10-20 of it), 8-15 t of rare goods and 5-10
        /// energy cells; empty while the last capital ship's crate is less than an hour of play ago.</summary>
        public static List<ItemStack> RollLoot(Database db)
        {
            var list = new List<ItemStack>();
            if (db == null || !LootReady) return list;
            var pool = db.Items.FindAll(it => it.maxPrice > 0 && it.blueprint.Count == 0 && it.TypeId <= 3 && it.techLevel >= 5 && it.occurrence > 0
                                              && Hangar.IsSaleable(it.index) && !Modding.ModContent.IsMissingItem(it.index));
            for (int n = 0; n < 2 && pool.Count > 0; n++)
            {
                var it = pool[Random.Range(0, pool.Count)];
                pool.Remove(it);
                list.Add(new ItemStack(it.index, it.TypeId == 1 ? Random.Range(10, 21) : 1));
            }
            list.Add(new ItemStack(RareGoods[Random.Range(0, RareGoods.Length)], Random.Range(8, 16)));
            list.Add(new ItemStack(GalaxyMap.EnergyCellItem, Random.Range(5, 11)));
            return list;
        }

        // ---- the carrier's resupply dock ----------------------------------------------------------------------------

        /// <summary>Why the carrier won't take the player, null = it will: not while it is hostile; a Terran signature, or the
        /// Terrans' trust (standing 71+, Standing.IsFriend) and a Terran ship.</summary>
        public static string DockRefusal(NpcShip carrier)
        {
            if (carrier == null || carrier.Target.hostileToPlayer || carrier.turnedEnemy)
                return Localization.Extra("carrierHostile", "The carrier's flight deck is closed to you.");
            if (InBattle(carrier)) return Localization.Extra("carrierInBattle", "The carrier is in battle: its flight deck is closed.");
            if (Standing.SignatureRace == 0) return null;
            if (Standing.IsFriend(0) && Shop.RaceOfShip(Session.ShipIndex) == 0) return null;
            return Localization.Extra("carrierRefused", "The carrier only takes trusted Terran pilots in a Terran ship, or pilots with a Terran signature.");
        }

        /// <summary>A fleet battle's capital ship while the enemy capital ship stands: not a docking target.</summary>
        public static bool InBattle(NpcShip ship)
        {
            var c = ship != null && ship.Spec.fleetBattle ? ship.GetComponent<CapitalShip>() : null;
            return c != null && c.InBattle;
        }

        public enum OfferKind { Repair, Ammo, Cargo }

        /// <summary>One row of the carrier's resupply window (UI.CarrierShopWindow).</summary>
        public class Offer
        {
            public OfferKind kind;
            public int item = -1;          // a mounted secondary or energy cells; -1 = the repair
            public int have;               // mounted (ammo) / in the hold (cargo)
            public int unitPrice;          // per unit; the repair: its whole price
            public int room;               // units it can take now (the hold's room, up to AmmoCap mounted; the repair 1 or 0)
            public int missingHull, missingArmor;
        }

        /// <summary>A mounted secondary is topped up to at most this many (the hold's room limits energy cells).</summary>
        public const int AmmoCap = 50;

        static int Price(Database db, int item) => db.Item(item) == null ? 0 : Shop.PriceList(db, Session.StationIndex, new[] { item })[0];

        public static string NotEnough => Localization.Extra("notEnoughCredits", "Not enough credits.");

        /// <summary>The resupply window's rows: the repair, each mounted secondary (once per item), energy cells; the station's
        /// prices x1.25.</summary>
        public static List<Offer> ResupplyOffers(Database db, PlayerHealth health)
        {
            var list = new List<Offer>();
            var hp = health != null ? health.Hp : null;
            if (hp != null)
            {
                int mh = Mathf.Max(0, hp.maxHull - hp.hull), ma = Mathf.Max(0, hp.maxArmor - hp.armor);
                list.Add(new Offer { kind = OfferKind.Repair, missingHull = mh, missingArmor = ma, unitPrice = (mh + ma) * RepairPerPoint, room = mh + ma > 0 ? 1 : 0 });
            }
            foreach (var e in Session.Equipment)
            {
                var it = db.Item(e.item);
                if (it == null || it.TypeId != 1 || list.Exists(o => o.item == e.item)) continue;
                int have = 0;
                foreach (var x in Session.Equipment) if (x.item == e.item) have += Mathf.Max(0, x.amount);
                list.Add(new Offer { kind = OfferKind.Ammo, item = e.item, have = have, unitPrice = UnitPrice(db, e.item), room = Mathf.Max(0, AmmoCap - have) });
            }
            int cells = GalaxyMap.EnergyCellItem;
            list.Add(new Offer { kind = OfferKind.Cargo, item = cells, have = Shop.CargoOf(cells), unitPrice = UnitPrice(db, cells), room = Mathf.Max(0, Shop.FreeCargo(db)) });
            return list;
        }

        public static int UnitPrice(Database db, int item) => Mathf.Max(1, Mathf.RoundToInt(Price(db, item) * ResupplyMarkup));

        /// <summary>Buys up to 'units' of the offer, as many as its room and the credits allow; 'bought' = the units bought
        /// (the repair: 1); the HUD message (the reason when nothing was bought).</summary>
        public static string Buy(PlayerHealth health, Offer o, int units, out int bought)
        {
            bought = 0;
            if (o.kind == OfferKind.Repair)
            {
                var hp = health != null ? health.Hp : null;
                if (hp == null || o.room <= 0) return Localization.Extra("resupplyRepaired", "Hull and armor intact");
                if (Session.Credits < o.unitPrice) return NotEnough;
                Session.Credits -= o.unitPrice;
                hp.hull = hp.maxHull;
                hp.armor = hp.maxArmor;
                health.Target.hp = hp.hull;
                bought = 1;
                return Localization.Extra("resupplyRepairDone", "Repaired.");
            }
            if (o.room <= 0)
                return o.kind == OfferKind.Cargo ? Localization.Extra("resupplyHoldFull", "The hold is full.")
                     : string.Format(Localization.Extra("resupplyAmmoFull", "Topped up: {0} is the most the carrier hands out."), AmmoCap);
            int n = Mathf.Min(units, o.room, Session.Credits / Mathf.Max(1, o.unitPrice));
            if (n <= 0) return NotEnough;
            Session.Credits -= n * o.unitPrice;
            if (o.kind == OfferKind.Ammo)
            {
                var stack = Session.Equipment.Find(e => e.item == o.item);   // the gun rigs share the mounted stack
                if (stack != null) stack.amount += n;
            }
            else Shop.AddToCargo(o.item, n);
            o.have += n;
            o.room -= n;
            bought = n;
            return o.kind == OfferKind.Cargo ? $"+{n}t {GameNames.Item(o.item)}" : $"+{n} {GameNames.Item(o.item)}";
        }
    }
}
