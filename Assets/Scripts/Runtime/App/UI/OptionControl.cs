// OptionControl.cs
// The row of one OptionDef: a Slider ("Label: value"), a Toggle or a ChoiceRow, plus an optional description line.
// Modified: an option with a description gets a "?" icon at the right end of its row (OptionInfo: its window opens after
// 1 s of the mouse on it, or on a tap); every row but the key bindings keeps that room free, so the controls line up.
// The description line under the row shows only with a controller (GoF2Common.uss).
// Modified: a slider is the name, then at the row's right its value in a small outlined box and the bar (as wide as the
// choices' drop-downs); on the Sound page a speaker before the value mutes the sound (a click: 0, another click: the
// volume it had).
// Root is the row's container, Field the focusable control inside it. Refresh re-reads Settings (after "Default
// settings", a language change or a change made elsewhere) without raising the control's own callbacks.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public sealed class OptionControl
    {
        public readonly OptionDef def;
        public readonly VisualElement Root;
        public readonly VisualElement Field;
        readonly Slider slider;
        readonly Toggle toggle;
        readonly ChoiceRow choice;
        readonly Button button;
        readonly BindingRow binding;
        readonly Label description;
        readonly VisualElement info;
        Label sliderValue;
        VisualElement muteIcon;
        float unmutedValue = -1f;
        static Texture2D soundIcon, mutedIcon;

        /// <summary>The player changed the value through this row.</summary>
        public event Action Changed;

        public OptionControl(OptionDef d)
        {
            def = d;
            Root = new VisualElement();
            Root.AddToClassList("option-item");
            switch (d.kind)
            {
                case OptionKind.Slider:
                    slider = new Slider(d.min, d.max);
                    slider.RegisterValueChangedCallback(e => { d.set(e.newValue); UpdateSliderLabel(); Changed?.Invoke(); });
                    Field = slider;
                    BuildSliderBox(d.page == OptionPage.Sound);
                    break;
                case OptionKind.Toggle:
                    toggle = new Toggle();
                    toggle.RegisterValueChangedCallback(e => { d.setBool(e.newValue); Changed?.Invoke(); });
                    Field = toggle;
                    break;
                case OptionKind.Binding:
                    binding = new BindingRow(d.control);
                    Field = binding;
                    break;
                case OptionKind.Button:
                    button = new Button(() => { d.action?.Invoke(); Changed?.Invoke(); });
                    button.AddToClassList("option-button");
                    Field = button;
                    break;
                default:
                    // Modified: every choice is a drop-down (ChoiceRow, ChoicePopup), the few-value ones too (they were segments).
                    choice = new ChoiceRow(false);
                    choice.Changed += i => { d.setIndex(i); UpdateDescription(); Changed?.Invoke(); };
                    Field = choice;
                    break;
            }
            Root.Add(Field);
            if (d.kind != OptionKind.Binding) Root.AddToClassList("option-item--info-room");
            if (d.description != null)
            {
                description = new Label { pickingMode = PickingMode.Ignore };
                description.AddToClassList("option-description");
                Root.Add(description);
                info = OptionInfo.Icon(d.description);
                Root.Add(info);   // on the row, not its control: a greyed-out option's description still opens
                Field.RegisterCallback<GeometryChangedEvent>(_ => PlaceInfo());
            }
            if (d.extra != null) Root.Add(d.extra());
            Refresh();
        }

        /// <summary>Left / right change the row's value (a slider, a choice, a binding), not move to the next item.</summary>
        public bool StepsSideways => slider != null || choice != null || binding != null;

        /// <summary>The row is shown (OptionDef.visible; always without one).</summary>
        public bool Shown => def.visible == null || def.visible();

        /// <summary>The row takes input (OptionDef.enabled; always without one): greyed out and skipped by the keys while not.</summary>
        public bool Enabled => def.enabled == null || def.enabled();

        public void Refresh()
        {
            if (def.visible != null) Root.style.display = Shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (def.enabled != null) Field.SetEnabled(Enabled);
            if (slider != null)
            {
                slider.SetValueWithoutNotify(def.get());
                UpdateSliderLabel();
            }
            else if (toggle != null)
            {
                toggle.label = def.label();
                toggle.SetValueWithoutNotify(def.getBool());
            }
            else if (button != null) button.text = def.label();
            else if (binding != null) binding.Refresh();
            else
            {
                choice.LabelText = def.label();
                choice.SetWithoutNotify(def.choices(), def.getIndex());
            }
            UpdateDescription();
        }

        void UpdateSliderLabel()
        {
            // Modified: the name alone; the value in its box (it was "Name: value").
            slider.label = def.label();
            float v = slider.value;
            if (sliderValue != null) sliderValue.text = def.format != null ? def.format(v) : Mathf.RoundToInt(v).ToString();
            if (muteIcon != null)
            {
                bool muted = v <= slider.lowValue + 0.0001f;
                var tex = muted ? mutedIcon : soundIcon;
                if (tex != null) muteIcon.style.backgroundImage = new StyleBackground(tex);
                muteIcon.EnableInClassList("option-slider-mute--muted", muted);
            }
        }

        /// <summary>The slider's parts: the value box (and the Sound page's speaker) before the bar.</summary>
        void BuildSliderBox(bool sound)
        {
            slider.AddToClassList("option-slider");
            var input = slider.Q(className: "unity-base-slider__input");
            if (input?.parent == null) return;
            int at = input.parent.IndexOf(input);
            if (sound)
            {
                if (soundIcon == null) soundIcon = Resources.Load<Texture2D>("GoF2Hud/option_sound");
                if (mutedIcon == null) mutedIcon = Resources.Load<Texture2D>("GoF2Hud/option_sound_muted");
                muteIcon = new VisualElement { pickingMode = PickingMode.Position };
                muteIcon.AddToClassList("option-slider-mute");
                // Its own press: not the row's (a drag) or the slider's.
                muteIcon.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                muteIcon.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
                muteIcon.RegisterCallback<ClickEvent>(e => { e.StopPropagation(); ToggleMute(); });
                input.parent.Insert(at++, muteIcon);
            }
            // Takes its own presses and does nothing with them: let through, the slider took them (a press there lost the
            // row's highlight until the mouse left and came back).
            sliderValue = new Label { pickingMode = PickingMode.Position };
            sliderValue.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            sliderValue.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
            sliderValue.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            sliderValue.AddToClassList("option-slider-value");
            sliderValue.AddToClassList("gof-semibold");
            input.parent.Insert(at, sliderValue);
        }

        /// <summary>The speaker: to 0, or back to the volume it had before (half the range when it had none).</summary>
        void ToggleMute()
        {
            if (slider == null || !Enabled) return;
            if (slider.value > slider.lowValue + 0.0001f) { unmutedValue = slider.value; slider.value = slider.lowValue; }
            else slider.value = unmutedValue > slider.lowValue ? unmutedValue : (slider.lowValue + slider.highValue) * 0.5f;
        }

        /// <summary>The "?" icon at the row's right end (the room .option-item--info-room keeps), centred on the control.</summary>
        void PlaceInfo()
        {
            if (info == null) return;
            var box = Field.layout;
            if (float.IsNaN(box.height) || box.height <= 0f) return;
            info.style.right = 4f;
            info.style.top = box.y + (box.height - OptionInfo.IconSize) * 0.5f;
        }

        void UpdateDescription()
        {
            if (description == null) return;
            description.text = def.description();
            bool has = !string.IsNullOrEmpty(description.text);
            // Shown or not by the stylesheet (a controller only); an empty one never.
            description.style.display = has ? StyleKeyword.Null : DisplayStyle.None;
            if (info != null) info.style.display = has ? StyleKeyword.Null : DisplayStyle.None;
        }

        /// <summary>Left / right on the row (keys, D-pad): a slider moves a twentieth of its range, a choice one value,
        /// a toggle flips.</summary>
        public void Step(int dir)
        {
            if (!Enabled) return;
            if (slider != null)
                slider.value = Mathf.Clamp(slider.value + dir * (slider.highValue - slider.lowValue) / 20f, slider.lowValue, slider.highValue);
            else if (toggle != null) toggle.value = !toggle.value;
            else if (binding != null) binding.Step(dir);
            else choice?.Step(dir);
        }

        /// <summary>Confirm on the row (Enter / A): a toggle flips, a button presses, a binding captures. A choice keeps its
        /// value: left / right pick it (a player's report: A cycled on to the next value).</summary>
        public void Activate()
        {
            if (!Enabled) return;
            if (toggle != null) toggle.value = !toggle.value;
            else if (button != null) { def.action?.Invoke(); Changed?.Invoke(); }
            else if (binding != null) binding.Activate();
        }
    }
}
