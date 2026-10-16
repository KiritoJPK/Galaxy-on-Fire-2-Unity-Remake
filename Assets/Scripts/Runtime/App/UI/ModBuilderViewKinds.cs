// ModBuilderViewKinds.cs
// The Mod Builder's hangar screen (part of ModBuilderView): what the kinds with a view of their own share with the screen's
// frame: the picks remembered over an undo, the hint line, the per-frame work, Esc, the pointer over the 3D view (the rooms'
// free camera, the star map) and the clean-up. Each kind's file implements its part (ModBuilderViewBlueprints, ...Campaign,
// ...Interiors, ...Quests, ...Audio, ...Systems).

using System;
using GoF2Remake.Modding;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        /// <summary>The kinds' picks before an undo; the returned action picks them again.</summary>
        Action RememberPicks()
        {
            string bp = currentBlueprint?.Ref, option = currentOption?.Ref, room = currentInterior?.Ref, quest = currentQuest?.Name, system = currentSystem?.Ref;
            return () =>
            {
                currentBlueprint = Project.Blueprints.Find(b => b.Ref == bp) ?? (Project.Blueprints.Count > 0 ? Project.Blueprints[0] : null);
                currentOption = Project.GameOptions.Find(o => o.Ref == option);
                currentInterior = Project.Interiors.Find(r => r.Ref == room) ?? (Project.Interiors.Count > 0 ? Project.Interiors[0] : null);
                currentQuest = Project.Quests.Find(q => q.Name == quest) ?? (Project.Quests.Count > 0 ? Project.Quests[0] : null);
                currentSystem = Project.Systems.Find(s => s.Ref == system) ?? (Project.Systems.Count > 0 ? Project.Systems[0] : null);
                currentStep = null;
            };
        }

        /// <summary>The hint line at the foot for the kind (a marker being placed, a system being moved).</summary>
        string PlaceHintOfKind() => kind switch
        {
            ModBuilder.Kind.Interiors => InteriorHint(),
            ModBuilder.Kind.Systems => SystemsHint(),
            _ => "",
        };

        void UpdateKind(float dt)
        {
            if (kind == ModBuilder.Kind.Interiors) UpdateInteriorView(dt);
            if (kind == ModBuilder.Kind.Audio) UpdateAudio();
        }

        bool EscapeKind() => kind switch
        {
            ModBuilder.Kind.Interiors => EscapeInterior(),
            ModBuilder.Kind.Systems => EscapeSystems(),
            _ => false,
        };

        bool KindPointerDown(PointerDownEvent e) => kind == ModBuilder.Kind.Interiors && InteriorPointerDown(e);
        bool KindPointerMove(PointerMoveEvent e) => kind == ModBuilder.Kind.Interiors && InteriorPointerMove(e);
        bool KindPointerUp(PointerUpEvent e) => kind == ModBuilder.Kind.Interiors && InteriorPointerUp(e);

        void DisposeKinds()
        {
            EndInteriorView();
            CloseMap();
            StopAudio();
        }
    }
}
