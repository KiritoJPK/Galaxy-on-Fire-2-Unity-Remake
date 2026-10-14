using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.Editor
{
    // GoF2 remake: embedded copy of Netcode for Entities 7.1.0. Unity 7000.0.0a9's built-in Entities 6.7 dropped the DOTS
    // Hierarchy window (IHierarchyItemDecorator, HierarchyListViewItem), so its DotsHierarchyItemDecorator was removed
    // here; the IMGUI drawer below still marks ghosts. Go back to the registry package once a release compiles on a9.

    /// <summary>Draws Netcode related data onto the Hierarchy Window.</summary>
    [InitializeOnLoad]
    static class LegacyHierarchyDrawer
    {
        const string k_IconDirectory = "Packages/com.unity.netcode/EditorIcons/";

        static Texture2D s_GhostIconDark;
        static Texture2D s_GhostIconLight;

        static LegacyHierarchyDrawer()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= GhostAuthoringSetCustomColors;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += GhostAuthoringSetCustomColors;
        }

        public static Texture2D GhostIcon => EditorGUIUtility.isProSkin
            ? s_GhostIconDark ??= (Texture2D) AssetDatabase.LoadAssetAtPath($"{k_IconDirectory}d_Ghost@64.png", typeof(Texture2D))
            : s_GhostIconLight ??= (Texture2D) AssetDatabase.LoadAssetAtPath($"{k_IconDirectory}Ghost@64.png", typeof(Texture2D));

        public static Color NetcodeColor => EditorGUIUtility.isProSkin ? new Color(0.91f, 0.55f, 0.86f) : new Color(0.8f, 0.14f, 0.5f);

        static void GhostAuthoringSetCustomColors(EntityId instanceID, Rect selectionRect)
        {
            var obj = EditorUtility.EntityIdToObject(instanceID);
            if (obj != null && obj is GameObject go)
            {
                if (go.GetComponent<GhostAuthoringComponent>())
                {
                    // Ghost icon on right-hand side:
                    var iconRectRight = selectionRect;
                    iconRectRight.x += iconRectRight.width - iconRectRight.height;
                    iconRectRight.y -= 1.5f;
                    iconRectRight.height += 1.5f;
                    iconRectRight.width -= 70; // Icon will shrink into invisibility earlier.
                    GUI.Label(iconRectRight, GhostIcon);
                }

                // TODO - Consider showing the GhostInspectionComponent too.
            }
        }
    }
}
