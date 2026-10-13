// ModJson.cs
// Reading a mod's JSON files (Newtonsoft's JToken, not JsonUtility: mod files leave fields out, use dictionaries and
// either a number or a "mod:id" key for references). Errors name the file and line, so a modder can find them;
// comments and trailing commas are allowed.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GoF2Remake.Modding
{
    public class ModJsonException : Exception
    {
        public ModJsonException(string message) : base(message) { }
    }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModJson
    {
        static readonly JsonLoadSettings Settings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        };

        /// <summary>A file of the mod parsed; null when it doesn't exist. Throws ModJsonException with the file and line.</summary>
        public static JToken Read(ModSource source, string path)
        {
            string text;
            try { text = source.ReadText(path); }
            catch (System.Exception e) when (ModFileException.IsFileError(e)) { throw new ModJsonException($"{path}: {e.Message}"); }
            if (text == null) return null;
            try
            {
                using var reader = new JsonTextReader(new System.IO.StringReader(text)) { DateParseHandling = DateParseHandling.None };
                return JToken.ReadFrom(reader, Settings);
            }
            catch (JsonReaderException e) { throw new ModJsonException($"{path} line {e.LineNumber}: {e.Message}"); }
        }

        /// <summary>"file line N" for messages about a value.</summary>
        public static string Where(JToken t, string file)
        {
            var li = (IJsonLineInfo)t;
            return li != null && li.HasLineInfo() ? $"{file} line {li.LineNumber}" : file;
        }

        public static bool Has(JObject o, string key) => o.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var v) && v.Type != JTokenType.Null;

        public static JToken Get(JObject o, string key) => o.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var v) && v.Type != JTokenType.Null ? v : null;

        public static string Str(JObject o, string key, string fallback = null)
        {
            var v = Get(o, key);
            return v == null ? fallback : v.Type == JTokenType.String ? (string)v : v.ToString(Formatting.None);
        }

        public static int Int(JObject o, string key, int fallback, string file)
        {
            var v = Get(o, key);
            if (v == null) return fallback;
            if (v.Type == JTokenType.Integer) return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)v));
            if (v.Type == JTokenType.Float) return (int)Math.Round((double)v);
            if (v.Type == JTokenType.Boolean) return (bool)v ? 1 : 0;
            if (v.Type == JTokenType.String && int.TryParse((string)v, out int n)) return n;
            throw new ModJsonException($"{Where(v, file)}: \"{key}\" must be a whole number");
        }

        public static float Float(JObject o, string key, float fallback, string file)
        {
            var v = Get(o, key);
            if (v == null) return fallback;
            if (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) return (float)(double)v;
            if (v.Type == JTokenType.String && float.TryParse((string)v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            throw new ModJsonException($"{Where(v, file)}: \"{key}\" must be a number");
        }

        public static bool Bool(JObject o, string key, bool fallback, string file)
        {
            var v = Get(o, key);
            if (v == null) return fallback;
            if (v.Type == JTokenType.Boolean) return (bool)v;
            if (v.Type == JTokenType.Integer) return (long)v != 0;
            throw new ModJsonException($"{Where(v, file)}: \"{key}\" must be true or false");
        }

        public static List<string> Strings(JObject o, string key)
        {
            var l = new List<string>();
            var v = Get(o, key);
            if (v is JArray a) { foreach (var e in a) if (e.Type != JTokenType.Null) l.Add(e.Type == JTokenType.String ? (string)e : e.ToString(Formatting.None)); }
            else if (v != null) l.Add((string)v);
            return l;
        }

        /// <summary>A text that is either one string (any language) or { "en": "...", "de": "..." }.</summary>
        public static Dictionary<string, string> Text(JObject o, string key)
        {
            var v = Get(o, key);
            if (v == null) return null;
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (v is JObject t) { foreach (var p in t.Properties()) if (p.Value.Type == JTokenType.String) d[p.Name] = (string)p.Value; }
            else d[""] = v.Type == JTokenType.String ? (string)v : v.ToString(Formatting.None);
            return d;
        }
    }
}
