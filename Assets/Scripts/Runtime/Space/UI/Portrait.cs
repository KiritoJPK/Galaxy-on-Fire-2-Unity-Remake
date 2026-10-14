// Portrait.cs
// Character portraits as UI Toolkit layers, like ImageFactory::loadChar 0x1417bc / drawChar 0x1419a0
// (Reference/research/dialogue_cutscenes.md 1.3): a 160x200 box with the background 0x485, the parts of a descriptor
// {body, part0..part3} drawn in the order part2, part1, part0, part3 (each 160 px wide at its native height, placed top-
// or bottom-anchored at the body's IMAGE_OFFSETS_IPAD_LARGE y), then the frame 0x511. Keith's portrait is mirrored in the
// dialogue window. Styles: .portrait* in UI/Dialogue/Dialogue.uss.
// Remake mods: a mod's character (Modding.ModCharacters) is one PNG covering the box, over the background and under the
// frame (each optional); one that "replaces" a story speaker is drawn wherever that speaker's portrait is.

using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class Portrait
    {
        static readonly int[] DrawOrder = { 2, 1, 0, 3 };

        /// <summary>A story speaker's portrait (its layers resolved through the image ids, StoryTable); 'original': the game's
        /// own, not a mod character that replaces the speaker (the Mod Builder's speaker picker).</summary>
        public static void ShowSpeaker(VisualElement box, int speaker, bool mirrored, bool original = false)
        {
            var replacement = original ? null : Modding.ModCharacters.ReplacementFor(speaker);
            if (replacement != null) { ShowCharacter(box, replacement, mirrored); return; }
            box.Clear();
            box.EnableInClassList("portrait--mirrored", mirrored);
            var assets = StoryAssets.Load();
            if (assets == null) return;
            if (assets.portraitBackground != null) box.style.backgroundImage = assets.portraitBackground;
            var layers = new VisualElement { pickingMode = PickingMode.Ignore };
            layers.AddToClassList("portrait-layers");
            box.Add(layers);
            foreach (var l in StoryTable.PortraitLayers(speaker))
            {
                var (tex, imageHeight) = assets.Part(l.key);
                if (tex == null) continue;
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList("portrait-part");
                e.style.backgroundImage = tex;
                e.style.width = tex.width;
                e.style.height = tex.height;
                e.style.top = l.anchor == 32 ? l.y - imageHeight : l.y;
                layers.Add(e);
            }
            if (assets.portraitFrame != null)
            {
                var frame = new VisualElement { pickingMode = PickingMode.Ignore };
                frame.AddToClassList("portrait-frame");
                frame.style.backgroundImage = assets.portraitFrame;
                box.Add(frame);
            }
        }

        /// <summary>A mod's character (ModCharacters): its PNG covering the 160 x 200 box (its top kept), the game's background
        /// under it and frame over it unless the character turns them off; mirrored as asked or as the character says.</summary>
        public static void ShowCharacter(VisualElement box, Modding.ModCharacters.Def character, bool mirrored)
        {
            box.Clear();
            box.EnableInClassList("portrait--mirrored", mirrored != (character != null && character.mirrored));
            var assets = StoryAssets.Load();
            box.style.backgroundImage = character != null && !character.background || assets == null || assets.portraitBackground == null
                ? new StyleBackground(StyleKeyword.None) : new StyleBackground(assets.portraitBackground);
            var layers = new VisualElement { pickingMode = PickingMode.Ignore };
            layers.AddToClassList("portrait-layers");
            box.Add(layers);
            var tex = Modding.ModCharacters.Portrait(character);
            if (tex != null)
            {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList("portrait-part");
                e.style.backgroundImage = tex;
                e.style.top = 0;
                e.style.width = 160;
                e.style.height = 200;
                e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
                layers.Add(e);
            }
            if (assets != null && assets.portraitFrame != null && (character == null || character.frame))
            {
                var frame = new VisualElement { pickingMode = PickingMode.Ignore };
                frame.AddToClassList("portrait-frame");
                frame.style.backgroundImage = assets.portraitFrame;
                box.Add(frame);
            }
        }

        /// <summary>Fills 'box' (class .portrait) with the portrait of a descriptor (null = empty box); random agents.</summary>
        public static void Show(VisualElement box, int[] descriptor, bool mirrored)
        {
            box.Clear();
            box.EnableInClassList("portrait--mirrored", mirrored);
            var assets = StoryAssets.Load();
            if (assets == null) return;
            if (assets.portraitBackground != null) box.style.backgroundImage = assets.portraitBackground;
            var layers = new VisualElement { pickingMode = PickingMode.Ignore };
            layers.AddToClassList("portrait-layers");
            box.Add(layers);
            if (descriptor != null && descriptor.Length >= 5 && descriptor[0] >= 0)
            {
                int body = descriptor[0];
                foreach (int part in DrawOrder)
                {
                    int variant = descriptor[1 + part];
                    if (variant < 0) continue;
                    var (tex, imageHeight) = assets.Part(body, part, variant);
                    var offset = StoryTable.PortraitOffset(body, part);
                    if (tex == null || offset == null) continue;
                    // The image fills the top-left of its power-of-two canvas (the rest is transparent): draw the canvas
                    // at its native size; a bottom-anchored part ends at y with its image height.
                    var e = new VisualElement { pickingMode = PickingMode.Ignore };
                    e.AddToClassList("portrait-part");
                    e.style.backgroundImage = tex;
                    e.style.width = tex.width;
                    e.style.height = tex.height;
                    e.style.top = offset.anchor == 32 ? offset.y - imageHeight : offset.y;   // 32 = bottom-anchored
                    layers.Add(e);
                }
            }
            if (assets.portraitFrame != null)
            {
                var frame = new VisualElement { pickingMode = PickingMode.Ignore };
                frame.AddToClassList("portrait-frame");
                frame.style.backgroundImage = assets.portraitFrame;
                box.Add(frame);
            }
        }
    }
}
