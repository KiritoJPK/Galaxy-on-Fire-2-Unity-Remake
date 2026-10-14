// SpeakerSpec.cs
// A speaker as it travels from the server's commands to each game (NetAdmin.ResolveSpeaker: a /dialog page, a radio call, a
// question, a bar mission's client): "id SEP name SEP extra", where id >= 0 is a story speaker (a name = renamed), -1 a
// generated face (extra: its descriptor "body,p0,p1,p2,p3"), -2 the reader (Keith's face with the pilot's name) and -3 a
// mod's character (extra: its key "mod_id:character_id", Modding.ModCharacters). The separator is US in /dialog and the
// radio, RS where the spec sits inside another field.

using GoF2Remake.Data;
using GoF2Remake.Modding;

namespace GoF2Remake.Events
{
    public struct SpeakerSpec
    {
        public const int Generated = -1, Reader = -2, Character = -3;

        public int id;
        public string name;
        public int[] face;
        public string characterKey;

        /// <summary>The mod character it names (null: not one, or its mod isn't on here).</summary>
        public ModCharacters.Def CharacterDef => characterKey != null ? ModCharacters.Find(characterKey) : null;

        public static bool TryParse(string spec, char separator, out SpeakerSpec s)
        {
            s = default;
            var f = (spec ?? "").Split(separator);
            if (f.Length < 2 || !int.TryParse(f[0], out s.id)) return false;
            s.name = f[1];
            string extra = f.Length > 2 ? f[2] : "";
            if (s.id == Generated && extra.Length > 0)
            {
                var parts = extra.Split(',');
                s.face = new int[parts.Length];
                for (int k = 0; k < parts.Length; k++) int.TryParse(parts[k], out s.face[k]);
            }
            else if (s.id == Character) s.characterKey = extra;
            return true;
        }

        /// <summary>The name to show: the renamed / given one, else the character's or the story speaker's.</summary>
        public string DisplayName(string reader)
        {
            string n = (name ?? "").Replace("%player%", reader ?? "");
            if (id == Reader) return reader ?? n;
            if (n.Length > 0) return n;
            if (id == Character) return CharacterDef?.Name ?? characterKey ?? "";
            if (id >= 0 && id < StoryTable.SpeakerCount) return StoryTable.SpeakerName(id);
            return "";
        }

        /// <summary>A mod character's spec for the server ("-3 SEP rename SEP key").</summary>
        public static string OfCharacter(ModCharacters.Def c, string rename, char separator) =>
            Character.ToString() + separator + (rename ?? "") + separator + c.key;
    }
}
