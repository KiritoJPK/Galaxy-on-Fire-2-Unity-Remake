// EntryPatch.cs
// Small in-place edits of a mod file's JSON entry (the Mod Builder MCP server's patch_entry): operations by path into the
// entry, like JSON Patch (RFC 6902) with plainer paths: "steps/2/text", "steps/3/then/0", "title" (slash-separated; a number
// indexes an array, "-" is past its end; a leading "/" is allowed, "~1" / "~0" stand for "/" and "~" in a key).
//   set     the value at the path (an array element replaced, or appended at "-"; missing object keys on the way made)
//   insert  the value into the array at the path's index (shifting the rest), "-" at its end
//   remove  the value at the path
//   move    the value at 'from' to the path (taken out first, then inserted there: the path is read after the removal)
//   test    nothing changes; the whole patch fails unless the value at the path equals 'value'
// All or nothing: the operations run on a copy, the first failing one refuses the patch with its index and why. What no
// operation names is left exactly as it was. Plain C#.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GoF2Remake.Modding
{
    public static class EntryPatch
    {
        /// <summary>'ops' applied to a copy of 'entry': the patched copy, or null and 'error'.</summary>
        public static JToken Apply(JToken entry, JArray ops, out string error)
        {
            error = null;
            if (entry == null) { error = "No entry."; return null; }
            if (ops == null || ops.Count == 0) { error = "ops: a list of operations."; return null; }
            var doc = new JObject { ["root"] = entry.DeepClone() };   // a parent for the root, so a "" path can replace it
            for (int i = 0; i < ops.Count; i++)
            {
                if (!(ops[i] is JObject op)) { error = $"op {i}: not an object."; return null; }
                string kind = ((string)op["op"] ?? "").ToLowerInvariant(), path = (string)op["path"];
                string why = kind switch
                {
                    "set" => Set(doc, path, op["value"]),
                    "insert" => Insert(doc, path, op["value"]),
                    "remove" => Remove(doc, path, out _),
                    "move" => Move(doc, (string)op["from"], path),
                    "test" => Test(doc, path, op["value"]),
                    _ => "op: set, insert, remove, move or test.",
                };
                if (why != null) { error = $"op {i} ({kind} {path}): {why}"; return null; }
            }
            return doc["root"];
        }

        /// <summary>The value at 'path' in 'entry' (a copy), or null and 'error'.</summary>
        public static JToken Get(JToken entry, string path, out string error)
        {
            var doc = new JObject { ["root"] = entry };
            error = Walk(doc, path, out var parent, out string key);
            if (error != null) return null;
            var v = Child(parent, key);
            if (v == null) { error = $"nothing at {path}."; return null; }
            return v.DeepClone();
        }

        // ---- paths --------------------------------------------------------------------------------------------------

        static List<string> Split(string path)
        {
            var parts = (path ?? "").Trim().Trim('/').Split(new[] { '/' }, StringSplitOptions.None).Where(p => p.Length > 0)
                                    .Select(p => p.Replace("~1", "/").Replace("~0", "~")).ToList();
            parts.Insert(0, "root");
            return parts;
        }

        /// <summary>The container holding the path's last part ('parent') and that part ('key'); 'create' makes missing
        /// object keys on the way. Null, or why not.</summary>
        static string Walk(JObject doc, string path, out JContainer parent, out string key, bool create = false)
        {
            var parts = Split(path);
            JContainer cur = doc;
            for (int i = 0; i < parts.Count - 1; i++)
            {
                var next = Child(cur, parts[i]);
                if (next == null)
                {
                    if (!create || !(cur is JObject o)) { parent = null; key = null; return $"no {string.Join("/", parts.Skip(1).Take(i))}{(i > 0 ? "/" : "")}{parts[i]} ({Describe(cur, parts[i])})."; }
                    next = o[parts[i]] = new JObject();
                }
                if (!(next is JContainer c)) { parent = null; key = null; return $"{parts[i]} is a {next.Type}, not an object or list."; }
                cur = c;
            }
            parent = cur;
            key = parts[parts.Count - 1];
            return null;
        }

        static JToken Child(JContainer c, string key)
        {
            if (c is JObject o) return o[key];
            if (c is JArray a && int.TryParse(key, out int i) && i >= 0 && i < a.Count) return a[i];
            return null;
        }

        static string Describe(JContainer c, string key) =>
            c is JArray a ? (int.TryParse(key, out _) ? $"the list has {a.Count}" : "a list takes numbers") : "no such key";

        // ---- operations ---------------------------------------------------------------------------------------------

        static string Set(JObject doc, string path, JToken value)
        {
            if (value == null) return "value: the new value.";
            string err = Walk(doc, path, out var parent, out string key, create: true);
            if (err != null) return err;
            switch (parent)
            {
                case JObject o: o[key] = value.DeepClone(); return null;
                case JArray a:
                    if (key == "-") { a.Add(value.DeepClone()); return null; }
                    if (!int.TryParse(key, out int i)) return $"{key}: a list takes numbers (or - to append).";
                    if (i < 0 || i >= a.Count) return $"no element {key} (the list has {a.Count}; insert adds one).";
                    a[i] = value.DeepClone();
                    return null;
            }
            return "can't set there.";
        }

        static string Insert(JObject doc, string path, JToken value)
        {
            if (value == null) return "value: what to insert.";
            string err = Walk(doc, path, out var parent, out string key);
            if (err != null) return err;
            if (!(parent is JArray a)) return "insert goes into a list (use set for an object's key).";
            if (key == "-") { a.Add(value.DeepClone()); return null; }
            if (!int.TryParse(key, out int i) || i < 0 || i > a.Count) return $"index {key}: 0..{a.Count} (or -).";
            a.Insert(i, value.DeepClone());
            return null;
        }

        static string Remove(JObject doc, string path, out JToken removed)
        {
            removed = null;
            if (Split(path).Count < 2) return "the entry itself can't be removed here.";
            string err = Walk(doc, path, out var parent, out string key);
            if (err != null) return err;
            switch (parent)
            {
                case JObject o:
                    if (o[key] == null) return "nothing there.";
                    removed = o[key];
                    o.Remove(key);
                    return null;
                case JArray a:
                    if (!int.TryParse(key, out int i) || i < 0 || i >= a.Count) return $"no element {key} (the list has {a.Count}).";
                    removed = a[i];
                    a.RemoveAt(i);
                    return null;
            }
            return "can't remove there.";
        }

        static string Move(JObject doc, string from, string path)
        {
            if (string.IsNullOrEmpty(from)) return "from: the path to move.";
            var f = Split(from); var t = Split(path);
            if (t.Count > f.Count && t.Take(f.Count).SequenceEqual(f)) return "can't move a value into itself.";
            string err = Remove(doc, from, out var value);
            if (err != null) return "from: " + err;
            var probe = Walk(doc, path, out var parent, out _);
            if (probe != null) return probe;
            return parent is JArray ? Insert(doc, path, value) : Set(doc, path, value);
        }

        static string Test(JObject doc, string path, JToken value)
        {
            string err = Walk(doc, path, out var parent, out string key);
            if (err != null) return err;
            var v = Child(parent, key);
            if (!JToken.DeepEquals(v, value)) return $"it is {(v == null ? "missing" : v.ToString(Newtonsoft.Json.Formatting.None))}, not {value?.ToString(Newtonsoft.Json.Formatting.None) ?? "null"}: read the entry again.";
            return null;
        }
    }
}
