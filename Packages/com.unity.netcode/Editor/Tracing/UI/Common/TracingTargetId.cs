using System;
using Unity.Entities;

namespace Unity.Netcode.Editor.Tracing.UI
{
    internal readonly struct TracingTargetId : IEquatable<TracingTargetId>
    {
        public readonly Type Type;
        public readonly TracingTargetKind Kind;

        TracingTargetId(Type type)
        {
            Type = type;
            Kind = TracingTargetTypes.IsTracingEcsSystemType(type)
                ? TracingTargetKind.System
                : TracingTargetKind.Component;
        }

        public static TracingTargetId Of(Type type) => new(type);
        public static TracingTargetId Of(TypeIndex component) => new(TypeManager.GetType(component));
        public static TracingTargetId Of(SystemTypeIndex system) => new(TypeManager.GetSystemType(system));

        public static bool TryParse(string key, out TracingTargetId id)
        {
            var type = string.IsNullOrEmpty(key) ? null : Type.GetType(key);
            id = new TracingTargetId(type);
            return type != null;
        }

        public bool IsValid => Type != null;

        public bool IsSystem => Kind == TracingTargetKind.System;

        public bool IsManagedComponent => !IsSystem && !TracingTargetTypes.IsUnmanagedComponentType(Type);

        //The backend always traces GhostInstance; selecting it only shows it in the views.
        public bool IsGhostInstance => Type == typeof(GhostInstance);

        public string Key => Type.AssemblyQualifiedName;

        public string FullName => Type.FullName;

        public string DisplayName => Type.Name;

        public bool Equals(TracingTargetId other) => Type == other.Type;

        public override bool Equals(object obj) => obj is TracingTargetId other && Equals(other);
        public override int GetHashCode() => Type == null ? 0 : Type.GetHashCode();
        public override string ToString() => Type == null ? "<none>" : FullName;
    }
}
