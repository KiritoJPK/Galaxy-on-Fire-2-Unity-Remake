// PipelineReloadGuard.cs  (Editor only)
// The Unity CLI bridge (com.unity.pipeline) starts its HTTP server from a static constructor after every code reload, but
// doesn't stop the old one before the reload (PipelineServerStartup.OnBeforeAssemblyReload is empty). Under Unity 7's code
// reload the old HttpListener's http.sys registration outlives it, so every recompile kept one port of 7800-7849 for
// good, and after ~50 recompiles the bridge failed with "No available ports in range 7800-7849" until the Editor was
// restarted. This stops the server just before each reload (through reflection: the package's class is internal), so
// the next one takes a free port again.

using System;
using UnityEditor;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    [InitializeOnLoad]
    static class PipelineReloadGuard
    {
        static PipelineReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= StopServer;
            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        }

        static void StopServer()
        {
            try
            {
                var type = Type.GetType("Unity.Pipeline.Editor.PipelineServerStartup, Unity.Pipeline.Editor");
                type?.GetMethod("StopServer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
            }
            catch (Exception e) { Debug.LogWarning($"PipelineReloadGuard: couldn't stop the pipeline server ({e.Message})."); }
        }
    }
}
