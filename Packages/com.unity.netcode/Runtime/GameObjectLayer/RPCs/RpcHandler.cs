using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.Netcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation,
     WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [CreateBefore(typeof(Internal_RPCAutoInvokeSystemGroup))]
    [UpdateInGroup(typeof(Internal_RPCAutoInvokeSystemGroup), OrderFirst = true)]
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    partial class RPCAutoInvokeSystem : SystemBase
    {
        private EntityQuery m_Query;
        internal Dictionary<RPCInvokeID, RPCHandler.AutoHandleDelegate> m_InvokeFunctions;

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RPCAutoInvoke>());
            m_InvokeFunctions = new Dictionary<RPCInvokeID, RPCHandler.AutoHandleDelegate>();
            RequireForUpdate(m_Query);
        }

        protected override void OnUpdate()
        {
            foreach (var (autoInvoke, ent) in SystemAPI.Query<RefRO<RPCAutoInvoke>>().WithEntityAccess())
            {
                if ( m_InvokeFunctions.TryGetValue(autoInvoke.ValueRO.invokeType, out var invokeFunction) )
                {
                    invokeFunction(EntityManager, ent);
                }
                else
                {
                    Debug.LogWarning($"Attempted to auto invoke PC method with invokeType: {autoInvoke.ValueRO.invokeType}. To debug set unity.netcode.sourcegenerator.write_files_to_disk to 1 in Default.globalconfig and look in your projects Temp directory for 'RpcSerializer.cs' files containing this number to match the name of the RPC.");
                }
            }
            EntityManager.DestroyEntity(m_Query);
        }
    }

    /// <summary>
    /// Main point of access to RPC APIs.
    /// </summary>
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    class RPCHandler
    {
        // When you call invoke with no arguments we send to all
        private List<NetcodeWorld> GetAllClientAndServerWorlds()
        {
            List<NetcodeWorld> worlds = new List<NetcodeWorld>(ClientServerBootstrap.ServerWorlds);

            foreach (var w in ClientServerBootstrap.ClientWorlds)
            {
                if (!worlds.Contains(w))
                {
                    worlds.Add(w);
                }
            }

            return worlds;
        }

        private List<Connection> GetWorldTargetConnections( NetcodeWorld world, SendDirection direction)
        {
            List<Connection> connections = new List<Connection>();

            if (world.IsServer() && (direction == SendDirection.ServerToClient || direction == SendDirection.Undefined))
            {
                connections.AddRange(world.AllConnections);
                if (world.IsHost())
                {
                    connections.Add(world.LocalConnection);
                }
            }

            if (world.IsClient() && (direction == SendDirection.ClientToServer || direction == SendDirection.Undefined))
            {
                connections.Add(world.LocalConnection);
            }

            return connections;
        }

        private void Send<T>(T rpc, Connection connection) where T : unmanaged, ICustomMessage
        {
            var req = connection.World.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(T));
            connection.World.EntityManager.SetComponentData(req, new SendRpcCommandRequest { TargetConnection = connection.ConnectionEntity });
            connection.World.EntityManager.SetComponentData(req, rpc);
        }

        /// <summary>
        /// Call to send an RPC of type T to the specified world, this will find the connection on that world
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Invoke.</typeparam>
        /// <param name="rpc">The RPC to send</param>
        /// <param name="target">The world to send the RPC on all of that worlds connections</param>
        /// <param name="direction">The direction the RPC is intended to be sent</param>
        public void Invoke<T>(T rpc, NetcodeWorld target, SendDirection direction) where T : unmanaged, ICustomMessage
        {
            Invoke<T>(rpc, GetWorldTargetConnections(target, direction));
        }

        /// <summary>
        /// Call to send an RPC of type T to the specified worlds, this will find the connection on the worlds
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Invoke.</typeparam>
        /// <param name="rpc">The RPC to send</param>
        /// <param name="target">The worlds to send the RPC on all of that worlds connections</param>
        /// /// <param name="direction">The direction the RPC is intended to be sent</param>
        public void Invoke<T>(T rpc, List<NetcodeWorld> targets, SendDirection direction) where T : unmanaged, ICustomMessage
        {
            foreach (var t in targets)
            {
                Invoke<T>(rpc, t, direction);
            }
        }

        /// <summary>
        /// Call to send an RPC of type T to the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Invoke.</typeparam>
        /// <param name="RPC">The RPC to send</param>
        /// <param name="target">The connection to send the RPC on</param>
        public void Invoke<T>(T RPC, Connection target) where T : unmanaged, ICustomMessage
        {
            Send<T>(RPC, target);
        }

        /// <summary>
        /// Call to send an RPC of type T to the list of specified connections
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Invoke.</typeparam>
        /// <param name="RPC">The RPC to send</param>
        /// <param name="targets">The list of connections to send to</param>
        public void Invoke<T>(T RPC, List<Connection> targets) where T : unmanaged, ICustomMessage
        {
            foreach (var t in targets)
            {
                Invoke(RPC, t);
            }
        }

        /// <summary>
        /// Call to get all the RPCs of type T received in all connections on the client and server worlds since the last call of Query. This will clear the RPCs list before adding to it.
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param>A list of RPCs of type T to set to all the received RPCs.</returns>
        public void Query<T>( List<T> rpcs ) where T : unmanaged, ICustomMessage
        {
            Query<T>(GetAllClientAndServerWorlds(), rpcs);
        }

        /// <summary>
        /// Call to get all the RPCs of type T received in all connections on the client and server worlds since the last call of Query.
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <returns>A list of all the received RPCs.</returns>
        public List<T> Query<T>() where T : unmanaged, ICustomMessage
        {
            List<T> rpcs = new List<T>();
            Query<T>(rpcs);
            return rpcs;
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="worlds">The list of worlds to query for received RPCs</param>
        /// <param name="rpcs">A list of all the received RPCs.</param>
        public void Query<T>(List<NetcodeWorld> worlds, List<T> rpcs) where T : unmanaged, ICustomMessage
        {
            foreach (var w in worlds)
            {
                Query<T>(w, rpcs);
            }
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="worlds">The connection to query for received RPCs</param>
        /// <returns>A list of all the received RPCs.</returns>
        public List<T> Query<T>(List<NetcodeWorld> worlds) where T : unmanaged, ICustomMessage
        {
            List<T> rpcs = new List<T>();
            Query<T>(worlds, rpcs);
            return rpcs;
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="target">The connection to query for received RPCs</param>
        /// <param name="rpcs">A list of all the received RPCs.</param>
        public void Query<T>(NetcodeWorld world, List<T> rpcs) where T : unmanaged, ICustomMessage
        {
            Query<T>(GetWorldTargetConnections(world, SendDirection.Undefined), rpcs);
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="world">The world to query for received RPCs</param>
        /// <returns>A list of all the received RPCs.</returns>
        public List<T> Query<T>(NetcodeWorld world) where T : unmanaged, ICustomMessage
        {
            return Query<T>(GetWorldTargetConnections(world, SendDirection.Undefined));
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connections
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="targets">The list of connections to query for received RPCs</param>
        /// <param name="RPCs">A list of all the received RPCs.</param>
        public void Query<T>(List<Connection> targets, List<T> RPCs) where T : unmanaged, ICustomMessage
        {
            foreach (var t in targets)
            {
                Query<T>(t, RPCs);
            }
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connections
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="targets">The list of connections to query for received RPCs</param>
        /// <returns>A list of all the received RPCs.</returns>
        public List<T> Query<T>(List<Connection> targets) where T : unmanaged, ICustomMessage
        {
            List<T> rpcs = new List<T>();
            Query<T>(targets, rpcs);
            return rpcs;
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="target">The connection to query for received RPCs</param>
        /// <param name="rpcs">A list of all the received RPCs.</param>
        public void Query<T>(Connection target, List<T> rpcs) where T : unmanaged, ICustomMessage
        {
            if (target.World != null)
            {
                // We should revisit this query and rebuilding it all th time. Currently caching this will require some way to cache per world and per type being looked up
                // that could get messy so there might be a better way to go about this but it needs some thought.
                var query = target.World.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<T, ReceiveRpcCommandRequest>());
                var entites = query.ToEntityArray(Allocator.Temp);
                var receivedRpcs = query.ToComponentDataArray<T>(Allocator.Temp);

                foreach (var e in entites)
                {
                    var rpcCommand = target.World.EntityManager.GetComponentData<ReceiveRpcCommandRequest>(e);
                    if (rpcCommand.SourceConnection == target.ConnectionEntity)
                    {
                        rpcs.Add(target.World.EntityManager.GetComponentData<T>(e));
                        target.World.EntityManager.DestroyEntity(e);
                    }
                }
            }
        }

        /// <summary>
        /// Call to get all the RPCs of type T received on the specified connection
        /// </summary>
        /// <typeparam name="T">The type of the RPC to Query.</typeparam>
        /// <param name="target">The connection to query for received RPCs</param>
        /// <returns>A list of all the received RPCs.</returns>
        public List<T> Query<T>(Connection target) where T : unmanaged, ICustomMessage
        {
            List<T> rpcs = new List<T>();
            Query<T>(target, rpcs);
            return rpcs;
        }

        /// <summary>
        /// Used for calling an RPCs handle method.
        /// </summary>
        public delegate void AutoHandleDelegate(EntityManager em, Entity e);

        /// <summary>
        /// Register an RPC handle method with its id so it can be automatically actioned when received
        /// </summary>
        /// <param name="id">The id of the RPC</param>
        /// <param name="invokeDelegate">the method to invoke when an RPC with this id is received</param>
        /// <param name="world">the world to be registered into</param>
        public void RegisterAutoInvokeType(RPCInvokeID id, AutoHandleDelegate invokeDelegate, NetcodeWorld world)
        {
            world.GetExistingSystemManaged<RPCAutoInvokeSystem>().m_InvokeFunctions.TryAdd(id, invokeDelegate);
        }

        /// <summary>
        /// Utility function to resolve a Gameobject from a world, entity id and spawnTick.
        /// </summary>
        /// <param name="world">The id of the RPC</param>
        /// <param name="id">the id of the entity to find the atached gameobject for</param>
        /// <param name="spawnTick">the spawnTick of the entity to find the atached gameobject for</param>
        /// /// <returns>The gameobject if found, null otherwise</returns>
        public GameObject ResolveGameObject( NetcodeWorld world, int id, uint spawnTick )
        {
            // TODO: We need to cache this query and possibly others, since managing this cache isn't trivial (need to account for the same world being taken down and back up again)
            //       we might want some central place we do this since getting the SpawnedGhostEntityMap will be useful in a bunch of places and we can manage this internally in one place for the netcode package
            using var query = world.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<SpawnedGhostEntityMap>());
            if (query.CalculateEntityCount() > 0)
            {
                var ghostMap = world.EntityManager.GetComponentData<SpawnedGhostEntityMap>(query.GetSingletonEntity());
                var netTick = new NetworkTick { SerializedData = spawnTick };
                if (ghostMap.Value.TryGetValue(new SpawnedGhost { ghostId = id, spawnTick = netTick }, out var ghostEnt) && world.EntityManager.Exists(ghostEnt))
                {
                    return (GameObject)Resources.EntityIdToObject(world.EntityManager.GetComponentData<GhostGameObjectLink>(ghostEnt).AssociatedGameObject);
                }
            }

            return null;
        }
    }
}
