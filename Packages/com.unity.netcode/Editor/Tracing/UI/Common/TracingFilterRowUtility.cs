using System;
using Unity.Netcode.Editor.Tracing.UI.FilterPanel;
using UnityEngine.UIElements;

namespace Unity.Netcode.Editor.Tracing.UI
{
    internal static class TracingFilterRowUtility
    {
        public static void SetEyeIcon(Button toggle, bool visible)
        {
            toggle.EnableInClassList(TracingFilterPanelUssClasses.IconVisible, visible);
            toggle.EnableInClassList(TracingFilterPanelUssClasses.IconHidden, !visible);
        }

        public static void SetToggleAction(Button toggle, Action onClicked)
        {
            if (toggle.userData is Action oldAction)
                toggle.clicked -= oldAction;
            toggle.userData = onClicked;
            if (onClicked != null)
                toggle.clicked += onClicked;
        }
    }
}
