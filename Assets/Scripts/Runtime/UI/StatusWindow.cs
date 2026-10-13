// StatusWindow.cs
// The Status window (StatusWindow 0x18345c / reInit 0x183784 / draw 0x183a64 / getMedalHintText 0x185da4; Reference/
// research/lounge_ui.md 6), station button 169 "Status". HD layout, both columns side by side:
//   left   1597 Keith T. Maxwell (credits, 321 Level N, playing time hh:mm), the ship (icon, name, 569 Fire power, 570
//          Defense), 576 Reputation (Terran / Vossk and Nivelian / Midorian bars, the marker toward the liked race, the
//          emblem tinted while that race is hostile), 577 Statistics (568 missions, 176 kills, 552 asteroids, 559 cargo
//          salvaged, 557 stations, 3235 battleships | 564 jumpgates, 558 goods produced, 560 ore, 561 cores, 567 wingmen)
//   right  168 Medals: 45 plates in three columns, coloured by grade (Achievements); an earned medal shows its hint
//          (1552 + i, # = the threshold of its grade)
// Fire power: the original's Ship::getFirePower formula was not recovered; the remake shows the mounted primaries' damage
// per second. Medal images: GoF2 > Build > HUD Images (GoF2Hud/medal_*). Plain class driven by
// StationMenu.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class StatusWindow
    {
        readonly StationMenu menu;
        readonly StationLevel level;
        readonly VisualElement root, left, grid;
        readonly Label hint;
        readonly Button close;
        readonly List<Button> medalButtons = new List<Button>();
        int selectedMedal = -1;

        static string T(int id) => Localization.Get(id);

        public StatusWindow(StationMenu menu, StationLevel level, VisualElement root)
        {
            this.menu = menu;
            this.level = level;
            this.root = root;
            left = root.Q("statusLeft");
            grid = root.Q("medalGrid");
            hint = root.Q<Label>("medalHint");
            close = root.Q<Button>("statusClose");
            close.text = Localization.Extra("hudBack", "BACK");
            close.RegisterCallback<PointerDownEvent>(_ => menu.PlayPush(), TrickleDown.TrickleDown);
            close.clicked += () => { menu.PlayRelease(); Close(); };
            root.Q<Label>("statusTitle").text = T(169).ToUpperInvariant();
            root.Q<Label>("medalsTitle").text = T(168).ToUpperInvariant();
            scroll = root.Q<ScrollView>("medalScroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        ScrollView scroll;

        public bool IsOpen => root.ClassListContains("status-open");

        public void Open()
        {
            root.AddToClassList("status-open");
            Build();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.RemoveFromClassList("status-open");
            menu.OnStatusClosed();
        }

        void Build()
        {
            var db = level.Database;
            left.Clear();
            // Pilot and ship.
            Plate(left, T(1597));
            var row = Row(left);
            var pilot = Box(row, false);
            pilot.AddToClassList("status-pilot");
            var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            portrait.AddToClassList("portrait");
            pilot.Add(portrait);
            Portrait.ShowSpeaker(portrait, 0, false);
            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("status-pilot-lines");
            pilot.Add(lines);
            Line(lines, ItemInfo.Credits(Session.Credits), true);
            Line(lines, $"{T(321)} {Session.Rank}", false);
            int minutes = (int)(Session.PlaySeconds / 60f);
            Line(lines, $"{minutes / 60:00}:{minutes % 60:00}", false);

            var shipBox = Box(row, true);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("status-ship-icon");
            icon.style.backgroundImage = new StyleBackground(ItemInfo.ShipIcon(Session.ShipIndex));
            // Remake: the ship's name and stats to the right of its icon.
            shipBox.AddToClassList("status-ship-box");
            shipBox.Add(icon);
            var shipLines = new VisualElement { pickingMode = PickingMode.Ignore };
            shipLines.AddToClassList("status-ship-lines");
            shipBox.Add(shipLines);
            Line(shipLines, ItemInfo.ShipName(Session.ShipIndex), true);
            StatLine(shipLines, T(569), ((int)Shop.FirePower(db)).ToString());
            StatLine(shipLines, T(570), Shop.CombinedHp(db).ToString());

            // Reputation.
            Plate(left, T(576));
            var rep = Row(left);
            Reputation(Box(rep, false), 0, 1, StandingShown(0));
            Reputation(Box(rep, true), 2, 3, StandingShown(1));

            // Statistics.
            Plate(left, T(577));
            var stats = Row(left);
            var a = Box(stats, false);
            StatLine(a, T(568), Session.FreelanceCompleted.ToString());
            StatLine(a, T(176), Session.Kills.ToString());
            StatLine(a, T(552), Session.AsteroidsDestroyed.ToString());
            StatLine(a, T(559), Session.CratesSalvaged.ToString());
            StatLine(a, T(557), Session.VisitedStations.Count.ToString());
            StatLine(a, T(3235), Session.BattleshipsDestroyed.ToString());
            var b = Box(stats, true);
            StatLine(b, T(564), Session.JumpgatesUsed.ToString());
            StatLine(b, T(558), Session.GoodsProduced.ToString());
            StatLine(b, T(560), Session.OreMined.ToString());
            StatLine(b, T(561), Session.CoresMined.ToString());
            StatLine(b, T(567), Session.WingmenHired.ToString());

            // Medals (none in multiplayer: the column is hidden).
            grid.Clear();
            medalButtons.Clear();
            bool medals = !GoF2Remake.Multiplayer.NetGame.Active;
            var column = root.Q(className: "status-right");
            if (column != null) column.style.display = medals ? DisplayStyle.Flex : DisplayStyle.None;
            if (!medals) { hint.text = ""; menu.Focus(close); return; }
            for (int i = 0; i < Achievements.Count; i++)
            {
                int medal = i;
                int grade = Achievements.Grade(i);
                bool elite = i >= Achievements.BaseCount;
                var m = new Button();
                m.AddToClassList("medal");
                if (grade > 0) m.AddToClassList("medal--earned");
                // TouchButton::draw style 4: the plate by grade, the symbol centred at (114, 41) tinted by grade, the
                // pressed overlay 2412 on the selected medal, the name 1507 + i in white under the plate.
                var plate = MedalPlate(i, grade);
                var pressed = new VisualElement { pickingMode = PickingMode.Ignore };
                pressed.AddToClassList("medal-pressed");
                pressed.style.backgroundImage = Hud("medal_pressed");
                plate.Add(pressed);
                m.Add(plate);
                var n = new Label(T(1507 + i)) { pickingMode = PickingMode.Ignore };
                n.AddToClassList("medal-name");
                n.AddToClassList("gof-semibold");
                m.Add(n);
                // TouchButton enabled by level: earned medals react, the elite ones (36-44) always (their hint then uses
                // the grade-1 threshold).
                bool reacts = grade > 0 || elite;
                m.clicked += () => { if (reacts) { menu.PlayRelease(); ShowHint(medal); } };
                // The keys / a controller move the focus through the grid: the list follows it (#45: it never scrolled).
                m.RegisterCallback<FocusInEvent>(_ => { if (reacts) ShowHint(medal); scroll?.ScrollTo(m); });
                grid.Add(m);
                medalButtons.Add(m);
            }
            hint.text = T(647);
            menu.Focus(close);
        }

        /// <summary>TouchButton::draw style 4 / ChoiceWindow::setMedal: the plate by grade with the medal's symbol tinted by
        /// grade (also the "New medal!" window's picture).</summary>
        public static VisualElement MedalPlate(int medal, int grade)
        {
            bool elite = medal >= Achievements.BaseCount;
            var plate = new VisualElement { pickingMode = PickingMode.Ignore };
            plate.AddToClassList("medal-plate");
            plate.style.backgroundImage = Hud(elite ? (grade == 1 ? "medal_plate_elite_gold" : grade == 0 ? "medal_plate_elite_none" : grade == 2 ? "medal_plate_silver" : "medal_plate_bronze")
                                                    : grade == 1 ? "medal_plate_gold" : grade == 2 ? "medal_plate_silver" : grade == 3 ? "medal_plate_bronze" : "medal_plate_none");
            var symbol = new VisualElement { pickingMode = PickingMode.Ignore };
            symbol.AddToClassList("medal-symbol");
            symbol.style.backgroundImage = Hud($"medal_{medal:00}");
            symbol.style.unityBackgroundImageTintColor = MedalTint(elite, grade);
            plate.Add(symbol);
            return plate;
        }

        /// <summary>Status::getStandingRate -> Standing::getStanding 0x14283a: with a signature mounted the axis of its race reads
        /// +-100 (100 toward the signature's race), the other 70; else the stored value.</summary>
        static int StandingShown(int axis)
        {
            int sig = Standing.SignatureRace;
            if (sig < 0 || sig > 3) return Session.Standing[axis];
            if (axis == 0) return sig == 0 ? 100 : sig == 1 ? -100 : 70;
            return sig == 2 ? 100 : sig == 3 ? -100 : 70;
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> hudImages = new System.Collections.Generic.Dictionary<string, Texture2D>();

        static StyleBackground Hud(string name)
        {
            if (!hudImages.TryGetValue(name, out var t)) hudImages[name] = t = Resources.Load<Texture2D>("GoF2Hud/" + name);
            return t != null ? new StyleBackground(t) : new StyleBackground(StyleKeyword.None);
        }

        /// <summary>DAT_00252060 (base: none 0x2198ff2f, gold 0xfad10eff, silver white, bronze 0xce8258ff) and DAT_00252050
        /// (elite: none 0xfa792160, gold 0xfa7921ff), RGBA.</summary>
        internal static Color MedalTint(bool elite, int grade)
        {
            uint c = elite ? (grade == 1 ? 0xfa7921ffu : grade == 0 ? 0xfa792160u : grade == 2 ? 0xffffffffu : 0xce8258ffu)
                           : grade == 1 ? 0xfad10effu : grade == 2 ? 0xffffffffu : grade == 3 ? 0xce8258ffu : 0x2198ff2fu;
            return new Color32((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
        }

        /// <summary>The medal selected and in view (the station's new-medal toast was tapped).</summary>
        public void SelectMedal(int medal)
        {
            if (medal < 0 || medal >= medalButtons.Count) return;
            ShowHint(medal);
            var button = medalButtons[medal];
            button.schedule.Execute(() =>
            {
                for (var v = button.parent; v != null; v = v.parent)
                    if (v is ScrollView scroll) { scroll.ScrollTo(button); break; }
                if (InputMode.Current != InputKind.Touch) button.Focus();
            });
        }

        void ShowHint(int medal)
        {
            selectedMedal = medal;
            for (int i = 0; i < medalButtons.Count; i++) medalButtons[i].EnableInClassList("medal--selected", i == medal);
            hint.text = $"{T(1507 + medal)}\n{Achievements.Hint(medal)}";
        }


        // ---- building blocks -----------------------------------------------------------------------------

        static void Plate(VisualElement parent, string text)
        {
            var l = new Label(text.ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("status-plate");
            l.AddToClassList("gof-semibold");
            parent.Add(l);
        }

        static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.AddToClassList("status-row");
            parent.Add(r);
            return r;
        }

        static VisualElement Box(VisualElement parent, bool last)
        {
            var b = new VisualElement { pickingMode = PickingMode.Ignore };
            b.AddToClassList("status-box");
            if (last) b.AddToClassList("status-box--last");
            parent.Add(b);
            return b;
        }

        static void Line(VisualElement parent, string text, bool big)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(big ? "status-big" : "status-line");
            if (big) l.AddToClassList("gof-semibold");
            parent.Add(l);
        }

        static void StatLine(VisualElement parent, string label, string value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("stat-line");
            var a = new Label(label) { pickingMode = PickingMode.Ignore };
            a.AddToClassList("stat-line-label");
            var b = new Label(value) { pickingMode = PickingMode.Ignore };
            b.AddToClassList("stat-line-value");
            b.AddToClassList("gof-semibold");
            row.Add(a);
            row.Add(b);
            parent.Add(row);
        }

        /// <summary>A reputation bar: rate = standing / 100 toward the first race (+) or the second (-).</summary>
        static void Reputation(VisualElement box, int raceA, int raceB, int standing)
        {
            float rate = Mathf.Clamp(standing / 100f, -1f, 1f);
            var bar = Resources.Load<Texture2D>("GoF2Races/race_status_bar");
            if (bar != null) { ReputationBar(box, raceA, raceB, rate, bar); return; }
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("rep-row");
            row.Add(Emblem(raceA));
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("rep-track");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("rep-fill");
            // Drawn from the centre toward the favoured side (the left race when positive).
            fill.style.left = Length.Percent(rate > 0f ? 50f - rate * 50f : 50f);
            fill.style.width = Length.Percent(Mathf.Abs(rate) * 50f);
            track.Add(fill);
            var marker = new VisualElement { pickingMode = PickingMode.Ignore };
            marker.AddToClassList("rep-marker");
            marker.style.left = Length.Percent(50f - rate * 50f);
            track.Add(marker);
            row.Add(track);
            row.Add(Emblem(raceB));
            box.Add(row);
            var names = new VisualElement { pickingMode = PickingMode.Ignore };
            names.AddToClassList("rep-names");
            var na = new Label(T(406 + raceA)) { pickingMode = PickingMode.Ignore };
            na.AddToClassList("rep-name");
            var nb = new Label(T(406 + raceB)) { pickingMode = PickingMode.Ignore };
            nb.AddToClassList("rep-name");
            names.Add(na);
            names.Add(nb);
            box.Add(names);
        }

        /// <summary>Remake: the bar width in panel units (the image's 164 x 28 at x1.5).</summary>
        const float RepBarWidth = 246f;

        /// <summary>Remake: the race status bar image (GoF2Races/race_status_bar). Its whole width is -100..+100, the centre
        /// neutral, the dividers before the end caps at 71 (Standing: hostile past them). The empty bar
        /// (GoF2Races/race_status_bar_empty) underneath; the lit image over it from the centre to the marker (brighter toward
        /// it, the image's own gradient). Each race's emblem against its end of
        /// the bar, its name centred under it.</summary>
        static void ReputationBar(VisualElement box, int raceA, int raceB, float rate, Texture2D bar)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("rep-row");
            row.AddToClassList("rep-row--bar");
            row.Add(RaceSide(raceA));
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("rep-bar");
            var empty = Resources.Load<Texture2D>("GoF2Races/race_status_bar_empty");
            track.style.backgroundImage = new StyleBackground(empty != null ? empty : bar);
            track.EnableInClassList("rep-bar--dimmed", empty == null);   // no empty bar: the lit one dimmed
            float markerPct = 50f - rate * 50f;   // toward the left race when positive
            float from = Mathf.Min(50f, markerPct), to = Mathf.Max(50f, markerPct);
            var lit = new VisualElement { pickingMode = PickingMode.Ignore };
            lit.AddToClassList("rep-bar-lit");
            lit.style.left = Length.Percent(from);
            lit.style.width = Length.Percent(to - from);
            var litImage = new VisualElement { pickingMode = PickingMode.Ignore };
            litImage.AddToClassList("rep-bar-lit-image");
            litImage.style.backgroundImage = new StyleBackground(bar);
            litImage.style.left = -RepBarWidth * from / 100f;   // the image stays put, the window over it moves
            lit.Add(litImage);
            track.Add(lit);
            var marker = new VisualElement { pickingMode = PickingMode.Ignore };
            marker.AddToClassList("rep-bar-marker");
            marker.style.left = Length.Percent(markerPct);
            track.Add(marker);
            row.Add(track);
            row.Add(RaceSide(raceB));
            box.Add(row);
        }

        /// <summary>Remake: a race's emblem with its name centred under it.</summary>
        static VisualElement RaceSide(int race)
        {
            var side = new VisualElement { pickingMode = PickingMode.Ignore };
            side.AddToClassList("rep-side");
            side.Add(Emblem(race));
            var name = new Label(T(406 + race)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("rep-name");
            name.AddToClassList("rep-side-name");
            side.Add(name);
            return side;
        }

        /// <summary>Remake: a race's name in the GoF2Races images (terran_icon, vossk_allied ...).</summary>
        public static string RaceFile(int race) => race switch { 0 => "terran", 1 => "vossk", 2 => "nivelian", _ => "midorian" };

        /// <summary>Remake: GoF2Races/<race>_allied, _neutral or _enemy for the player's current standing with that race
        /// (shared by the status window and the star map); null when the image is missing or the race isn't one of the four.</summary>
        public static Texture2D StandingIcon(int race)
        {
            if (race < 0 || race > 3) return null;
            string state = Standing.IsEnemy(race) ? "enemy" : Standing.IsFriend(race) ? "allied" : "neutral";
            return Resources.Load<Texture2D>($"GoF2Races/{RaceFile(race)}_{state}");
        }

        /// <summary>Remake: the race's emblem in its standing's colours: GoF2Races/<race>_allied (green, a friend), _enemy (red,
        /// hostile) or _neutral (blue); the radar's emblem tinted red while hostile if those images are missing.</summary>
        static VisualElement Emblem(int race)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("rep-emblem");
            var tex = StandingIcon(race);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            else
            {
                tex = Resources.Load<Texture2D>($"GoF2Hud/race_{race}");
                if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
                e.EnableInClassList("rep-emblem--hostile", Standing.IsEnemy(race));
            }
            return e;
        }

        public VisualElement[] NavItems()
        {
            var l = new List<VisualElement>(medalButtons) { close };
            return l.ToArray();
        }
    }
}
