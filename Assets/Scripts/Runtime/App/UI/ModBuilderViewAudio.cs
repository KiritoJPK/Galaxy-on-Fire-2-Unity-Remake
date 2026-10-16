// ModBuilderViewAudio.cs
// The Mod Builder's Audio kind (part of ModBuilderView), in the Space Lounge where the music plays: the project's audio files
// (Modding/README.md "Music", "Sound effects", "Voice-over"). Left: the three groups. Right, for the group picked: Music
// (a game track replaced: music/<track>.ogg; or a new track by name, for the quests' Music step, systems' and stations'
// music), Sound effects (a game sound replaced, sounds/<name>, from the list in Resources/GoF2ModBuilder/SoundNames.txt),
// Voice lines (voices/<clip>, English or German: voices/de/; the quests' conversations name them). Each file plays on ▶
// (ModAudio, from the project's folder), is replaced or removed. The game reads them when the project is read again.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        VisualElement audioList, audioPage;
        int audioGroup;
        AudioSource audioPreview;
        string playing;

        static readonly (string key, string english)[] AudioGroups = { ("mbMusic", "Music"), ("mbSounds", "Sound effects"), ("mbVoices", "Voice lines") };

        void BuildAudioPanels(VisualElement left, VisualElement right)
        {
            audioList = ListPanel(left, T("mbAudio", "Audio"));
            audioPage = PagePanel(right);
        }

        void RebuildAudio()
        {
            audioList.Clear();
            if (Project == null) return;
            string[] folders = { ModProject.MusicFolder, ModProject.SoundsFolder, ModProject.VoicesFolder };
            for (int i = 0; i < AudioGroups.Length; i++)
            {
                int n = i;
                int count = Project.AudioFiles(folders[i]).Count + (i == 2 ? Project.AudioFiles(ModProject.VoicesFolder + "/de").Count + Project.AudioFiles(ModProject.VoicesFolder + "/en").Count : 0);
                ListRow(audioList, T(AudioGroups[i].key, AudioGroups[i].english), string.Format(T("mbFilesN", "{0} files"), count), i == audioGroup,
                        () => { audioGroup = n; RebuildAudio(); BuildAudioPage(); }, null, null);
            }
        }

        void AudioChanged()
        {
            RequestReload(false);
            RebuildAudio();
            BuildAudioPage();
        }

        void BuildAudioPage()
        {
            audioPage.Clear();
            if (Project == null) return;
            switch (audioGroup)
            {
                case 0:
                    Heading(audioPage, T("mbMusic", "Music"));
                    Note(audioPage, T("mbMusicNote", "A file named like one of the game's tracks plays instead of it everywhere. Any other name is a new track: quests, systems and stations can play it."));
                    var mrow = Row(audioPage);
                    Act(mrow, T("mbReplaceTrack", "+ Replace a game track"), () =>
                    {
                        var choices = ModRefs.GameMusic.Select(t => (t, (Texture2D)null, (Action)(() => PickAudio(T("mbPickMusic", "Choose the music file"), path =>
                        { Project.SetAudio(ModProject.MusicFolder, t, path); AudioChanged(); })))).ToList();
                        ShowPicker(T("mbReplaceTrack", "+ Replace a game track"), choices, true);
                    }, "mbv-add");
                    Act(mrow, T("mbNewTrack", "+ New track"), () => AskText(T("mbNewTrack", "+ New track"), T("mbTrackName", "Its name (a-z, 0-9, _)"), "", name =>
                    {
                        string id = ModProject.Slug(name);
                        PickAudio(T("mbPickMusic", "Choose the music file"), path => { Project.SetAudio(ModProject.MusicFolder, id, path); AudioChanged(); });
                    }), "mbv-add");
                    FileRows(ModProject.MusicFolder, n => ModRefs.GameMusic.Contains(n, StringComparer.OrdinalIgnoreCase) ? T("mbReplaces", "replaces the game's") : T("mbNewTrackSub", "a new track"));
                    break;
                case 1:
                    Heading(audioPage, T("mbSounds", "Sound effects"));
                    Note(audioPage, T("mbSoundsNote", "A file named like one of the game's sounds plays instead of it everywhere. A sound the game picks from several files (most shots) needs each replaced. A weapon's own shot is set on the item (Weapon fx)."));
                    Act(audioPage, T("mbReplaceSound", "+ Replace a game sound"), () =>
                    {
                        var choices = new List<(string, Texture2D, Action)>();
                        string folder = null;
                        foreach (var line in GameSounds())
                        {
                            int slash = line.IndexOf('/');
                            string f = slash > 0 ? line.Substring(0, slash) : "", name = slash > 0 ? line.Substring(slash + 1) : line;
                            if (f != folder) { folder = f; choices.Add((f.Replace('_', ' '), null, null)); }
                            string n = name;
                            choices.Add((n, null, () => PickAudio(T("mbPickSound", "Choose the sound file"), path => { Project.SetAudio(ModProject.SoundsFolder, n, path); AudioChanged(); })));
                        }
                        ShowPicker(T("mbReplaceSound", "+ Replace a game sound"), choices, true);
                    }, "mbv-add");
                    FileRows(ModProject.SoundsFolder, n => T("mbReplaces", "replaces the game's"));
                    break;
                default:
                    Heading(audioPage, T("mbVoices", "Voice lines"));
                    Note(audioPage, T("mbVoicesNote", "Lines for your quests' conversations and radio calls (each page names its line; a quest's \"Sound file…\" puts its file here). German ones in voices/de, played when the voice language is German."));
                    var vrow = Row(audioPage);
                    Act(vrow, T("mbNewVoice", "+ Voice line"), () => NewVoice(ModProject.VoicesFolder), "mbv-add");
                    Act(vrow, T("mbNewVoiceDe", "+ German line"), () => NewVoice(ModProject.VoicesFolder + "/de"), "mbv-add");
                    FileRows(ModProject.VoicesFolder, n => UsedBy(n));
                    if (Project.AudioFiles(ModProject.VoicesFolder + "/en").Count > 0) { Caption(audioPage, "English (voices/en)"); FileRows(ModProject.VoicesFolder + "/en", UsedBy); }
                    if (Project.AudioFiles(ModProject.VoicesFolder + "/de").Count > 0) { Caption(audioPage, "Deutsch (voices/de)"); FileRows(ModProject.VoicesFolder + "/de", UsedBy); }
                    break;
            }
        }

        void NewVoice(string folder) => AskText(T("mbNewVoice", "+ Voice line"), T("mbClipName", "The line's name (a-z, 0-9, _)"), "", name =>
        {
            string id = ModProject.Slug(name);
            PickAudio(T("mbPickVoice", "Choose the voice line"), path => { Project.SetAudio(folder, id, path); AudioChanged(); });
        });

        /// <summary>The quests that name a voice line.</summary>
        string UsedBy(string clip)
        {
            var users = Project.Quests.Where(q => q.ToJson().ToString(Newtonsoft.Json.Formatting.None).Contains("\"voice\":\"" + clip + "\"")).Select(q => q.Title ?? q.Name).ToList();
            return users.Count == 0 ? T("mbNotUsed", "not used by a quest") : string.Join(", ", users);
        }

        void FileRows(string folder, Func<string, string> sub)
        {
            var files = Project.AudioFiles(folder);
            if (files.Count == 0) { Note(audioPage, T("mbNoFiles", "No files yet.")); return; }
            foreach (var (name, path) in files)
            {
                string n = name, p = path;
                var row = Row(audioPage);
                var play = Act(row, playing == p ? "■" : "▶", () => TogglePreview(p), "mbv-step");
                Add(row, new Label($"{n}  ·  {sub(n)}"), "mbv-inline-label");
                Act(row, T("mbReplaceFile", "Replace…"), () => PickAudio(T("mbPickAudio", "Choose the file"), src => { Project.SetAudio(folder, n, src); AudioChanged(); }), "mbv-step");
                Act(row, "✕", () => Confirm(T("mbRemoveFile", "Remove file"), string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), n), () =>
                {
                    StopAudio();
                    Project.RemoveAudio(folder, n);
                    AudioChanged();
                }), "mbv-step");
            }
        }

        List<string> gameSounds;

        List<string> GameSounds()
        {
            if (gameSounds != null) return gameSounds;
            var asset = Resources.Load<TextAsset>("GoF2ModBuilder/SoundNames");
            gameSounds = asset == null ? new List<string>()
                : asset.text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
            return gameSounds;
        }

        void TogglePreview(string path)
        {
            if (playing == path) { StopAudio(); BuildAudioPage(); return; }
            StopAudio();
            var mod = ModManager.BuilderProject;
            if (mod == null) return;
            playing = path;
            ModAudio.Load(mod, path, clip =>
            {
                if (playing != path || clip == null) { if (clip == null) Status(T("mbAudioUnreadable", "The file couldn't be played."), true); return; }
                if (audioPreview == null)
                {
                    var go = new GameObject("Mod Builder audio preview");
                    audioPreview = go.AddComponent<AudioSource>();
                    audioPreview.ignoreListenerPause = true;
                    audioPreview.spatialBlend = 0f;
                }
                audioPreview.clip = clip;
                audioPreview.volume = path.StartsWith(ModProject.MusicFolder) ? Settings.MusicVolume : path.StartsWith(ModProject.VoicesFolder) ? Settings.VoiceVolume : Settings.SfxVolume;
                audioPreview.Play();
            });
            BuildAudioPage();
        }

        void UpdateAudio()
        {
            if (playing != null && audioPreview != null && audioPreview.clip != null && !audioPreview.isPlaying) { playing = null; BuildAudioPage(); }
        }

        void StopAudio()
        {
            playing = null;
            if (audioPreview == null) return;
            audioPreview.Stop();
            var clip = audioPreview.clip;
            audioPreview.clip = null;
            if (clip != null) UnityEngine.Object.Destroy(clip);
            UnityEngine.Object.Destroy(audioPreview.gameObject);
            audioPreview = null;
        }
    }
}
