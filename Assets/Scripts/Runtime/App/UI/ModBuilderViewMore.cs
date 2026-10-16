// ModBuilderViewMore.cs
// The Mod Builder's less common item and ship fields (part of ModBuilderView; Modding/README.md "Items" and "Ships"): an
// item's cheapest / dearest systems, Vossk-only, the station that always stocks it, its condition, its attributes by number
// and its Default Economy prices; a ship's condition, the ordinary dealers and the lounge sellers that sell it, the turret
// items its NPC ships carry, its Default Economy price and its engine flame. Everything else is in Edit as JSON.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        static int? JInt(JToken t) => t is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (int)Math.Round((double)v) : (int?)null;
        static float? JFloat(JToken t) => t is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (float)v : (float?)null;

        static readonly string[] RaceChoices = { "Any", "Terran", "Vossk", "Nivelian", "Midorian" };
        static int RaceIndex(int race) => race < 0 || race > 3 ? 0 : race + 1;

        /// <summary>An item's other shop fields.</summary>
        void BuildItemMore(ProjectItem it, ItemData original)
        {
            Heading(itemPage, T("mbItemMore", "Where it sells"));
            RefField(itemPage, T("mbCheapestSystem", "Cheapest in the system"), RefKind.System, ProjectEntry.RefText(it.Get("lowestPriceSystem")),
                     v => { it.Set("lowestPriceSystem", v == null ? null : ProjectEntry.RefToken(v)); ItemEdited(); BuildItemPage(); },
                     original != null ? string.Format(T("mbAsBase", "as its base ({0})"), original.lowestPriceSystem) : null);
            RefField(itemPage, T("mbDearestSystem", "Dearest in the system"), RefKind.System, ProjectEntry.RefText(it.Get("highestPriceSystem")),
                     v => { it.Set("highestPriceSystem", v == null ? null : ProjectEntry.RefToken(v)); ItemEdited(); BuildItemPage(); },
                     original != null ? string.Format(T("mbAsBase", "as its base ({0})"), original.highestPriceSystem) : null);
            RefField(itemPage, T("mbAlwaysSoldAt", "Always in stock at"), RefKind.Station, ProjectEntry.RefText(it.Get("alwaysSoldAt")),
                     v => { it.Set("alwaysSoldAt", v == null ? null : ProjectEntry.RefToken(v)); ItemEdited(); BuildItemPage(); }, T("mbNoStation", "no station"));
            bool vossk = it.Get("vosskOnly") is JValue vo && vo.Type == JTokenType.Boolean && (bool)vo;
            FlagButton(Row(itemPage), T("mbVosskOnly", "Only Vossk systems sell it"), vossk, v => { it.Set("vosskOnly", v ? (JToken)true : null); ItemEdited(); });
            ConditionField(itemPage, T("mbAvailable", "Sold (and dropped) only when"), (string)it.Get("available"), v => { it.Set("available", v); ItemEdited(); });

            // Default Economy: the prices when the player picked the PC / Mac economy (the rest stays the item's).
            var de = it.Get("defaultEconomy") as JObject;
            int dMin = JInt(de?["minPrice"]) ?? 0, dMax = JInt(de?["maxPrice"]) ?? 0;
            var row = Row(itemPage, "mbv-xyz");
            void SetDe(string k, int v)
            {
                var o = it.Get("defaultEconomy") as JObject ?? new JObject();
                if (v <= 0) o.Remove(k); else o[k] = v;
                it.Set("defaultEconomy", o.HasValues ? o : null);
                ItemEdited();
            }
            Int(row, T("mbDefaultMin", "Default Economy: lowest"), dMin, 0, 999999999, v => SetDe("minPrice", v));
            Int(row, T("mbDefaultMax", "highest (0 = as above)"), dMax, 0, 999999999, v => SetDe("maxPrice", v));

            // Raw attributes: the original game's attribute numbers ({ "9": 14 }).
            Heading(itemPage, T("mbAttributes", "Attributes by number"));
            var attrs = it.Get("attributes") as JObject;
            if (attrs != null)
                foreach (var p in attrs.Properties().ToList())
                {
                    string key = p.Name;
                    var arow = Row(itemPage, "mbv-xyz");
                    Int(arow, string.Format(T("mbAttrN", "Attribute {0}"), key), JInt(p.Value) ?? 0, int.MinValue / 2, int.MaxValue / 2, v => { ((JObject)it.Get("attributes"))[key] = v; ItemEdited(); });
                    Act(arow, "✕", () => { var o = (JObject)it.Get("attributes"); o.Remove(key); it.Set("attributes", o.HasValues ? o : null); ItemEdited(); BuildItemPage(); }, "mbv-step");
                }
            Act(itemPage, T("mbAddAttribute", "+ Attribute"), () => AskText(T("mbAddAttribute", "+ Attribute"), T("mbAttrNumber", "The attribute's number (reference.md lists them)"), "", text =>
            {
                if (!int.TryParse(text.Trim(), out int n) || n < 0) { Status(T("mbAttrBad", "An attribute is a number."), true); return; }
                var o = it.Get("attributes") as JObject ?? new JObject();
                o[n.ToString(CultureInfo.InvariantCulture)] = original != null ? original.Attr(n) : 0;
                it.Set("attributes", o);
                ItemEdited();
                BuildItemPage();
            }, 6), "mbv-add");
            Note(itemPage, T("mbAttributesNote", "For the numbers the stats above have no name for (Modding/ai/gof2-modding/reference.md lists every attribute)."));
        }

        /// <summary>A ship's selling and NPC fields.</summary>
        void BuildShipMore(ProjectShip s)
        {
            Heading(ship, T("mbShipSelling", "Where it sells"));
            ConditionField(ship, T("mbAvailable", "Sold only when"), (string)s.Get("available"), v => { s.Set("available", v); MarkDirty(); });
            Int(ship, T("mbPriceDefault", "Price in the Default Economy (0 = the same)"), JInt(s.Get("priceDefault")) ?? 0, 0, 999999999,
                v => { s.Set("priceDefault", v > 0 ? (JToken)v : null); MarkDirty(); });

            // The ordinary dealers.
            var dealer = s.Get("dealer") as JObject;
            FlagButton(Row(ship), T("mbDealerSells", "Ship dealers stock it"), dealer != null, v =>
            {
                s.Set("dealer", v ? new JObject { ["chance"] = 2, ["systemRace"] = -1, ["minTechLevel"] = 0 } : null);
                MarkDirty();
            }, () => BuildShipPage());
            if (dealer != null)
            {
                var box = Add(ship, new VisualElement(), "mbv-page-box");
                Float(box, T("mbChancePct", "Chance (%)"), JFloat(dealer["chance"]) ?? 2f, 0f, 100f, v => { dealer["chance"] = Math.Round(v, 2); MarkDirty(); });
                Segments(box, T("mbInSystemsOf", "In systems of"), RaceChoices.Select(r => T("mbRace" + r, r)).ToArray(), RaceIndex(JInt(dealer["systemRace"]) ?? -1),
                         n => { dealer["systemRace"] = n - 1; MarkDirty(); BuildShipPage(); });
                Int(box, T("mbMinTech", "At stations of tech level at least"), JInt(dealer["minTechLevel"]) ?? 0, 0, 10, v => { dealer["minTechLevel"] = v; MarkDirty(); });
            }

            // A lounge visitor sells it.
            var lounge = s.Get("lounge") as JObject;
            FlagButton(Row(ship), T("mbLoungeSells", "A lounge visitor sells it"), lounge != null, v =>
            {
                s.Set("lounge", v ? new JObject { ["systemRace"] = -1, ["minCampaign"] = 0, ["minRank"] = 0, ["chance"] = 10 } : null);
                MarkDirty();
            }, () => BuildShipPage());
            if (lounge != null)
            {
                var box = Add(ship, new VisualElement(), "mbv-page-box");
                Int(box, T("mbChancePct", "Chance (%)"), JInt(lounge["chance"]) ?? 10, 0, 100, v => { lounge["chance"] = v; MarkDirty(); });
                Segments(box, T("mbInSystemsOf", "In systems of"), RaceChoices.Select(r => T("mbRace" + r, r)).ToArray(), RaceIndex(JInt(lounge["systemRace"]) ?? -1),
                         n => { lounge["systemRace"] = n - 1; MarkDirty(); BuildShipPage(); });
                Int(box, T("mbMinCampaign", "From story step"), JInt(lounge["minCampaign"]) ?? 0, 0, 162, v => { lounge["minCampaign"] = v; MarkDirty(); });
                Int(box, T("mbMinRank", "In free play from rank"), JInt(lounge["minRank"]) ?? 0, 0, 50, v => { lounge["minRank"] = v; MarkDirty(); });
            }

            // The turret items NPC ships of this type carry.
            Heading(ship, T("mbNpcTurrets", "NPC turrets"));
            var turrets = (s.Get("npcTurrets") as JArray)?.Select(ProjectEntry.RefText).Where(x => x != null).ToList() ?? new List<string>();
            for (int i = 0; i < turrets.Count; i++)
            {
                int n = i;
                var trow = Row(ship);
                Add(trow, new Label($"{n + 1}. {RefLabel(RefKind.Item, turrets[n], turrets[n])}"), "mbv-inline-label");
                Act(trow, "✕", () => { turrets.RemoveAt(n); s.Set("npcTurrets", turrets.Count > 0 ? new JArray(turrets.Select(ProjectEntry.RefToken)) : null); MarkDirty(); BuildShipPage(); }, "mbv-step");
            }
            Act(ship, T("mbAddNpcTurret", "+ Turret for NPC ships"), () => PickRef(RefKind.Item, T("mbNpcTurrets", "NPC turrets"), e =>
            {
                if (e == null) return;
                turrets.Add(e.value);
                s.Set("npcTurrets", new JArray(turrets.Select(ProjectEntry.RefToken)));
                MarkDirty();
                BuildShipPage();
            }, e => int.TryParse(e.value, out int idx) ? Database.Shared.Item(idx)?.categoryId == 8 : true), "mbv-add");
            Note(ship, T("mbNpcTurretsNote", "The n-th turret item goes on the n-th turret mount of the NPC ships of this type: they aim and fire by themselves, and show parked in hangars."));

            // The engine flame.
            Heading(ship, T("mbEngineFlame", "Engine flame"));
            Float(ship, T("mbGlowRadius", "Flame size (game units; 24 by default)"), JFloat(s.Get("engineGlowRadius")) ?? 24f, 1f, 2000f,
                  v => { s.Set("engineGlowRadius", Mathf.Approximately(v, 24f) ? null : (JToken)Math.Round(v, 2)); MarkDirty(); RequestReload(true); });
            var colour = s.Get("engineGlowColor") as JArray;
            FlagButton(Row(ship), T("mbGlowColour", "Its own flame colour"), colour != null, v =>
            {
                s.Set("engineGlowColor", v ? new JArray(0.4, 0.7, 1.0) : null);
                MarkDirty();
                RequestReload(true);
            }, () => BuildShipPage());
            if (colour != null && colour.Count >= 3)
            {
                var crow = Row(ship, "mbv-xyz");
                for (int k = 0; k < 3; k++)
                {
                    int c = k;
                    Float(crow, "RGB".Substring(c, 1), (float)colour[c], 0f, 1f, v => { colour[c] = Math.Round(v, 3); MarkDirty(); RequestReload(true); });
                }
            }
            Note(ship, T("mbMoreJsonNote", "Per-mesh materials, throttle glows and extra glows: Edit as JSON (at the top)."));
        }
    }
}
