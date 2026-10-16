// ModBuilderViewAi.cs
// The hangar's Mod Builder screen as the MCP server's AI drives it (part of ModBuilderView; ModBuilderScreenAi has the hooks
// and their meaning): selecting an entry of any kind edited here as a click on its row would, the camera from a named side
// of the turntable ship (HangarInspect.SetView) or a room (the Interiors kind's free camera), the engine slider, Test fly,
// the hangar picked, and the hardpoints placed onto the hull by a ray from a side (the gizmo's pick colliders), snapped,
// selected, mirrored or removed. Each does what the screen's own controls do, through the same methods.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using GoF2Remake.World;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        static string RefOf(string id) => id == null ? null : id.StartsWith("override:") || id.StartsWith("id:") ? id : "id:" + id;

        internal override string ExternalSelectEntry(string kindName, string id, JObject options)
        {
            if (Project == null) return "No project is open.";
            string r = RefOf(id);
            switch (kindName)
            {
                case "ships":
                {
                    var s = Project.Ships.FirstOrDefault(x => x.Ref == r);
                    if (s == null) return $"No ship \"{id}\" in the project.";
                    SelectShip(s);
                    string tabName = (string)options?["tab"];
                    if (tabName != null)
                    {
                        int t = Array.IndexOf(new[] { "ship", "hardpoints", "loadout" }, tabName);
                        if (t < 0) return "tab: ship, hardpoints or loadout.";
                        SelectTab(t);
                    }
                    return null;
                }
                case "items":
                {
                    var it = Project.Items.FirstOrDefault(x => x.Ref == r);
                    if (it == null) return $"No item \"{id}\" in the project.";
                    currentItem = it; RebuildItems(); BuildItemPage();
                    return null;
                }
                case "characters":
                {
                    var c = Project.Characters.FirstOrDefault(x => x.Ref == r);
                    if (c == null) return $"No character \"{id}\" in the project.";
                    currentCharacter = c; RebuildCharacters(); BuildCharacterPage();
                    return null;
                }
                case "skins":
                {
                    if (!int.TryParse(id, out int ship) || !Project.SkinShips().Contains(ship)) return $"The project has no skin of ship \"{id}\" (the game ship's number).";
                    ShowSkin(ship, true);
                    return null;
                }
                case "blueprints":
                {
                    var b = Project.Blueprints.FirstOrDefault(x => x.Ref == r);
                    if (b == null) return $"No blueprint \"{id}\" in the project.";
                    currentBlueprint = b; RebuildBlueprints(); BuildBlueprintPage();
                    return null;
                }
                case "gameOptions":
                {
                    var o = Project.GameOptions.FirstOrDefault(x => x.Ref == r);
                    if (o == null) return $"No game option \"{id}\" in the project.";
                    currentOption = o; RebuildCampaign(); BuildCampaignPage();
                    return null;
                }
                case "campaign":
                    if (Project.Campaign == null) return "The project has no campaign.";
                    currentOption = null; RebuildCampaign(); BuildCampaignPage();
                    return null;
                case "interiors":
                {
                    var room = Project.Interiors.FirstOrDefault(x => x.Ref == r);
                    if (room == null) return $"No room \"{id}\" in the project.";
                    PickInterior(room);
                    return null;
                }
                case "quests":
                {
                    var q = Project.Quests.FirstOrDefault(x => x.Name == id);
                    if (q == null && !Project.EventGraphs().Contains(id)) return $"No quest \"{id}\" in the project.";
                    if (options?["graph"] is JValue g && g.Type == JTokenType.Boolean) GraphMode = (bool)g;
                    if (options?["wide"] is JValue w && w.Type == JTokenType.Boolean) GraphWide = (bool)w;
                    currentQuest = q; currentGraph = q == null ? id : null; currentStep = null;
                    RebuildQuests(); BuildQuestPage();
                    return null;
                }
                case "systems":
                {
                    var s = Project.Systems.FirstOrDefault(x => x.Ref == r);
                    if (s == null) return $"No system \"{id}\" in the project.";
                    currentSystem = s; pickedGameSystem = -1;
                    RebuildSystems(); BuildSystemPage();
                    builderMap?.FocusSystem(SystemIndex(s));
                    return null;
                }
            }
            return base.ExternalSelectEntry(kindName, id, options);
        }

        internal override JObject ExternalSelection()
        {
            var o = base.ExternalSelection();
            switch (kind)
            {
                case ModBuilder.Kind.Ships: o["entry"] = Current?.Ref; o["tab"] = new[] { "ship", "hardpoints", "loadout" }[tab]; if (gizmo.Selected >= 0) o["hardpoint"] = gizmo.Selected; break;
                case ModBuilder.Kind.Items: o["entry"] = currentItem?.Ref; break;
                case ModBuilder.Kind.Characters: o["entry"] = currentCharacter?.Ref; break;
                case ModBuilder.Kind.Skins: o["entry"] = ModBuilder.SkinShip; break;
                case ModBuilder.Kind.Blueprints: o["entry"] = currentBlueprint?.Ref; break;
                case ModBuilder.Kind.Kits: o["entry"] = currentKit?.Id; o["part"] = currentPart?.Id; if (gizmo.Selected >= 0) o["hardpoint"] = gizmo.Selected; break;
                case ModBuilder.Kind.Campaigns: o["entry"] = currentOption?.Ref ?? (Project.Campaign != null ? "campaign" : null); break;
                case ModBuilder.Kind.Interiors: o["entry"] = currentInterior?.Ref; break;
                case ModBuilder.Kind.Quests: o["entry"] = currentQuest?.Name ?? currentGraph; o["graphView"] = GraphMode; break;
                case ModBuilder.Kind.Systems: o["entry"] = currentSystem?.Ref; break;
            }
            if (InHangar && ShipKind(kind)) { o["turntableShip"] = Session.ShipIndex; o["engines"] = Math.Round(engineLevel, 2); }
            o["hangar"] = HangarTypeName(Session.StationIndex);
            return o;
        }

        // ---- camera -------------------------------------------------------------------------------------------------

        internal override string ExternalCamera(string angle, float zoom, bool reset)
        {
            var view = ModelViewer.Angles.FirstOrDefault(a => a.name == angle);
            if (!reset && view.name == null) return $"angle: one of {string.Join(", ", ModelViewer.Angles.Select(a => a.name))}.";
            if (kind == ModBuilder.Kind.Interiors)
            {
                if (currentInterior == null) return "Pick a room first.";
                gameCamera = false;
                BuildInteriorPage();
                if (reset) { roomYaw = 0.6f; roomPitch = 0.35f; roomDistance = roomRadius * 1.6f; return null; }
                var d = view.from.normalized;
                roomYaw = Mathf.Atan2(d.x, d.z);
                roomPitch = Mathf.Clamp(Mathf.Asin(d.y), -1.45f, 1.45f);
                if (zoom > 0f) roomDistance = Mathf.Clamp(roomRadius * zoom, roomRadius * 0.05f, roomRadius * 6f);
                return null;
            }
            if (!InHangar || !ShipKind(kind)) return "This kind shows the lounge or the map: there is no camera to aim (use screenshot, or view_model for a model).";
            if (boundTransform == null) return "The ship is still being built: try again in a moment.";
            if (!level.Inspect.Active) level.BeginInspect(0.55f);
            if (reset)
            {
                level.Inspect.ResetPan();
                level.Inspect.SetView(boundTransform.TransformDirection(ModelViewer.Angles.First(a => a.name == "threequarter").from), 2.1f);
                return null;
            }
            level.Inspect.ResetPan();
            level.Inspect.SetView(boundTransform.TransformDirection(view.from.normalized), zoom);
            return null;
        }

        internal override bool CameraSettled =>
            !reloading && !reloadPending && (!(InHangar && ShipKind(kind)) || boundTransform != null && (!level.Inspect.Active || level.Inspect.Blend >= 1f));

        protected override IEnumerable<VisualElement> CleanViewHides()
        {
            yield return uiEngines;
            yield return testFly;
            yield return placeHint;
        }

        // ---- engines, test fly, hangar ----------------------------------------------------------------------------

        internal override string ExternalEngines(float value)
        {
            if (!InHangar || !ShipKind(kind)) return base.ExternalEngines(value);
            engineSlider.value = Mathf.Clamp01(value);   // its callback lights them
            return null;
        }

        internal override string ExternalTestFly()
        {
            if (!Flies(kind)) return "Test fly is offered in Ships, Items and Skins: show_kind one of them first.";
            bool skin = kind == ModBuilder.Kind.Skins && ModBuilder.SkinShip >= 0;
            if (Current == null && !skin) return "Add a ship first.";
            if (!InHangar) return "The hangar isn't shown.";
            if (launchPending || level.PlayerFlying) return "A take-off is already under way.";
            StartTestFlight();
            return null;
        }

        internal override string ExternalHangar(string which, out string done)
        {
            done = null;
            var choices = HangarChoices(out int current);
            string w = (which ?? "").Trim().ToLowerInvariant();
            var pick = choices.FirstOrDefault(c => c.name.ToLowerInvariant() == w || c.name.ToLowerInvariant().Replace(" ", "") == w.Replace(" ", "")
                                                   || int.TryParse(w, out int n) && c.station == n);
            if (pick.name == null)
                return "which: " + string.Join(", ", choices.Select(c => $"{c.name.ToLowerInvariant()} ({Database.Shared.Stations[c.station].name}, {c.station})"));
            done = $"{pick.name} hangar at {Database.Shared.Stations[pick.station].name}";
            if (pick.room == current) { done += " (already there)"; return null; }
            SaveNow();
            ModBuilder.SwitchStation(pick.station);
            return null;
        }

        // ---- the test loadout ----------------------------------------------------------------------------------------

        static readonly string[] KindWords = { "primary", "secondary", "turret", "equipment" };

        /// <summary>An item reference as this game's number: a number, a key "mod:id", or this project's item id.</summary>
        int ItemRef(string r)
        {
            if (string.IsNullOrEmpty(r)) return -1;
            int n = ModBuilder.ItemNumber(r);
            if (n < 0 && !r.Contains(":") && Project != null) n = ModBuilder.ItemNumber(Project.Key(r));
            return n;
        }

        JObject LoadoutJson(ProjectShip s)
        {
            var db = Database.Shared;
            var slots = SlotsOf(db, s);
            var kinds = new JObject();
            for (int k = 0; k < 4; k++)
            {
                var list = new JArray();
                for (int i = 0; i < s.SlotCount(k); i++)
                {
                    int item = i < slots[k].Count ? slots[k][i] : -1;
                    list.Add(item >= 0 ? new JObject { ["slot"] = i, ["item"] = item, ["name"] = GameNames.Item(item) } : new JObject { ["slot"] = i, ["item"] = null });
                }
                kinds[KindWords[k]] = list;
            }
            return new JObject { ["ship"] = s.Ref, ["godMode"] = ModBuilder.TestGodMode, ["infiniteAmmo"] = ModBuilder.TestInfiniteAmmo, ["slots"] = kinds };
        }

        internal override string ExternalLoadout(JObject a, out JObject done)
        {
            done = null;
            if (kind != ModBuilder.Kind.Ships) return "The loadout is the Ships kind's: select_entry ships first.";
            var s = Current;
            if (s == null) return "The project has no ship (or none is selected).";
            var db = Database.Shared;
            string op = ((string)a["op"] ?? "list").ToLowerInvariant();
            switch (op)
            {
                case "list": break;
                case "set":
                {
                    int k = Array.IndexOf(KindWords, ((string)a["kind"] ?? "").ToLowerInvariant());
                    if (k < 0) return "kind: primary, secondary, turret or equipment.";
                    int slot = a["slot"] is JValue sv && sv.Type == JTokenType.Integer ? (int)sv : -1;
                    if (slot < 0 || slot >= s.SlotCount(k)) return $"slot: 0..{s.SlotCount(k) - 1} (the ship has {s.SlotCount(k)} {KindWords[k]} slots).";
                    string r = a["item"]?.Type == JTokenType.Integer ? ((int)a["item"]).ToString() : (string)a["item"];
                    int item = string.IsNullOrEmpty(r) || r == "empty" ? -1 : ItemRef(r);
                    if (item < 0 && !string.IsNullOrEmpty(r) && r != "empty") return $"No item \"{r}\" (a number, a key, or this project's item id; the game reads new items after validate).";
                    if (item >= 0)
                    {
                        var it = db.Item(item);
                        if (it == null || it.TypeId != k) return $"{GameNames.Item(item)} isn't a {KindWords[k]} item.";
                    }
                    SelectTab(2);
                    SetSlot(k, slot, item);
                    break;
                }
                case "autofill":
                    SelectTab(2);
                    Project.SetLoadout(s, DefaultLoadout(db, s));
                    MarkDirty();
                    ModBuilder.ApplyShip();
                    BuildLoadoutPage();
                    break;
                case "cheats":
                    if (a["godMode"] is JValue g && g.Type == JTokenType.Boolean) ModBuilder.TestGodMode = (bool)g;
                    if (a["infiniteAmmo"] is JValue am && am.Type == JTokenType.Boolean) ModBuilder.TestInfiniteAmmo = (bool)am;
                    MarkDirty();
                    BuildLoadoutPage();
                    break;
                default: return "op: list, set, autofill or cheats.";
            }
            done = LoadoutJson(s);
            return null;
        }

        // ---- the rooms' markers --------------------------------------------------------------------------------------

        static readonly (string name, Vector3 dir)[] Towards =
        {
            ("down", Vector3.down), ("up", Vector3.up), ("left", Vector3.left), ("right", Vector3.right), ("front", Vector3.forward), ("back", Vector3.back),
        };

        internal override string ExternalRoomMarker(JObject a, out JObject done)
        {
            done = null;
            if (kind != ModBuilder.Kind.Interiors) return "Room markers are the Hangars & bars kind's: select_entry interiors first.";
            var r = currentInterior;
            var room = RoomTransform;
            if (r == null || room == null) return "Pick a room first (select_entry interiors), and wait for it to be built.";
            string op = ((string)a["op"] ?? "list").ToLowerInvariant();
            string name = ((string)a["name"])?.Trim();
            if (op == "place" || op == "remove")
            {
                if (string.IsNullOrEmpty(name)) return "name: the marker (op list shows them; a new parked_N / visitor_N is made by placing it).";
                string prefix = r.IsBar ? "visitor_" : "parked_";
                bool known = MarkerList(r).Any(m => m.name == name) || name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out _);
                if (!known) return $"No marker \"{name}\" in a {(r.IsBar ? "bar" : "hangar")}: {string.Join(", ", MarkerList(r).Select(m => m.name))}, {prefix}N.";
            }
            if (op == "remove")
            {
                r.SetMarker(name, null);
                RoomEdited();
            }
            else if (op == "place")
            {
                Vector3 local;
                if (TryVector(a["position"], out var pos)) local = pos;
                else
                {
                    if (roomColliders.Count == 0) return "The room's surfaces can't be read (or it is still being built): give 'position' instead.";
                    var b = roomColliders[0].bounds;
                    foreach (var c in roomColliders) if (c != null) b.Encapsulate(c.bounds);
                    var at = TryVector(a["at"], out var atLocal) ? room.TransformPoint(atLocal) : b.center;
                    Vector3 dir;
                    if (TryVector(a["toward"], out var tv) && tv.sqrMagnitude > 1e-6f) dir = room.TransformDirection(tv.normalized);
                    else
                    {
                        string tw = ((string)a["toward"] ?? "down").ToLowerInvariant();
                        var t = Towards.FirstOrDefault(x => x.name == tw);
                        if (t.name == null) return "toward: down, up, left, right, front, back or [x, y, z] in the room's frame.";
                        dir = room.TransformDirection(t.dir);
                    }
                    Physics.SyncTransforms();
                    if (!Physics.Raycast(at, dir, out var hit, 100000f, 1 << RoomLayer, QueryTriggerInteraction.Ignore))
                        return $"Nothing of the room that way from [{room.InverseTransformPoint(at).x:0.#}, {room.InverseTransformPoint(at).y:0.#}, {room.InverseTransformPoint(at).z:0.#}] (room metres): try another 'at' or 'toward'.";
                    var world = hit.point;
                    if (name.StartsWith("camera") && !name.EndsWith("_target") || name == "light" || name == "gate_out")
                        world += hit.normal * Mathf.Clamp(roomRadius * 0.02f, 2f, 20f);
                    local = room.InverseTransformPoint(world);
                }
                r.SetMarker(name, local);
                RoomEdited();
            }
            else if (op != "list") return "op: list, place or remove.";
            var list = new JArray();
            foreach (var (n, required) in MarkerList(r))
            {
                var v = MarkerAt(r, n, out bool placed);
                var o = new JObject { ["name"] = n, ["required"] = required, ["placed"] = placed };
                if (v != null) o["position"] = new JArray(Math.Round(v.Value.x, 2), Math.Round(v.Value.y, 2), Math.Round(v.Value.z, 2));
                if (v != null && !placed) o["from"] = "the model's own node";
                list.Add(o);
            }
            done = new JObject { ["room"] = r.Ref, ["type"] = r.IsBar ? "bar" : "hangar", ["unit"] = "metres in the room's frame (y up)", ["markers"] = list };
            if (roomColliders.Count > 0)
            {
                var b = roomColliders[0].bounds;
                foreach (var c in roomColliders) if (c != null) b.Encapsulate(c.bounds);
                Vector3 lo = room.InverseTransformPoint(b.min), hi = room.InverseTransformPoint(b.max);
                done["roomBox"] = new JObject { ["min"] = new JArray(Math.Round(lo.x, 1), Math.Round(lo.y, 1), Math.Round(lo.z, 1)), ["max"] = new JArray(Math.Round(hi.x, 1), Math.Round(hi.y, 1), Math.Round(hi.z, 1)) };
            }
            return null;
        }

        // ---- the star map ------------------------------------------------------------------------------------------

        internal override string ExternalMap(string what)
        {
            if (kind != ModBuilder.Kind.Systems) return "The star map is the Systems kind's: show_kind systems first.";
            if (builderMap == null) return "The map is still opening: try again in a moment.";
            string w = (what ?? "").Trim();
            // Not the full-map overview: framing every system the builder's map shows (placeholders far out too) broke it.
            if (w == "galaxy") { builderMap.BuilderOverview(false); return null; }
            int n = int.TryParse(w, out int k) ? k : ModBuilder.SystemNumber(w.Contains(":") ? w : Project.Key(w));
            if (n < 0 || n >= Database.Shared.Systems.Count) return $"map: a system's number (0..{Database.Shared.Systems.Count - 1}; search_content system) or a project system's id (or galaxy).";
            builderMap.FocusSystem(n);
            return null;
        }

        // ---- hardpoints -----------------------------------------------------------------------------------------------

        static readonly string[] TypeWords = { "primary", "secondary", "turret", "engine" };

        static Vector3 GameToLocalDir(Vector3 g) => new Vector3(-g.x, g.y, g.z);

        static bool TryVector(JToken t, out Vector3 v)
        {
            v = default;
            if (!(t is JArray a) || a.Count != 3) return false;
            try { v = new Vector3((float)a[0], (float)a[1], (float)a[2]); return true; } catch (Exception) { return false; }
        }

        /// <summary>The direction (game frame) a point is looked for from: an angle's name or [x, y, z].</summary>
        static bool ParseSide(JToken t, out Vector3 gameDir, out string error)
        {
            error = null;
            gameDir = default;
            if (t == null) return false;
            if (TryVector(t, out gameDir) && gameDir.sqrMagnitude > 1e-6f) { gameDir.Normalize(); return true; }
            string name = t.Type == JTokenType.String ? ((string)t).ToLowerInvariant() : null;
            var view = ModelViewer.Angles.FirstOrDefault(a => a.name == name);
            if (view.name == null) { error = $"from: an angle ({string.Join(", ", ModelViewer.Angles.Select(a => a.name))}) or [x, y, z] in the ship's game frame (x the pilot's left)."; return false; }
            gameDir = GameToLocalDir(view.from).normalized;   // the angles are Unity-frame: mirror x into the game frame
            return true;
        }

        /// <summary>A mount by its index in the list or its tag ("P1", "E2": the type's letter and its count in the list).</summary>
        int MountIndex(JObject a, ProjectShip s)
        {
            if (a["index"] is JValue v && v.Type == JTokenType.Integer) return (int)v;
            string tagText = ((string)a["tag"] ?? (a["index"]?.Type == JTokenType.String ? (string)a["index"] : null))?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(tagText) || tagText.Length < 2) return -1;
            int type = Array.IndexOf(TypeShort, tagText.Substring(0, 1));
            if (type < 0 || !int.TryParse(tagText.Substring(1), out int n)) return -1;
            for (int i = 0, k = 0; i < s.Mounts.Count; i++)
                if (Mathf.Clamp(s.Mounts[i].slotType, 0, 3) == type && ++k == n) return i;
            return -1;
        }

        JArray MountList(ProjectShip s)
        {
            var list = new JArray();
            var counts = new int[4];
            for (int i = 0; i < s.Mounts.Count; i++)
            {
                var m = s.Mounts[i];
                int type = Mathf.Clamp(m.slotType, 0, 3);
                var o = new JObject { ["index"] = i, ["tag"] = TypeShort[type] + ++counts[type], ["type"] = TypeWords[type], ["position"] = new JArray(m.position_engine) };
                if (type == 2 && m.turretUp != null) o["turretUp"] = new JArray(m.turretUp);
                if (type == 2 && m.upsideDown) o["upsideDown"] = true;
                list.Add(o);
            }
            return list;
        }

        /// <summary>The hull's box in game units (the ship's frame).</summary>
        JObject HullJson()
        {
            var b = gizmo.LocalBounds;
            Vector3 lo = new Vector3(-b.max.x, b.min.y, b.min.z) / M, hi = new Vector3(-b.min.x, b.max.y, b.max.z) / M, ce = new Vector3(-b.center.x, b.center.y, b.center.z) / M;
            JArray V(Vector3 v) => new JArray(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
            return new JObject { ["min"] = V(lo), ["max"] = V(hi), ["centre"] = V(ce) };
        }

        string HullText()
        {
            var h = HullJson();
            return $"The hull spans {h["min"].ToString(Newtonsoft.Json.Formatting.None)} to {h["max"].ToString(Newtonsoft.Json.Formatting.None)}, centre {h["centre"].ToString(Newtonsoft.Json.Formatting.None)} (game units: x the pilot's left, y up, z the nose).";
        }

        /// <summary>The hull's surface on the line through 'gameAt' along 'gameDir' (coming from that side), world point and
        /// normal; false when the ray misses the hull or the model's meshes can't be read.</summary>
        bool HullFrom(Vector3 gameAt, Vector3 gameDir, out Vector3 point, out Vector3 normal)
        {
            point = normal = default;
            var at = boundTransform.TransformPoint(GameToLocalDir(gameAt) * M);
            var dir = boundTransform.TransformDirection(GameToLocalDir(gameDir)).normalized;
            float reach = Mathf.Max(gizmo.LocalBounds.size.magnitude * Mathf.Abs(boundTransform.lossyScale.x), 5f) * 2f;
            Physics.SyncTransforms();
            if (!Physics.Raycast(at + dir * reach, -dir, out var hit, reach * 2f, 1 << ModBuilderGizmo.PickLayer, QueryTriggerInteraction.Ignore)) return false;
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        internal override string ExternalHardpoint(JObject a, out JObject done)
        {
            done = null;
            if (kind != ModBuilder.Kind.Ships) return "Hardpoints are edited in the Ships kind: show_kind ships first.";
            var s = Current;
            if (s == null) return "The project has no ship (or none is selected).";
            if (boundTransform == null || gizmo.Ship == null) return "The ship is still being built: try again in a moment.";
            string op = ((string)a["op"] ?? "list").ToLowerInvariant();
            if (op == "list") { done = new JObject { ["ship"] = s.Ref, ["hull"] = HullJson(), ["hardpoints"] = MountList(s) }; return null; }
            if (op == "add")
            {
                int type = Array.IndexOf(TypeWords, ((string)a["type"] ?? "").ToLowerInvariant());
                if (type < 0) return "type: primary, secondary, turret or engine.";
                if (!ParseSide(a["from"] ?? "top", out var side, out string err)) return err ?? "from: an angle or [x, y, z].";
                var c = gizmo.LocalBounds.center;
                var hullCentre = new Vector3(-c.x, c.y, c.z) / M;
                var gameAt = TryVector(a["at"], out var at) ? at : TryVector(a["offset"], out var off) ? hullCentre + off : hullCentre;
                if (!HullFrom(gameAt, side, out var point, out var normal))
                    return $"No hull on that line through [{gameAt.x:0}, {gameAt.y:0}, {gameAt.z:0}] (try another 'at' / 'offset' or 'from'; view_model shows the hull). {HullText()}";
                AddMount(type, point, normal);
                done = new JObject { ["added"] = s.Mounts.Count - 1, ["hardpoints"] = MountList(s) };
                return null;
            }
            int i = MountIndex(a, s);
            if (i < 0 || i >= s.Mounts.Count) return "index (the list's index) or tag (\"P1\") of an existing hardpoint: op list shows them.";
            switch (op)
            {
                case "select":
                    gizmo.Selected = i; gizmo.Refresh(); SelectTab(1); BuildHardpointsPage();
                    break;
                case "snap":
                {
                    var m = s.Mounts[i];
                    var p = new Vector3(m.position_engine[0], m.position_engine[1], m.position_engine[2]);
                    var c = gizmo.LocalBounds.center;
                    var centre = new Vector3(-c.x, c.y, c.z) / M;
                    Vector3 side;
                    if (a["from"] != null) { if (!ParseSide(a["from"], out side, out string err)) return err; }
                    else side = (p - centre).sqrMagnitude > 1f ? (p - centre).normalized : Vector3.up;
                    if (!HullFrom(p, side, out var point, out var normal)) return "No hull on that line through the point: give 'from'.";
                    var g = gizmo.GameOf(point);
                    m.position_engine = new[] { g.x, g.y, g.z };
                    if (m.slotType == 2) m.SetTurretUp(gizmo.GameDirOf(normal), true);
                    gizmo.Selected = i;
                    gizmo.Refresh();
                    OnMountEdited(m.slotType);
                    BuildHardpointsPage();
                    break;
                }
                case "delete":
                    gizmo.Selected = i; DeleteSelected();
                    break;
                case "mirror":
                    if (s.Mounts[i].position_engine[0] == 0) return "The point is on the centre line: it has no mirror image.";
                    gizmo.Selected = i; Mirror();
                    break;
                default:
                    return "op: list, add, snap, select, mirror or delete.";
            }
            done = new JObject { ["done"] = op, ["hardpoints"] = MountList(s) };
            return null;
        }
    }
}
