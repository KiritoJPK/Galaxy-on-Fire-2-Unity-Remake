using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Netcode.Editor
{
    // Inspector used for discoverability for users. This way they don't need to know they need to add a GhostRigidbody monoBehaviour to replicate a Rigidbody, they just have a "replicate" checkbox.
    [CustomEditor(typeof(Rigidbody))]
    internal class GhostRigidbodyEditor : GhostMonoBehaviourEditor<Rigidbody, GhostRigidbody>
    {

    }

    /// <summary>
    /// Base class for different kinds of monoBehaviour editors, where we add a checkbox to an already existing monoBehaviour (e.g. Rigidbody).
    /// Useful for PhysicsCharacterController, Colliders, etc
    /// </summary>
    /// <typeparam name="T">The existing type to add the checkbox to.</typeparam>
    /// <typeparam name="TGhost">The GhostBehaviour to add when that checkbox is checked.</typeparam>
    internal abstract class GhostMonoBehaviourEditor<T, TGhost> : UnityEditor.Editor where T : Component where TGhost : GhostBehaviour
    {
        internal const string k_ToggleName = "Netcode Replication";
        private const string k_ToggleTooltip = "Adds Netcode replication/prediction support to this component.";

        public override VisualElement CreateInspectorGUI()
        {
            var baseElement = new VisualElement();
            InspectorElement.FillDefaultInspector(baseElement, serializedObject, this);
            var monoBehaviourTarget = (T)target;
            var existingMonoBehaviour = monoBehaviourTarget.GetComponent<TGhost>();
            var hasExisting = existingMonoBehaviour != null;

            // Ensure that any manually added component is hidden.
            if (hasExisting && existingMonoBehaviour.hideFlags != HideFlags.HideInInspector)
            {
                existingMonoBehaviour.hideFlags = HideFlags.HideInInspector;
            }

            // TODO-release@physicsUI better UI style for this
            var toggleParent = new PropertyField();
            toggleParent.tooltip = k_ToggleTooltip;
            toggleParent.style.flexDirection = FlexDirection.Row;
            toggleParent.SetEnabled(GhostObjectEditor.IsPrefabEditable(monoBehaviourTarget.gameObject));

            var toggle = new Toggle();
            toggle.value = hasExisting;
            toggle.style.marginRight = 4;

            var iconElement = new Image();
            iconElement.image = LegacyHierarchyDrawer.GhostIcon;
            iconElement.style.height = 15;
            iconElement.style.width = 15;

            var label = new Label(k_ToggleName);
            label.style.marginLeft = 2;

            toggle.RegisterCallback<ChangeEvent<bool>>(OnToggle);

            toggleParent.Add(toggle);
            toggleParent.Add(iconElement);
            toggleParent.Add(label);
            baseElement.Insert(0, toggleParent);
            return baseElement;
        }

        private void OnToggle(ChangeEvent<bool> evt)
        {
            var monoBehaviourTarget = target as T;
            // Safety check, this shouldn't be possible
            if (monoBehaviourTarget == null)
            {
                return;
            }

            var isEnabling = evt.newValue;
            var existing = monoBehaviourTarget.GetComponent<TGhost>();
            var hasExisting = existing != null;

            if (isEnabling && !hasExisting)
            {
                TrackAsUndoGroup(() =>
                {
                    var created = Undo.AddComponent<TGhost>(monoBehaviourTarget.gameObject);
                    created.hideFlags = HideFlags.HideInInspector;
                });
            }
            else if (!isEnabling && hasExisting)
            {
                TrackAsUndoGroup(() => Undo.DestroyObjectImmediate(existing));
            }

            // The UI of the inspector is automatically rebuilt whenever adding or removing components.
            // However, because we're removing a hidden component, this rebuild isn't triggered.
            // The GhostAuthoringInspectionComponentEditor relies on that rebuild whenever the components list changes.
            // Therefore, we need to force rebuild the editor.
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        /// <summary>
        /// The name to track if an undo or redo action came from this inspector.
        /// Allows us to correctly force rebuild the editor due to the hidden component.
        /// </summary>
        private const string k_ToggleUndoName = "Toggle Netcode Replication";

        /// <summary>
        /// Wrap the given action into a named undo group.
        /// This lets us watch for only this toggle and force rebuild the inspector on undo/redo.
        /// </summary>
        private void TrackAsUndoGroup(Action undoAction)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(k_ToggleUndoName);
            var group = Undo.GetCurrentGroup();
            undoAction();
            Undo.CollapseUndoOperations(group);
        }

        void OnEnable()
        {
            Undo.undoRedoEvent += OnUndoRedoEvent;
        }

        void OnDisable()
        {
            Undo.undoRedoEvent -= OnUndoRedoEvent;
        }

        /// <summary>
        /// Watch for an undo event that was made by <see cref="TrackAsUndoGroup"/>.
        /// If we see one, force a rebuild of the editor.
        /// </summary>
        private void OnUndoRedoEvent(in UndoRedoInfo info)
        {
            if (info.undoName == k_ToggleUndoName)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
        }
    }
}
