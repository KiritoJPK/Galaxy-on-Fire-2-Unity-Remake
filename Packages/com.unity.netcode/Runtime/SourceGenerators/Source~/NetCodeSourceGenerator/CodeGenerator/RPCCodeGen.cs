using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode.Roslyn;

namespace Unity.Netcode.Generators
{
    // The RPCSerializer instances are created by CodeGenerator. The class itself is not threadsafe,
    // but since every SourceGenerator has its own Context it is safe use.
    // Please avoid to use shared static variables or state here and verify that in case you need, they are immutable or thread safe.
    internal class RPCCodeGen
    {
        private GhostCodeGen m_CommandGenerator;

        public RPCCodeGen(CodeGenerator.Context context)
        {
            var generator = context.codeGenCache.GetTemplate(CodeGenerator.RPCSynchronization);
            m_CommandGenerator = generator.Clone();
        }

        public void Generate(CodeGenerator.Context context, TypeInformation typeInfo) 
        {
            var replacements = new Dictionary<string, string>
            {
                // RPC_NAME shouldn't contain namespace
                {"RPC_NAME", context.RPCContext.BackingRPCComponentName.Replace(".", "").Replace('+', '_')},
                // {"RPC_NAME", typeInfo.TypeFullName.Replace(".", "").Replace('+', '_')},
                {"RPC_NAMESPACE", typeInfo.Namespace == "" ? "global" : typeInfo.Namespace}
            };

            // Make the namespace block
            if ( typeInfo.Namespace != "" )
            {
                m_CommandGenerator.GenerateFragment("HAS_RPC_NAMESPACE_START", replacements);
                m_CommandGenerator.GenerateFragment("HAS_RPC_NAMESPACE_END", replacements);   
            }


            if (!string.IsNullOrEmpty(typeInfo.Namespace))
                context.imports.Add(typeInfo.Namespace);

            foreach (var import in context.imports)
            {
                replacements["RPC_USING"] = CodeGenerator.GetValidNamespaceForType(context.generatedNs, import);
                m_CommandGenerator.GenerateFragment("RPC_USING_STATEMENT", replacements);
            }

            // Check to see if we have a handle function
            if ( typeInfo.IsAutoInvokeRPC )
            {
                replacements["RPC_HANDLER_ID"] = ComputeCustomMessageHash( context ).ToString();
                replacements["RPC_AUTO_INVOKE_HANDLE_ARGS"] = typeInfo.IsGhostBehaviourMethod ? "(NetcodeWorld)em.World" : "";
                m_CommandGenerator.GenerateFragment("RPC_AUTO_INVOKE", replacements);
            }

            // Check to see if we are an RPC method
            if ( typeInfo.MethodSymbol != null )
            {
                List<string> callArguments = new List<string>();
                List<string> methodArguments = new List<string>();
                List<string> constructorArguments = new List<string>();

                foreach (var type in typeInfo.GhostFields)
                {
                    replacements["MEMBER_NAME"] = $"{type.FieldName}";
                    replacements["MEMBER_TYPE"] = type.FieldTypeName;
                    m_CommandGenerator.GenerateFragment("RPC_METHOD_ARGS_AS_MEMBERS", replacements);


                    bool isParamTargetGhostData = typeInfo.IsGhostBehaviourMethod && ( type.FieldName == GetTargetGhostIdMemberName() || type.FieldName == GetTargetSpawnTickMemberName() );

                    if (!isParamTargetGhostData)
                    {
                        callArguments.Add($"{type.FieldName}");
                        methodArguments.Add($"{type.FieldTypeName} _{type.FieldName}");

                        constructorArguments.Add($"{type.FieldName} = _{type.FieldName}");
                    }
                    else
                    {
                        // This needs serious consideration to fix up properly
                        if (type.FieldName == GetTargetGhostIdMemberName())
                        {
                            constructorArguments.Add($"{GetTargetGhostIdMemberName()} = this.Ghost.GhostId");
                        }
                        else
                        {
                            constructorArguments.Add($"{GetTargetSpawnTickMemberName()} = this.Ghost.SpawnTick.SerializedData");
                        }
                    }
                }

                replacements["RPC_METHOD_STATIC"] = typeInfo.MethodSymbol.IsStatic ? "static" : "";

                // pass the member name to the handle function
                replacements["RPC_METHOD_NAME"] = typeInfo.MethodSymbol.Name;
                replacements["RPC_METHOD_INVOKE_CALL"] = $"{typeInfo.MethodSymbol.ReceiverType.GetFullTypeName()}.{typeInfo.MethodSymbol.Name}";

                replacements["RPC_METHOD_CALL_ARGS"] = string.Join(",", callArguments);

                if (typeInfo.IsGhostBehaviourMethod)
                {

                    replacements["RPC_GHOST_METHOD_CLASS_NAME"] = typeInfo.MethodSymbol.ReceiverType.GetFullTypeName();
                    replacements["RPC_GHOST_TARGET_ID_NAME"] = GetTargetGhostIdMemberName();
                    replacements["RPC_GHOST_SPAWN_TICK_NAME"] = GetTargetSpawnTickMemberName();

                    m_CommandGenerator.GenerateFragment("RPC_GHOST_METHOD_HANDLE_METHOD", replacements);
                }
                else
                {
                    m_CommandGenerator.GenerateFragment("RPC_STATIC_METHOD_HANDLE_METHOD", replacements);
                }

                // Make the invoke function
                replacements["RPC_METHOD_ARGS"] = string.Join(",", methodArguments);

                replacements["RPC_METHOD_DIRECTION_ARGS"] = "";

                // TODO: there is more we can do here, we never need to generate the system to process this RPC on the server/client if its not needed
                // this is not an optimization it will also make sure no one can call the function remotely by packet messing since the system to process it simply won't exist
                var RPCAttribute = Roslyn.Extensions.GetAttribute(typeInfo.MethodSymbol, "Unity.Netcode", "RPCAttribute");
                if ( RPCAttribute != null )
                {
                    foreach ( var ca in RPCAttribute.ConstructorArguments )
                    {
                        if (Roslyn.Extensions.GetFullTypeName(ca.Type) == "Unity.Netcode.SendDirection")
                        {
                            switch (ca.Value)
                            {
                                case 0: // Undefined
                                    replacements["RPC_METHOD_DIRECTION_ERROR"] = "Debug.LogError( \"RPC Methods must specify a SendDirection.\" );";
                                    break;
                                case 1: // ClientToServer
                                    replacements["RPC_METHOD_DIRECTION_ARGS"] = ", ClientServerBootstrap.ClientWorlds, SendDirection.ClientToServer";
                                    replacements["RPC_METHOD_DIRECTION_ERROR"] = typeInfo.IsGhostBehaviourMethod
                                        ? "if (!IsClient) Debug.LogError( \"Can't Invoke a ClientToServer RPC on a Server GhostBehaviour.\" );"
                                        : "if (!Netcode.IsClientRole) Debug.LogError( \"Can't Invoke a static ClientToServer RPC on a Server instance.\" );";
                                    break;
                                case 2: // ServerToClient
                                    replacements["RPC_METHOD_DIRECTION_ARGS"] = ", ClientServerBootstrap.ServerWorlds, SendDirection.ServerToClient";
                                    replacements["RPC_METHOD_DIRECTION_ERROR"] = typeInfo.IsGhostBehaviourMethod
                                        ? "if (!IsServer) Debug.LogError( \"Can't Invoke a ServerToClient RPC on a Client GhostBehaviour.\" );"
                                        : "if (!Netcode.IsServerRole) Debug.LogError( \"Can't Invoke a static ServerToClient RPC on a Client instance.\" );";
                                    break;
                            }
                        }
                    }
                }

                replacements["RPC_METHOD_CONSTRUICTOR_ARGS"] = string.Join(",", constructorArguments);

                string containingTypeAsString = "";
                var containingType = typeInfo.MethodSymbol.ContainingType;

                switch (containingType.DeclaredAccessibility)
                {
                    case Microsoft.CodeAnalysis.Accessibility.Public:
                        containingTypeAsString += "public";
                        break;
                };

                containingTypeAsString += " partial";

                switch ( containingType.TypeKind )
                {
                    case Microsoft.CodeAnalysis.TypeKind.Class:
                        containingTypeAsString += " class";
                        break;
                    case Microsoft.CodeAnalysis.TypeKind.Struct:
                        containingTypeAsString += " struct";
                        break;
                }

                containingTypeAsString += $" {containingType.Name}";

                replacements["RPC_METHOD_CONTAINER_TYPE_DECLARATION"] = containingTypeAsString;

                m_CommandGenerator.GenerateFragment("RPC_METHOD", replacements);
            }

            var serializerName = context.generatorName + "RPCSerializer.cs";
            m_CommandGenerator.GenerateFile(serializerName, replacements, context.batch);
        }

        public static ulong ComputeCustomMessageHash(CodeGenerator.Context context)
        {
            var hash = Utilities.TypeHash.FNV1A64("NetCode.ICustomMessage");
            hash = Utilities.TypeHash.CombineFNV1A64(hash, Utilities.TypeHash.FNV1A64(context.generatorName));
            return hash;
        }

        public override string ToString()
        {
            var debugInformation = m_CommandGenerator?.ToString();
            return debugInformation;
        }

        public static string GetTargetGhostIdMemberName()
        {
            return "_targetGhostId";
        }

        public static string GetTargetSpawnTickMemberName()
        {
            return $"{GetTargetGhostIdMemberName()}_SpawnTick";
        }

        public static string GetGhostBehaviourFullTypeName()
        {
            return "Unity.Netcode.GhostBehaviour";
        }
    }
}
