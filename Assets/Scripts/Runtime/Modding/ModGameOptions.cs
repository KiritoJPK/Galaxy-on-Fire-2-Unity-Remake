// ModGameOptions.cs
// Remake mods: options a mod offers when a new game starts (gameoptions.json), each a card with a banner in the main menu's
// Game options panel (MainMenu.RefreshModOptions) like the Kaamo Club / Hardcore toggles:
//   [ { "id": "canon_ships", "name": "All Canon Ships" | { "en": ..., "de": ... }, "description": ...,
//       "image": "card.png" (the card's art, like the campaign cards: 290 x 448 or a multiple), "imageHover": ... (while
//       selected), "showTitle": true (the name on a plate at the card's foot), "default": true } ]
// The choice is the game's (Session.ModGameOptions, saved): conditions ask for it with option(mod_id:option_id) (the
// mod's "available" fields, quests' "Starts when", event graphs; EventRunner.Condition). The last choice per option is
// remembered for the next new game (PlayerPrefs newgame_modopt_<key>). A multiplayer session plays with every option at its
// default (its mods arrive from the host after the game is made).

using System;
using System.Collections.Generic;
using GoF2Remake.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModGameOptions
    {
        public const string File = "gameoptions.json";

        static readonly HashSet<string> Fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "name", "description", "image", "imageHover", "showTitle", "default" };

        public sealed class Def
        {
            public ModInfo mod;
            public string localId, image, imageHover;
            public bool showTitle = true;
            public Dictionary<string, string> name, description;
            public bool defaultOn;
            public string Key => mod.Id + ":" + localId;
            public string Name => ModManifest.Pick(name) ?? localId;
            public string Description => ModManifest.Pick(description) ?? "";
        }

        static readonly List<Def> defs = new List<Def>();
        static int parsedRevision = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { defs.Clear(); parsedRevision = -1; }

        /// <summary>The options of the mods that are on, in load order.</summary>
        public static List<Def> All()
        {
            if (parsedRevision == ModManager.Revision) return defs;
            parsedRevision = ModManager.Revision;
            defs.Clear();
            foreach (var mod in ModManager.Active)
            {
                try { Read(mod, defs); }
                catch (ModJsonException e) { Warn(mod, e.Message); }
            }
            return defs;
        }

        static void Read(ModInfo mod, List<Def> into)
        {
            if (!(ModJson.Read(mod.Source, File) is JToken token)) return;
            if (!(token is JArray list)) throw new ModJsonException($"{File}: must be a list [ {{ ... }}, ... ]");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in list)
            {
                string where = ModJson.Where(t, File);
                if (!(t is JObject o)) throw new ModJsonException($"{where}: each option must be an object {{ ... }}");
                foreach (var prop in o.Properties())
                    if (!Fields.Contains(prop.Name)) Warn(mod, $"{ModJson.Where(prop, File)}: unknown field \"{prop.Name}\" (ignored)");
                var d = new Def
                {
                    mod = mod, localId = ModJson.Str(o, "id"), image = ModJson.Str(o, "image"), imageHover = ModJson.Str(o, "imageHover"),
                    showTitle = ModJson.Bool(o, "showTitle", true, File),
                    name = ModJson.Text(o, "name"), description = ModJson.Text(o, "description"),
                    defaultOn = ModJson.Bool(o, "default", false, File),
                };
                if (string.IsNullOrEmpty(d.localId) || !ModManifest.ValidId(d.localId))
                    throw new ModJsonException($"{where}: \"id\" is missing or uses more than a-z, 0-9, _ and -");
                if (!ids.Add(d.localId)) throw new ModJsonException($"{where}: the id \"{d.localId}\" is used twice");
                if (!string.IsNullOrEmpty(d.image) && !mod.Source.Exists(d.image)) Warn(mod, $"{where}: the image \"{d.image}\" isn't in the mod");
                into.Add(d);
            }
        }

        static void Warn(ModInfo mod, string message)
        {
            if (!mod.Warnings.Contains(message)) mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }

        /// <summary>An option by "mod_id:option_id", or by its option id alone when only one mod has it.</summary>
        public static Def Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            key = key.Trim();
            Def only = null;
            int matches = 0;
            foreach (var d in All())
            {
                if (string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)) return d;
                if (string.Equals(d.localId, key, StringComparison.OrdinalIgnoreCase)) { only = d; matches++; }
            }
            return matches == 1 ? only : null;
        }

        /// <summary>The option is on in this game (and its mod is on); in a multiplayer session its default.</summary>
        public static bool IsOn(string key)
        {
            var d = Find(key);
            if (d == null) return false;
            return Multiplayer.NetGame.SessionGame ? d.defaultOn : Session.ModGameOptions.Contains(d.Key);
        }

        /// <summary>The new game panel's choice (the last one made for this option, else its default).</summary>
        public static bool Chosen(Def d) => PlayerPrefs.GetInt(PrefKey(d), d.defaultOn ? 1 : 0) == 1;

        public static void SetChosen(Def d, bool on)
        {
            PlayerPrefs.SetInt(PrefKey(d), on ? 1 : 0);
            PlayerPrefs.Save();
        }

        static string PrefKey(Def d) => "newgame_modopt_" + d.Key;

        /// <summary>MainMenu.BeginGame (after Session.ResetNewGame): the chosen options become the game's.</summary>
        public static void ApplyChoices()
        {
            Session.ModGameOptions.Clear();
            foreach (var d in All()) if (Chosen(d)) Session.ModGameOptions.Add(d.Key);
        }

        /// <summary>The card's banner (the mod's PNG; null: none).</summary>
        public static Texture2D Image(Def d) => string.IsNullOrEmpty(d.image) ? null : ModMaterials.Texture(d.mod, d.image, false, true);
        public static Texture2D ImageHover(Def d) => string.IsNullOrEmpty(d.imageHover) ? null : ModMaterials.Texture(d.mod, d.imageHover, false, true);
    }
}
