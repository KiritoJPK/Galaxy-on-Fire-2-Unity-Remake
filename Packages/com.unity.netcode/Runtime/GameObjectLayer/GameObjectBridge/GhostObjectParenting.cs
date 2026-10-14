using System.Diagnostics;
using Unity.Entities;
using Unity.Netcode.LowLevel;
using Unity.Netcode.NetcodeTime;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Netcode
{
    /// <summary>
    /// Parenting data is used to track the parenting status of a ghost.
    /// </summary>
    internal struct ParentingData : IComponentData
    {
        private const byte k_WorldPositionStays = 0x01;
        private const byte k_HasParent = 0x02;

        [GhostField]
        public byte ParentingFlags;

        /// <summary>
        /// This field is not replicated.
        /// This is used to determine if a parenting event is triggered by an authority replicated parent change, handled by the <see cref="ClientApplyParentingToGameObjectSystem"/>, or the <see cref="GhostObject"/> itself.
        /// </summary>
        /// <remarks>
        /// Declared between the two GhostFields so it packs into the padding after <see cref="ParentingFlags"/>.
        /// </remarks>
        public bool ApplyingReplicatedParent;

        /// <summary>
        /// The parent ghost, or <see cref="Entity.Null"/> when the ghost has no parent.
        /// </summary>
        /// <remarks>
        /// An Entity GhostField carries the parent's ghost identity on the wire and resolves it against the client's spawned ghost map.
        /// A parent ghost that has not spawned on this client and will resolve to <see cref="Entity.Null"/> is indistinguishable from an
        /// authority parent replication event that genuinely unparented. The <see cref="GetHasParent"/> flag is what separates the two,
        /// and should be checked before acting on a null parent event.
        /// </remarks>
        [GhostField]
        public Entity Parent;

        /// <summary>
        /// Sets the world position stays flag.
        /// </summary>
        /// <remarks>
        /// The WorldPositionStays flag needs to be synchronized and is only used within the <see cref="ParentingInterpolationFixSystem"/>.
        /// </remarks>
        internal void SetWorldPositionStays(bool worldPositionStays)
        {
            if (worldPositionStays)
            {
                ParentingFlags |= k_WorldPositionStays;
            }
            else
            {
                ParentingFlags &= (0xFF & ~k_WorldPositionStays);
            }
        }

        /// <summary>
        /// Returns whether the world position stays flag is set or not.
        /// </summary>
        /// <remarks>
        /// The WorldPositionStays flag needs to be synchronized and is only used within the <see cref="ParentingInterpolationFixSystem"/>.
        /// </remarks>
        internal bool GetWorldPositionStays()
        {
            return (ParentingFlags & k_WorldPositionStays) == k_WorldPositionStays;
        }

        /// <summary>
        /// Used by the authority to determine if a parent is set. This is used independently from the actual parent in the event that the
        /// <see cref="Parent"/> resolves on a client or not.
        /// </summary>
        /// <remarks>
        /// Set by the <see cref="GhostObject"/> on the authority. The <see cref="ClientApplyParentingToGameObjectSystem"/> needs this because an unresolved parent
        /// and no parent both arrive as <see cref="Entity.Null"/>, and unparenting on the first of those leaves the ghost at the root.
        /// </remarks>
        internal void SetHasParent(bool hasParent)
        {
            if (hasParent)
            {
                ParentingFlags |= k_HasParent;
            }
            else
            {
                ParentingFlags &= (0xFF & ~k_HasParent);
            }
        }

        /// <summary>
        /// Returns whether the authority has this ghost parented under another ghost.
        /// </summary>
        internal bool GetHasParent()
        {
            return (ParentingFlags & k_HasParent) == k_HasParent;
        }
    }

    /// <summary>
    /// This system handles GhostObject parent changes on clients only.
    /// </summary>
    /// <remarks>
    /// This will parent before the local to world system is invoked to assure the local to world transformation takes the parent (or no parent) into consideration.
    /// </remarks>
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LocalToWorldSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    internal partial class ClientApplyParentingToGameObjectSystem : SystemBase
    {
        /// <summary>
        /// Puts a ghost back in line for <see cref="GhostUpdateSystem"/> to apply its newest snapshot again.
        /// </summary>
        /// <remarks>
        /// A static optimized ghost is skipped entirely (see the "Interpolated static ghost early exit" in <see cref="GhostUpdateSystem"/>)
        /// once its newest snapshot has been applied and the interpolation timeline has passed the parenting tick window. Otherwise, an
        /// unresolved <see cref="ParentingData.Parent"/> would then never resolve and the ghost would remain at the root.
        /// This only runs while a parent is unresolved, so a settled ghost is never kept awake by it.
        /// </remarks>
        private void ForceSnapshotReapply(Entity ghostEntity)
        {
            if (!SystemAPI.HasComponent<SnapshotData>(ghostEntity))
            {
                return;
            }
            SystemAPI.GetComponentRW<SnapshotData>(ghostEntity).ValueRW.AppliedTick = NetworkTick.Invalid;
        }

        [Conditional("UNIFIED_PARENTING_DEBUG")]
        private void LogParentChanged(ref GameObject gameObject, ref Transform parentTransform)
        {
            var currentParent = gameObject.transform.parent == null ? "Root" : gameObject.transform.parent.name;
            var nextParent = parentTransform == null ? "Root" : parentTransform.name;
            Debug.Log($"Parenting: From {currentParent} to {nextParent}");
        }

        protected override void OnCreate()
        {

            if (!World.IsServer())
            {
                // Only run this system when a Ghost has ParentingData.
                RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<ParentingData>(), ComponentType.ReadOnly<GhostInstance>()));

                // Only update parenting if the client is connected to a session.
                RequireForUpdate<NetworkStreamInGame>();
            }
            else
            {
                // Server/Host does not need to run this system
                Enabled = false;
            }
        }

        protected override void OnUpdate()
        {
            var entityManager = World.EntityManager;
            foreach (var (parentData, link, ghostEntity) in SystemAPI.Query<RefRW<ParentingData>, RefRO<GhostGameObjectLink>>().WithEntityAccess())
            {
                var ghostGameObject = Resources.EntityIdToObject(link.ValueRO.AssociatedGameObject) as GameObject;
                if (!ghostGameObject)
                {
                    Debug.LogError($"Failed to find linked GameObject for Ghost-{ghostEntity.Index}");
                    continue;
                }
                // The authority is parented but the Entity GhostField has not resolved yet, because the parent ghost has not been spawned
                // on the client yet (will spawn in a later packet or is not relevant until now). Unparenting on that is indistinguishable
                // from the authority unparenting, so hold the current hierarchy and retry next frame.
                if (parentData.ValueRO.GetHasParent() && parentData.ValueRO.Parent == Entity.Null)
                {
                    ForceSnapshotReapply(ghostEntity);
                    continue;
                }

                // This check assures the parent ghost is spawned. If it is not spawned or has been despawned, then ignore it
                // for this update pass. Parenting against a half-resolved parent is worse than waiting a tick.
                Transform parentTransform = null;
                if (parentData.ValueRO.Parent != Entity.Null)
                {
                    var parentEntity = parentData.ValueRO.Parent;
                    if (!entityManager.Exists(parentEntity) || !entityManager.HasComponent<GhostGameObjectLink>(parentEntity))
                    {
                        continue;
                    }
                    var parentGameObject = Resources.EntityIdToObject(entityManager.GetComponentData<GhostGameObjectLink>(parentEntity).AssociatedGameObject) as GameObject;
                    if (!parentGameObject)
                    {
                        continue;
                    }
                    parentTransform = parentGameObject.transform;
                }

                // If the parent has changed, then mirror the authority instance's current parenting state
                if (ghostGameObject.transform.parent != parentTransform)
                {
                    LogParentChanged(ref ghostGameObject, ref parentTransform);

                    // Mark this parenting event as a parent replication.
                    parentData.ValueRW.ApplyingReplicatedParent = true;

                    // Note: Why always parent local space relative?
                    // The authority has already computed and replicated the post-parent LocalTransform. If we used the worldPositionStays
                    // value and it is true, then it will change the pre-reparent world position/scale for one frame, causing a visible
                    // pop/flicker when adding OR removing a parent. As such, we should always parent "local space" relative and let the
                    // replicated transform values drive the GhostObject's transform for flicker-free parenting.
                    try
                    {
                        ghostGameObject.transform.SetParent(parentTransform, worldPositionStays: false);
                    }
                    finally
                    {
                        // We always clear this flag in a finally in the event SetParent throws an exception. Otherwise, if this flag is
                        // left set it would silently accept every later client authored change on this ghost (i.e. within GhostObject.OnParentChanged).
                        parentData.ValueRW.ApplyingReplicatedParent = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// This system only exists as a work around due to the non-interruptible interpolation (i.e. missing teleport-like capabilities) that occurs after quantization. <br />
    /// This only applies for Ghosts that are not in a predicting state.
    /// </summary>
    /// <remarks>
    /// This fix must run after GhostUpdateSystem has written the interpolated LocalTransform, but before the Entity->GameObject transform sync consumes it.
    /// GhostGameObjectSystemGroup contains GhostObjectEntityToGameObjectTransformSystem that pushes LocalTransform to the GameObject.
    /// </remarks>
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostUpdateSystem))]
    [UpdateBefore(typeof(GhostGameObjectSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    // SnapshotDataLookupHelper reads the GhostCollection and the SpawnedGhostEntityMap, so it can only be built
    // after the systems that create them (below).
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateAfter(typeof(GhostReceiveSystem))]
    internal partial class ParentingInterpolationFixSystem : SystemBase
    {
        private SnapshotDataLookupHelper m_SnapshotDataLookupHelper;

        protected override void OnCreate()
        {
            // Server/Host does not need to run this system. Its ghosts are authoritative rather than interpolated.
            if (World.IsServer())
            {
                Enabled = false;
                return;
            }
            // Only run on Ghosts that have parent replication enabled.
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<ParentingData>(), ComponentType.ReadOnly<GhostInstance>()));
            // Only update parenting if the client is connected to a session.
            RequireForUpdate<NetworkStreamInGame>();
            RequireForUpdate<GhostCollection>();

            // Build the snapshot helper that is used to hold the transform values between the tick prior to parenting and
            // the parenting tick itself.
            m_SnapshotDataLookupHelper = new SnapshotDataLookupHelper(ref CheckedStateRef,
                SystemAPI.GetSingletonEntity<GhostCollection>(),
                SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>());
        }
        protected override void OnUpdate()
        {
            var time = SystemAPI.GetSingleton<NetworkTime>();

            m_SnapshotDataLookupHelper.Update(ref CheckedStateRef);
            var snapshotDataLookup = m_SnapshotDataLookupHelper.CreateSnapshotBufferLookup();

            // Iterate over Interpolated ghosts only.
            // Since there currently doesn't appear to be a way to exclude entities with a specific tag, component, or the like, we have to exclude predicted Ghosts here.
            // Notes on excluding anything with PredictedGhost:
            // This system pins the transform over the INTERPOLATION window in order to prevent interpolation from occuring during the parenting tick. A predicted ghost's
            // transform is produced by the prediction loop that uses the predicted tick which can be several ticks ahead of the INTERPOLATION window. The ClientApplyParentingToGameObjectSystem
            // reparents its GameObject off the same predicted timeline. So, a predicted ghost straddles the reparent tick long after the GameObject moved and pinning a predicted
            // Ghost will restore a stale pre-reparent LocalTransform underneath the new parent. Thus, we don't want to include predicted Ghosts for this interpolation only
            // specific fix.
            foreach (var (transformRef, buffer, data, ghost, ghostEntity) in SystemAPI.Query<RefRW<LocalTransform>, DynamicBuffer<SnapshotDataBuffer>, RefRO<SnapshotData>, RefRO<GhostInstance>>().WithAll<ParentingData>().WithNone<PredictedGhost>().WithEntityAccess())
            {
                // Sample the same interpolation window GhostUpdateSystem used this frame.
                // BeforeIdx is the snapshot at/below the interpolation tick where the applied ParentingData.Parent is applied since the Entity GhostField
                // is copied from the "before" snapshot. AfterIdx is the snapshot the interpolation blending, towards any changes to the transform values,
                // that we want to prevent.
                // False means no snapshot has been received at or before the interpolation tick, which leaves the returned indices uninitialized.
                if (!data.ValueRO.GetDataAtTick(time.InterpolationTick, 0, 1, time.InterpolationTickFraction, buffer, out var tickData, 0, false))
                {
                    continue;
                }

                // Ignore anything outside of the before and after indices.
                if (!snapshotDataLookup.TryGetComponentDataFromSnapshotHistory(ghost.ValueRO.ghostType, buffer, out ParentingData beforeParentData, tickData.BeforeIdx)
                    || !snapshotDataLookup.TryGetComponentDataFromSnapshotHistory(ghost.ValueRO.ghostType, buffer, out ParentingData afterParentData, tickData.AfterIdx))
                {
                    continue;
                }

                // Notes on this fix:
                // This handles the Before and After straddling for a parent change only when world-position-stays is true. This "pins" the ghost's local
                // transform to the Before-frame value until the window advances past the reparent tick (Before == After again). Upon advancement, the
                // ClientApplyParentingToGameObjectSystem has performed the parenting action to the GameObject with no visual artifacts and the ghost can continue interpolating smoothly.
                //
                // Do NOT pin when world-position-stays is false:
                // Under this scenario, the local space transform values should be preserved across the parenting frame (i.e local stays, world changes).
                // Pinning would instead freeze the ghost at its Before-frame value, holding back the intended world-position change during any in-flight
                // motion would yield a visible visual artifact.
                // GetHasParent is compared alongside the entity because a parent ghost that has not spawned here resolves to Entity.Null on
                // both sides of the window, which would hide a parent or unparent that the flag still reports.
                if ((beforeParentData.Parent != afterParentData.Parent || beforeParentData.GetHasParent() != afterParentData.GetHasParent())
                    && afterParentData.GetWorldPositionStays()
                    && snapshotDataLookup.TryGetComponentDataFromSnapshotHistory(ghost.ValueRO.ghostType, buffer, out LocalTransform beforeTransform, tickData.BeforeIdx))
                {
                    // Pin the full LocalTransform (Position + Rotation + Scale) so nothing moves while the window straddles the reparent tick.
                    transformRef.ValueRW = beforeTransform;

                    // LocalTransform.Scale is uniform, so a non-uniform scale rides in the PostTransformMatrix and is replicated by PostTransformMatrix3DScaleVariant
                    // with InterpolateAndExtrapolate smoothing. Pinning LocalTransform alone leaves that half still interpolating across the reparent tick, which is a
                    // visible world-scale flicker under a non-uniformly scaled parent.
                    // This only pins scale:
                    // The variant never replicates rotation or translation, so the rest of the live matrix must be preserved.
                    if (SystemAPI.HasComponent<PostTransformMatrix>(ghostEntity)
                        && snapshotDataLookup.TryGetComponentDataFromSnapshotHistory(ghost.ValueRO.ghostType, buffer, out PostTransformMatrix beforeMatrix, tickData.BeforeIdx))
                    {
                        var postTransformMatrix = SystemAPI.GetComponentRW<PostTransformMatrix>(ghostEntity);
                        postTransformMatrix.ValueRW.Value = MatrixScaleHelper.SetScale(postTransformMatrix.ValueRO.Value,
                            MatrixScaleHelper.SignedScaleFromScaleOnlyMatrix(beforeMatrix.Value));
                    }
                }
            }
        }
    }
}


