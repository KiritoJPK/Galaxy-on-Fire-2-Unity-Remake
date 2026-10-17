// KitRules.cs
// Remake mods (PR #87, ported for a test): what a customizable ship's build (Modding.ModShipKits, GoF2.Space) does to the
// game's rules, here in GoF2.Game for the database, the shop and the hangar: the hull, cargo, handling and slots in % of the
// ship's own, and the parts' flight bonuses. ModShipKits works the build out and hands it over (GameHooks.KitBuildEffect,
// KitMounts); without it (no flight layer, no kit ship) every ship is its own.

using System;
using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Modding
{
    /// <summary>What a build does to its ship: the parts' stats added up, and the slots per type (-1 = the ship's own).</summary>
    public sealed class KitEffect
    {
        public float armor, cargo, handling;   // %
        public int equipment;
        public int primary = -1, secondary = -1, turret = -1;
        public readonly Flight.FlightBonus flight = new Flight.FlightBonus();
    }

    public static class KitRules
    {
        /// <summary>A kit build's assembly name: "ship_NNN_mod#<build>" (ModShipKits.VariantAssembly).</summary>
        public const char VariantSeparator = '#';

        /// <summary>A game model's copy for one ship: "&lt;model&gt;#ship=N" (a mod ship with its own textures,
        /// Database.ShipAssembly), "&lt;model&gt;#ship=N;skin=id" (a skin of a ship without a kit, ModShipKits.VariantAssembly).</summary>
        public const string ShipToken = "ship=";

        /// <summary>The build effect of the player's ship (null: not a kit ship); another ship: its default build.</summary>
        public static KitEffect BuildEffect(int ship) => GameHooks.KitBuildEffect?.Invoke(ship);

        /// <summary>Database.MountsOf for a kit ship (its build's parts), null: not a kit ship.</summary>
        public static List<WeaponMount> BuildMounts(int ship, int slotType) => GameHooks.KitMounts?.Invoke(ship, slotType);

        static int Pct(int value, float pct) => Mathf.Max(1, Mathf.RoundToInt(value * (1f + pct / 100f)));

        /// <summary>A ship's hull with its build ('s' null: 'fallback').</summary>
        public static int Armor(ShipData s, int fallback) => s == null ? fallback : BuildEffect(s.index) is KitEffect e ? Pct(s.armor, e.armor) : s.armor;
        public static int Cargo(ShipData s, int fallback) => s == null ? fallback : BuildEffect(s.index) is KitEffect e ? Mathf.Max(0, Mathf.RoundToInt(s.cargo * (1f + e.cargo / 100f))) : s.cargo;
        public static float Handling(ShipData s) => BuildEffect(s.index) is KitEffect e ? s.handling * (1f + e.handling / 100f) : s.handling;

        /// <summary>A ship's slots with its build (a copy; the ship's own without a build).</summary>
        public static ShipSlots Slots(ShipData s)
        {
            var b = s?.slots;
            if (b == null) return null;
            if (!(BuildEffect(s.index) is KitEffect e)) return b;
            return new ShipSlots
            {
                primary = e.primary >= 0 ? e.primary : b.primary, secondary = e.secondary >= 0 ? e.secondary : b.secondary,
                turret = e.turret >= 0 ? e.turret : b.turret, equipment = Math.Max(0, b.equipment + e.equipment),
            };
        }
    }
}
