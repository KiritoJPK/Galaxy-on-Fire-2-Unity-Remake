// UiHooks.cs
// What the layers below the screens (GoF2.Space, GoF2.Net: the event graphs, the multiplayer clients) need from them
// (GoF2.App): the dialogue window, the flight HUD's message line, the station menu's toasts, dialogs, credits and cargo,
// the multiplayer window's trade tab. AppHooksInstaller fills these when the game starts; until then nothing shows.

using System;
using System.Collections.Generic;
using GoF2Remake.Data;

namespace GoF2Remake.UI
{
    /// <summary>A page of a conversation in the dialogue window (DialogueView).</summary>
    public struct DialogueLine
    {
        public int speaker;
        public string text;
        /// <summary>The text table id of a story page (-1 / 0 for built texts).</summary>
        public int textId;
        public string voice;
        /// <summary>A bar agent instead of a story speaker (freelance briefings / results): its name and portrait parts.</summary>
        public string agentName;
        public int[] agentPortrait;
        /// <summary>A mod's character ("mod_id:character_id", Modding.ModCharacters) instead: its portrait.</summary>
        public string character;
        public static DialogueLine From(DialoguePage p) => new DialogueLine { speaker = p.speaker, text = Localization.Get(p.text), textId = p.text, voice = p.voice };
    }

    /// <summary>A scene's dialogue window (DialogueView).</summary>
    public interface IDialogueWindow
    {
        /// <summary>It is on screen (its panel lives: a scene change leaves the old one behind).</summary>
        bool Usable { get; }
        bool IsOpen { get; }
        void Show(List<DialogueLine> list, Action<bool> onClosed);
    }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class UiHooks
    {
        /// <summary>The newest dialogue window (DialogueView.Latest), null = none.</summary>
        public static Func<IDialogueWindow> LatestDialogueProvider;
        /// <summary>A line on the flight HUD's message line; false = no flight HUD in this scene.</summary>
        public static Func<string, bool> FlightMessage;
        /// <summary>A toast in the station menu; false = no station menu in this scene.</summary>
        public static Func<string, bool> StationToast;
        /// <summary>The station menu's dialog (text, on Yes, info = only OK); false = no station menu in this scene.</summary>
        public static Func<string, Action, bool, bool> StationDialog;
        /// <summary>The station menu shows the credits / cargo again.</summary>
        public static Action RefreshStationCredits, StationCargoChanged;
        /// <summary>The multiplayer window's trade tab (docked).</summary>
        public static Action OpenTrade;

        public static IDialogueWindow LatestDialogue => LatestDialogueProvider?.Invoke();
    }
}
