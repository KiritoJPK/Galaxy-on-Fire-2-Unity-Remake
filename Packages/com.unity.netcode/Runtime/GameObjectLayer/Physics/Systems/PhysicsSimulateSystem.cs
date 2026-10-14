using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode.PhysicsInternalAccess;
using Unity.Netcode.NetcodeTime;
using Unity.Profiling;
using Unity.Scripting.LifecycleManagement;
using Unity.Transforms;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// System in charge of doing the main netcode tick driven physics step.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [CreateAfter(typeof(NetDebugSystem))]
    internal partial class GameObjectPhysicsSimulateSystem : SystemBase
    {
        ProfilerMarker m_PredictMarker = new ProfilerMarker($"{nameof(GameObjectPhysicsSimulateSystem)}.Predict");

        internal delegate PhysicsScene GetPhysicsSceneToSimulateDelegate(World forWorld);
        [AutoStaticsCleanup]
        internal static GetPhysicsSceneToSimulateDelegate GetPhysicsSceneCallback;

        SimulationMode m_OldMode = SimulationMode.FixedUpdate;
        EntityQuery m_RigidbodyQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_RigidbodyQuery = World.EntityManager.CreateEntityQuery(typeof(GhostRigidbodyData), typeof(GhostRigidbodyGameObjectTracker));

            m_OldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script; // This means if users want an offline scene, they'll need to call Simulate on it manually. Unity doesn't allow Physics.Simulate + normal FixedUpdate simulation.
            RequireForUpdate<GhostRigidbodyData>();
#if UNITY_EDITOR
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            // Those logs should only appear if log level is set to max noise level (debug). These are useful warnings/guidance when debugging issues with netcode.
            if (GetSolverType() != 0 && netDebug.LogLevel <= NetDebug.LogLevelType.Debug)
                netDebug.LogWarning("[Netcode Debug] [Predicted GameObject Physics] The solver type Temporal Gauss Seidel usually doesn't produce deterministic enough results for prediction. Please update your solver type in your project settings.");
            if (!GetIsDeterminism() && netDebug.LogLevel <= NetDebug.LogLevelType.Debug)
                netDebug.LogWarning("[Netcode Debug] [Predicted GameObject Physics] Enhanced Determinism is set to false in your physics settings. It's recommended to set it to true in a predicted netcode context.");
#endif
        }

        protected override void OnUpdate()
        {
            using var _ = m_PredictMarker.Auto();
            var time = SystemAPI.GetSingleton<NetworkTime>();
            if (time.IsPartialTick) return;

            var deltaTime = SystemAPI.Time.DeltaTime;

            // SetAutoWakeInternal is set to false client side in PredictedGhostPrepareRigidbodySystem, but we need to set it server side too because the SyncTransform below can wake things up.
            // However, we want server side moved transforms to wake up as normal.
            // The transform move must affect sleep status, but only if the transform was changed by user code.

            // This needs to happen before we reenable AutoWake on each rigidbody
            // GO to RB
            {
                // TODO-next@physicsPerf there's some jobs overhead here... would be great if we had some APIs engine side to force schedule those jobs, there's idle time right now
                // TODO-next@physicsPerf we should check if a Rigidbody is active and just skip applying changes to it if there's no need for it.

                // Now apply transform changes from PredictionPhysicsUpdate to the RB. Any changes coming from the network have already been applied in PredictedGhostPrepareRigidbodySystem.
                using var rbTrackers = m_RigidbodyQuery.ToComponentDataArray<GhostRigidbodyGameObjectTracker>(Allocator.Temp);
                for (int i = 0; i < rbTrackers.Length; i++)
                {
                    Rigidbody rb = rbTrackers[i].Rigidbody.Value;
                    rb.gameObject.transform.GetPositionAndRotation(out var transformPos, out var transformRot);
                    // Need to use Quaternion's != operator as it has an epsilon. Transform's rotation setters are not idempotent (it normalizes on set).
                    // If you do this.transform.rotation = this.transform.rotation a few times with any initial value that's not the default, you'll get drift
                    // TransformRef will have an API to set rotation without normalizing, but for now using a small epsilon to counter this.
                    // position should be fine without epsilon so using Equals instead.
                    rb.SetAutoWakeInternal(!rb.position.Equals(transformPos) || rb.rotation != (transformRot));
                    // zero moves still need to be applied client/server side the same way for determinism purposes.
                    rb.Move(transformPos, transformRot);
                }

                // TODO-next@physicsPerf, looks like doing this is different from SyncTransform. That's why tests keep passing when they shouldn't. Using this for now and will use Physics.SyncTransform once it's fixed. Make sure to come back to tests with multiple worlds, they will need to be adapted to sync individual worlds
                // Physics.SyncTransforms(); // assumes auto sync transform is set to false (set in prefixedupdate)

                //TODO-next@physicsPerf since we control inputs, we could detect if there's zero inputs and zero user side logic that could influence the sim and then just skip transform syncing in those ticks?
            }


            // SetAutoWakeInternal needs to be true for the remainder of the frame so that objects are woken appropriately when they're moved server side in the editor scene view.
            foreach (var (data, goTracker, ghostTransformData) in SystemAPI.Query<GhostRigidbodyData, GhostRigidbodyGameObjectTracker, LocalTransform>())
            {
                Rigidbody rigidbody = goTracker.Rigidbody.Value;
                rigidbody.SetAutoWakeInternal(true);
            }

            PhysicsScene currentScene;
            if (GetPhysicsSceneCallback == null)
            {
                currentScene = Physics.defaultPhysicsScene;
            }
            else
            {
                // useful for tests when running with binary world (so one scene per world). Internal right now, but could be useful for binary world setup for users in the future as well
                currentScene = GetPhysicsSceneCallback(World);
            }

            currentScene.RunSimulationStages(deltaTime, SimulationStage.PrepareSimulation, SimulationOption.None); // TODO-release@physics this could be skipped if users can tell us "I'm not touching anything this tick"
            currentScene.RunSimulationStages(deltaTime, SimulationStage.RunSimulation, SimulationOption.None);
            currentScene.RunSimulationStages(deltaTime, SimulationStage.PublishSimulationResults, SimulationOption.None); // TODO-release@physics can skip this? or at least the publishing of contact events? If we make this optional, this could help skip some of the sim cost.
        }

        protected override void OnDestroy()
        {
            int netcodeWorldCount = 0;
            foreach (var netWorld in Netcode.GetNetcodeWorlds())
            {
                netcodeWorldCount++;
            }

            if (netcodeWorldCount == 1)
            {
                // Only set this when this is the last world remaining
                Physics.simulationMode = m_OldMode;
            }
        }

