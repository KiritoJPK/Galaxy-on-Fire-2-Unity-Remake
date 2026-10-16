// WeaponTint.cs
// Remake: the weapon colour of a customizable ship's build (the hangar's Customize screen, Modding.ModShipKits Build.weaponColor):
// the energy weapons' projectiles, muzzle flashes, impacts and thermo trails in one colour, on the primaries and on turrets
// firing energy bolts. No colour = the weapons' own look. Energy = the lasers and beams (category 0), blasters (1, the EMP
// blasters too), thermo guns (3), and the turrets whose shots are blaster bolts (Hammerhead D1 / D2A2, L'ksaar, Matador TS:
// their weapon_fx projectiles are blaster ones); the auto-cannons, railguns, mass drivers, scatter guns and the cannon turrets
// keep theirs. Mod items count by their base (Look).
// A tinted material is a copy with its main texture recoloured (Hidden/GoF2/WeaponTint: the texel's brightness x the colour,
// the hottest part kept white; a red laser's texture can't just be multiplied blue) and its colour made grey at its own
// intensity (so the fades and the HDR glow stay); copies and textures are cached per source and colour for the run.

using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class WeaponTint
    {
        static readonly Dictionary<(Material, Color32), Material> materials = new Dictionary<(Material, Color32), Material>();
        static readonly Dictionary<(Texture, Color32), Texture> textures = new Dictionary<(Texture, Color32), Texture>();
        static Material blit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { materials.Clear(); textures.Clear(); blit = null; }

        static readonly HashSet<int> EnergyTurrets = new HashSet<int> { 47, 48, 49, 224 };

        /// <summary>Whether an item fires energy (its shots take the weapon colour).</summary>
        public static bool IsEnergy(ItemData item)
        {
            if (item == null) return false;
            int look = item.Look;
            int category = Database.Shared.Item(look)?.categoryId ?? item.categoryId;
            return category == 0 || category == 1 || category == 3 || EnergyTurrets.Contains(look);
        }

        /// <summary>The weapon colour of the player's ship (its build's; null = the weapons' own).</summary>
        public static Color? PlayerColor() => ShipColor(Session.ShipIndex, null);

        /// <summary>A kit ship's weapon colour in a build (null: the player's own build of it), null = none.</summary>
        public static Color? ShipColor(int ship, Modding.ModShipKits.Build build)
        {
            var kit = Modding.ModShipKits.CustomizationOf(ship);   // a kit ship's kit, or the customizations mods give another ship
            if (kit == null) return null;
            var b = build != null ? Modding.ModShipKits.Normalize(kit, Modding.ModShipKits.TypeOf(ship), build, Modding.ModShipKits.DefaultBuildOf(ship, kit))
                  : Modding.ModShipKits.PlayerBuild(ship);
            return Modding.ModShipKits.WeaponColor(kit, b);
        }

        /// <summary>The colour a gun of 'item' takes from 'color' (null: not an energy weapon, or no colour).</summary>
        public static Color? For(ItemData item, Color? color) => color.HasValue && IsEnergy(item) ? color : null;

        /// <summary>A tinted copy of a weapon effect's material (the source itself when it has no main texture).</summary>
        public static Material Tinted(Material source, Color color)
        {
            if (source == null) return null;
            var key = (source, (Color32)color);
            if (materials.TryGetValue(key, out var made) && made != null) return made;
            var copy = new Material(source) { name = source.name + " (weapon colour)" };
            if (copy.HasProperty("_MainTex") && copy.GetTexture("_MainTex") is Texture tex)
            {
                var t = TintedTexture(tex, color);
                if (t != null) copy.SetTexture("_MainTex", t);
            }
            foreach (var prop in new[] { "_Color", "_BaseColor" })
            {
                if (!copy.HasProperty(prop)) continue;
                var c = copy.GetColor(prop);
                float k = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                copy.SetColor(prop, new Color(k, k, k, c.a));
            }
            materials[key] = copy;
            return copy;
        }

        static Texture TintedTexture(Texture source, Color color)
        {
            var key = (source, (Color32)color);
            if (textures.TryGetValue(key, out var made) && made != null) return made;
            if (blit == null)
            {
                var shader = Resources.Load<Shader>("GoF2Weapons/WeaponTint");
                if (shader == null) { Debug.LogWarning("WeaponTint: the Hidden/GoF2/WeaponTint shader is missing"); return null; }
                blit = new Material(shader);
            }
            var rt = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = source.name + " (weapon colour)", useMipMap = true, autoGenerateMips = true,
                wrapMode = source.wrapMode, filterMode = source.filterMode, anisoLevel = source.anisoLevel,
            };
            blit.SetColor("_Tint", color.linear);
            Graphics.Blit(source, rt, blit);
            textures[key] = rt;
            return rt;
        }
    }
}
