using Unity.Netcode.Tracing;

namespace Unity.Netcode.Editor.Tracing.UI
{
    // The active view filters of the tracing tool, evaluated against stored DiffAggregates. Shared by every view.
    internal class TracingViewFilters
    {
        public DiffInfo.DiffReasons EnabledDiffReasons = DiffInfo.AllDiffReasons;
        public bool ChangedValuesOnly;

        public float FuzzyDiffThreshold;

        public DiffInfo.DiffReasons ApplyFuzzyThreshold(DiffInfo.DiffReasons reasons, float dataDiffAmount)
        {
            if (FuzzyDiffThreshold > 0f
                && (reasons & DiffInfo.DiffReasons.ComponentData) != 0
                && !(dataDiffAmount > FuzzyDiffThreshold))
                reasons &= ~DiffInfo.DiffReasons.ComponentData;
            return reasons;
        }

        // True when the diff info contains at least one aggregate that survives every active filter.
        public bool HasVisibleDiff(in DiffInfo diffInfo)
        {
            if (!diffInfo.AggregatesRO.IsCreated)
                return false;

            foreach (var kvp in diffInfo.AggregatesRO)
            {
                if (PassesFilters(kvp.Key, kvp.Value))
                    return true;
            }

            return false;
        }

        DiffInfo.DiffReasons MutedTickReasons => DiffInfo.TickScopeReasons & ~EnabledDiffReasons;

        public bool PassesFilters(in DiffAggregate aggregate, float dataDiffAmount)
        {
            var reasons = ApplyFuzzyThreshold(aggregate.Reasons, dataDiffAmount);
            if ((reasons & MutedTickReasons) != 0)
                return false;

            if (!IsUntargeted(aggregate))
                reasons &= ~DiffInfo.TickScopeReasons;
            return (reasons & EnabledDiffReasons) != 0 && PassesTargetFilters(aggregate);
        }

        static bool IsUntargeted(in DiffAggregate aggregate)
            => aggregate.System == default && aggregate.GhostId == DiffAggregate.InvalidGhostId && aggregate.Component == default;

        public bool IsTickMuted(DiffInfo.DiffReasons tickReasons) => (tickReasons & MutedTickReasons) != 0;

        public DiffInfo.DiffReasons EffectiveEnabledReasons(DiffInfo.DiffReasons tickReasons)
            => IsTickMuted(tickReasons) ? DiffInfo.DiffReasons.Undefined : EnabledDiffReasons;

        // Target visibility lives in the store, so every view agrees with the filter panel's eyes.
        public bool IsTargetHidden(TracingTargetId target) => TracingTargetStore.IsHiddenInViews(target);

        // True when the aggregate survives the target filters (hidden systems/components, changed values only).
        bool PassesTargetFilters(in DiffAggregate aggregate)
        {
            if (IsUntargeted(aggregate))
                return true;

            if (ChangedValuesOnly && !aggregate.HasValueChanged)
                return false;
            if (aggregate.Component != default && IsTargetHidden(TracingTargetId.Of(aggregate.Component)))
                return false;
            if (aggregate.System != default && IsTargetHidden(TracingTargetId.Of(aggregate.System)))
                return false;
            return true;
        }
    }
}