#if UNITY_EDITOR
        const string k_SolverTypePropertyKey = "m_SolverType";
        const string k_DeterminismPropertyKey = "m_EnableEnhancedDeterminism";

        static SerializedObject GetPhysicsSettings()
        {
            var found = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (found == null || found.Length == 0)
                return null;

            return new SerializedObject(found[0]);
        }
        /// <summary>
        /// Since there's no API to get this, reading it ourselves to validate at the recommendation of @Alexandru
        /// </summary>
        /// <returns>0 for Projected Gauss Seidel and 1 for Temporal Gauss Seidel</returns>
        internal static int GetSolverType()
        {
            var serializedObject = GetPhysicsSettings();
            if (serializedObject == null)
            {
                Debug.LogWarning("couldn't find physics settings, defaulting to 0 for solver type check.");
                return 0;
            }
            var solverType = serializedObject.FindProperty(k_SolverTypePropertyKey);
            return solverType.intValue;
        }

        /// <summary>
        /// Gets the setting for physics determinism. Note that this only gets the setting, not whether any active Physics World (scene) had it enabled when they themselves were created. A discrepancy can happen if you set that setting after scene creation.
        /// </summary>
        /// <returns></returns>
        internal static bool GetIsDeterminism()
        {
            var serializedObject = GetPhysicsSettings();
            if (serializedObject == null)
            {
                Debug.LogWarning("couldn't find physics settings, defaulting to false for determinism check.");
                return false;
            }

            var determinismEnabled = serializedObject.FindProperty(k_DeterminismPropertyKey);
            return determinismEnabled.boolValue;
        }

        /// <summary>
        /// Saves on disk the determinism setting in physics. This needs to happen before the target Physics World is created (so before scene creation)
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="FileNotFoundException"></exception>
        internal static void SetDeterminism(bool value)
        {
            var serializedObject = GetPhysicsSettings();
            if (serializedObject == null)
            {
                throw new FileNotFoundException("Couldn't find physics settings to set determinism checkbox");
            }
            var determinismEnabled = serializedObject.FindProperty(k_DeterminismPropertyKey);
            determinismEnabled.boolValue = value;
            serializedObject.ApplyModifiedProperties();
        }
#endif
    }
}
