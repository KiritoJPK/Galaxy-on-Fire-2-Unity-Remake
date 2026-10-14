using Unity.Netcode.Tracing;

namespace Unity.Netcode.Editor.Tracing.UI
{

    // The display name and one-line explanation of each <see cref="DiffInfo.DiffReasons"/>.
    static class DiffReasonCatalog
    {
        internal readonly struct Entry
        {
            public readonly DiffInfo.DiffReasons Reason;
            public readonly string Label;
            public readonly string Tooltip;

            public Entry(DiffInfo.DiffReasons reason, string label, string tooltip)
            {
                Reason = reason;
                Label = label;
                Tooltip = tooltip;
            }
        }

        // Declaration order is the order reasons render in
        internal static readonly Entry[] All =
        {
            new(DiffInfo.DiffReasons.ComponentData, "Component Data", "Value differs between client and server."),
            new(DiffInfo.DiffReasons.MissingComponent, "Missing Component", "Component on the server but not the client."),
            new(DiffInfo.DiffReasons.ExtraComponent, "Extra Component", "Component on the client but not the server."),
            new(DiffInfo.DiffReasons.MissingGhost, "Missing Ghost", "Ghost on the server but not the client."),
            new(DiffInfo.DiffReasons.ExtraGhost, "Extra Ghost", "Ghost on the client but not the server."),
            new(DiffInfo.DiffReasons.MissingSystem, "Missing System", "System ran on the server but not the client."),
            new(DiffInfo.DiffReasons.ExtraSystem, "Extra System", "System ran on the client but not the server."),
            new(DiffInfo.DiffReasons.SystemOrder, "System Order", "Systems ran in a different order on each side."),
            new(DiffInfo.DiffReasons.DeltaTime, "Delta Time", "Client and server tick delta times differ."),
            new(DiffInfo.DiffReasons.BatchedTick, "Batched Tick", "Server simulated this tick as part of a batch."),
            new(DiffInfo.DiffReasons.PartialTick, "Partial Tick", "Diff on a partial tick, where mismatches are expected."),
        };

        // Every catalog reason OR'd together — the "all diff tags selected" mask
        internal static readonly DiffInfo.DiffReasons SelectableMask = ComputeSelectableMask();

        // The diff reasons are grouped in two categories: highlighted diff reasons, and the tick-scope reasons that act as tick filters when deselected.
        internal static readonly DiffInfo.DiffReasons HighlightedMask = SelectableMask & ~DiffInfo.TickScopeReasons;
        internal static readonly DiffInfo.DiffReasons TickScopeMask = SelectableMask & DiffInfo.TickScopeReasons;

        internal static bool IsTickScope(in Entry entry) => (entry.Reason & DiffInfo.TickScopeReasons) != 0;

        static DiffInfo.DiffReasons ComputeSelectableMask()
        {
            var mask = DiffInfo.DiffReasons.Undefined;
            foreach (var entry in All)
                mask |= entry.Reason;
            return mask;
        }

        internal static string SelectionTooltip(Entry entry, bool selected)
            => entry.Tooltip + (selected ? "\nClick to deselect." : "\nClick to select.");
    }
}
