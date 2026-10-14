using System;

namespace Unity.Netcode
{
    /// <summary>
    /// Describes the direction an RPC is sent.
    /// </summary>
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    enum SendDirection
    {
        /// <summary>
        /// Undefined SendDirection this should not be specified by users, its a guard so we can have a default and check users have specifed thier send direction explicitly.
        /// </summary>
        Undefined,
        /// <summary>
        /// Used to send an RPC from a Client instace to a Server instace, if sent in the other direction the RPC will fail
        /// </summary>
        ClientToServer,
        /// <summary>
        /// Used to send an RPC from a Server instace to a Client instace, if sent in the other direction the RPC will fail
        /// </summary>
        ServerToClient,
        // TODO: Revist this and see if we want this to be an option, How useful is it really?
        // BiDirectional,
    }

    /// <summary>
    /// Add to a struct, static or ghost behaviour method to turn it into an RPC
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
        class RPCAttribute : Attribute
    {
        private SendDirection _sendDirection;

        /// <summary>
        /// <inheritdoc cref="RPCAttribute"/>
        /// </summary>
        /// <param name="sendDirection">The direction in which this remote is valid to send.</param>
        public RPCAttribute(SendDirection sendDirection = SendDirection.Undefined)
        {
            this._sendDirection = sendDirection;
        }
    }
    
    /// <summary>
    /// Add to a struct, static or ghost behaviour method to turn it into an RPC
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
        class CustomMessageAttribute : Attribute
    {
        private SendDirection _sendDirection;

        /// <summary>
        /// <inheritdoc cref="RPCAttribute"/>
        /// </summary>
        /// <param name="sendDirection">The direction in which this remote is valid to send.</param>
        public CustomMessageAttribute(SendDirection sendDirection = SendDirection.Undefined)
        {
            this._sendDirection = sendDirection;
        }
    }
}
