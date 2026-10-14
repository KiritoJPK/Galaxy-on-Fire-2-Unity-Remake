using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Netcode.Generators
{
    internal class GameObjectRPCFactory
    {
        /// <summary>
        /// Collect and generate RPCs.
        /// </summary>
        /// <param name="rpcCandidates"></param>
        /// <param name="compilation"></param>
        /// <param name="codeGenContext"></param>
        public static void Generate(IReadOnlyList<SyntaxNode> rpcCandidates, CodeGenerator.Context codeGenContext)
        {
            GenerateRPCs(rpcCandidates, codeGenContext);
            GenerateRPCRegistrationSystem(rpcCandidates, codeGenContext);
        }

        private static void GenerateRPCs(IReadOnlyList<SyntaxNode> rpcCandidates, CodeGenerator.Context codeGenContext)
        {
            var typeBuilder = new TypeInformationBuilder(codeGenContext.diagnostic, codeGenContext.executionContext, TypeInformationBuilder.SerializationMode.Commands);
            var rootNamespace = codeGenContext.generatedNs;
            foreach (var syntaxNode in rpcCandidates)
            {
                codeGenContext.executionContext.CancellationToken.ThrowIfCancellationRequested();
                Profiler.Begin("GetSemanticModel");
                var model = codeGenContext.executionContext.Compilation.GetSemanticModel(syntaxNode.SyntaxTree);
                Profiler.End();

                // Allow us to generate for methods too
                if (model.GetDeclaredSymbol(syntaxNode) is not INamedTypeSymbol && model.GetDeclaredSymbol(syntaxNode as BaseMethodDeclarationSyntax) is not IMethodSymbol)
                    continue;

                TypeInformation typeInfo = null;

                var candidateTypeSymbol = model.GetDeclaredSymbol(syntaxNode) as INamedTypeSymbol;

                // RPC can be either on a type or on a method
                if ( candidateTypeSymbol != null )
                {
                    var disableCommandCodeGen = Roslyn.Extensions.GetAttribute(candidateTypeSymbol,
                    "Unity.Netcode", "NetcodeDisableCommandCodeGenAttribute");
                    if (disableCommandCodeGen != null)
                        continue;

                    codeGenContext.ResetState();
                    codeGenContext.RPCContext.OriginalRPCComponentTypeName = Roslyn.Extensions.GetTypeNameWithDeclaringTypename(candidateTypeSymbol);
                    codeGenContext.RPCContext.BackingRPCComponentName = codeGenContext.RPCContext.OriginalRPCComponentTypeName;
                    typeInfo = typeBuilder.BuildTypeInformation(candidateTypeSymbol, null);
                }

                IMethodSymbol candidateMethodSymbol = null;
                if (typeInfo == null)
                {
                    candidateMethodSymbol = model.GetDeclaredSymbol(syntaxNode as BaseMethodDeclarationSyntax) as IMethodSymbol;

                    if (candidateMethodSymbol != null)
                    {
                        candidateTypeSymbol = candidateMethodSymbol.ContainingType;
                        codeGenContext.ResetState();

                        // generated backing RPC component
                        codeGenContext.RPCContext.BackingRPCComponentName = NameUtils.GetRPCGeneratedTypeName(candidateTypeSymbol, candidateMethodSymbol);
                        codeGenContext.RPCContext.OriginalRPCComponentTypeName = null;
                        typeInfo = typeBuilder.BuildRPCMethodTypeInformation(codeGenContext,candidateMethodSymbol, null);
                    }
                }

                if (typeInfo == null)
                    continue;

                codeGenContext.types.Add(typeInfo);
                codeGenContext.diagnostic.LogInfo($"Generating RPC for ${typeInfo.TypeFullName}");
                NameUtils.UpdateNameAndNamespace(ref typeInfo, ref codeGenContext, candidateTypeSymbol, candidateMethodSymbol);
                
                CodeGenerator.GenerateRPC(codeGenContext, typeInfo);
            }

            codeGenContext.generatedNs = rootNamespace;
        }

        private static void GenerateRPCRegistrationSystem(IReadOnlyList<SyntaxNode> rpcCandidates, CodeGenerator.Context context)
        {
            //There is nothing to generate in that case. Skip creating an empty system
            if (context.types.Count == 0 || !ContainsRPCTypes(context.types))
                return;

            using (new Profiler.Auto("GenerateRPCRegistrationSystem"))
            {
                //Generate the ghost registration
                var registrationSystemCodeGen = context.codeGenCache.GetTemplate(CodeGenerator.RPCRegistrationSystem);
                registrationSystemCodeGen = registrationSystemCodeGen.Clone();
                var replacements = new Dictionary<string, string>(16);

                foreach (var t in context.types)
                {
                    if (t.IsAutoInvokeRPC)
                    {
                        replacements["RPC_NAME"] = t.TypeFullName;
                        registrationSystemCodeGen.GenerateFragment("RPC_AUTO_INVOKE_LIST", replacements);
                    }
                }

                if (context.generatedNs != null)
                {
                    replacements.Clear();
                    replacements["RPC_USING"] = context.generatedNs;
                    registrationSystemCodeGen.GenerateFragment("RPC_USING_STATEMENT", replacements);
                }

                replacements.Clear();
                replacements.Add("RPC_NAMESPACE", context.generatedNs != null ? context.generatedNs : "Unity.Netcode.Generated.RPCs");

                registrationSystemCodeGen.GenerateFile("RPCAutoInvokeCollection.cs", replacements, context.batch);
            }
        }

        private static bool ContainsRPCTypes(List<TypeInformation> types)
        {
            foreach (var t in types)
            {
                if ( t.IsAutoInvokeRPC )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
