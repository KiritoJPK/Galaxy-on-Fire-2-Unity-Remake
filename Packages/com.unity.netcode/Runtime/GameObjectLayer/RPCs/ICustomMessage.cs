using Unity.Entities;

namespace Unity.Netcode
{
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    readonly struct RPCInvokeID
    {
        readonly private ulong Value;

        public RPCInvokeID(ulong value)
        {
            Value = value;
        }

        public bool IsValid() { return Value != 0; }

        public override string ToString() { return Value.ToString(); }
    }

    // Used to indentify an RPC as AutoInvoke so we can pick it up when we receive it and call the linked method
    internal struct RPCAutoInvoke : IComponentData
    {
        public RPCInvokeID invokeType;
    }

    /// <summary>
    /// An interface that can be used to declare a 'Custom Message' (RPC) struct.
    /// </summary>
    /// <remarks>
    /// Custom messages are backed by <see cref="IRpcCommand"/> and have all the same restrictions
    /// </remarks>\
#if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    public
#else
    internal
#endif // NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
    interface ICustomMessage : IRpcCommand
    {
    }
}
