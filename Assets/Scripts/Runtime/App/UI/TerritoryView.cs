// TerritoryView.cs
// Remake-only: the flight HUD's part of the factions' territory (NetFactions / NetFactionsClient), in a multiplayer session:
//   the toll: arriving at a station another faction holds (the local pilot in a faction of their own), once the launch /
//     arrival camera is over, the HUD's ChoiceWindow asks for the toll (NetState.Toll; Traffic.Ask). Paid: the station's
//     fighters spare this pilot for the visit (NetFactionsClient.TollStation, reset on the next orbit); refused: they
//     attack (NetOrbit's territory rule). Not in an arena;
//   the siege banner: in a besieged orbit, top centre: the two factions, the time to the start or the end, the control;
//   otherwise a squadmate's distress call (NetDistress): who, where, and how to answer (the actions menu's Help, the
//     squad window's Help or /assist); pulsing.
// Built in code with inline styles.

using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public sealed class TerritoryView : MonoBehaviour
    {
        Label banner;
        SpaceLevel level;
        bool tollAsked;

        /// <summary>The territory HUD on 'parent' (again after a UI reload).</summary>
        public static void Attach(GameObject host, VisualElement parent)
        {
            if (parent == null) return;
            var view = host.GetComponent<TerritoryView>();
            if (view == null) view = host.AddComponent<TerritoryView>();
            view.Build(parent);
        }

        void Build(VisualElement parent)
        {
            banner?.RemoveFromHierarchy();
            banner = new Label { pickingMode = PickingMode.Ignore };
            var s = banner.style;
            s.position = Position.Absolute;
            s.top = 64; s.left = 0; s.right = 0;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.fontSize = 18;
            s.color = new Color(1f, 0.45f, 0.35f);
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.unityTextOutlineColor = Color.black; s.unityTextOutlineWidth = 1f;
            s.display = DisplayStyle.None;
            parent.Add(banner);
        }

        void Update()
        {
            if (level == null)
            {
                level = FindAnyObjectByType<SpaceLevel>();
                if (level == null) return;
                tollAsked = false;
                // A new orbit: a toll paid elsewhere is over.
                if (level.Layout != null && NetFactionsClient.TollStation != level.Layout.stationIndex) NetFactionsClient.TollStation = -1;
            }
            if (!NetGame.Active || level.Layout == null || NetArenaClient.InMatch) { Show(null); return; }
            int station = level.NetOrbitId;
            AskToll(station);
            string text = BannerText(station) ?? DistressText();
            Show(text);
            if (text != null && banner != null) banner.style.opacity = NetFactionsClient.SiegeAt(station) != null ? 1f : 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
        }

        /// <summary>The squadmates calling for help (the nearest first is not known: in squad order), null = none.</summary>
        static string DistressText()
        {
            var me = NetPlayer.Local;
            if (me == null || NetSquad.LocalSquad == 0) return null;
            var db = NetGame.Db;
            foreach (var m in NetSquad.Members())
            {
                if (m == null || m == me || !m.Distress) continue;
                var st = db.Stations.Find(s => s.index == m.Station);
                string where = st == null ? "?" : m.Station == me.Station && me.InSpace ? Localization.Extra("mpDistressHere", "in this orbit") : $"{st.name} ({st.systemName})";
                return string.Format(Localization.Extra("mpDistressBanner", "⚠ {0} CALLS FOR HELP · {1} · actions menu: Help"), m.DisplayName, where);
            }
            return null;
        }

        void AskToll(int station)
        {
            if (tollAsked || !level.LaunchCameraOver || level.Traffic == null || NetPlayer.Local == null) return;
            int toll = NetState.Instance != null ? NetState.Instance.Toll : 0;
            string tag = NetPlayer.Local.FactionTag;
            if (toll <= 0 || NetFactionsClient.Relation(station, tag, NetFactionsClient.TollStation) >= 0) { tollAsked = true; return; }
            tollAsked = true;
            NetFactionsClient.Owner(station, out string owner, out string name);
            string text = string.Format(Localization.Extra("mpTollAsk",
                "This station belongs to [{0}] {1}. Pay {2} to pass in peace? Refuse, and its fighters attack you."), owner, name, ItemInfo.Credits(toll));
            level.Traffic.Ask(text, Localization.Get(134), Localization.Get(135), () =>
            {
                if (!NetFactionsClient.PayToll(station))
                    level.Traffic.Ask(Localization.Get(203).Replace("#C", ItemInfo.Credits(toll - Session.Credits)), null, null, null, null);
            }, null);
        }

        static string BannerText(int station)
        {
            var s = NetFactionsClient.SiegeAt(station);
            if (s == null) return null;
            int left = Mathf.Max(0, Mathf.CeilToInt(s.secondsLeft - (Time.unscaledTime - s.receivedAt)));
            string time = $"{left / 60}:{left % 60:00}";
            return s.started
                ? string.Format(Localization.Extra("mpSiegeBanner", "SIEGE  [{0}] against [{1}]  ·  {2}% taken  ·  {3} left"), s.attacker, s.defender, s.control, time)
                : string.Format(Localization.Extra("mpSiegeBannerSoon", "SIEGE  [{0}] against [{1}]  ·  starts in {2}"), s.attacker, s.defender, time);
        }

        void Show(string text)
        {
            if (banner == null) return;
            banner.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (text != null) banner.text = text;
        }

        void OnDestroy() => banner?.RemoveFromHierarchy();
    }
}
