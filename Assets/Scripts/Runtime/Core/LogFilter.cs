// LogFilter.cs
// Drops known harmless log lines from the packages. URP's upscaler framework (UpscalerFramework) instantiates every upscaler
// on the URP asset's priority list whenever it creates its pipeline (Upscaling.cs, Activator.CreateInstance), and
// FSR4IUpscaler's constructor warns "FSR4 is not available on this device." on every GPU but AMD's newest: in the Editor
// and the players, on every pipeline creation. FSR 4 stays on the list (UrpStpGuard) for the GPUs that run it; the
// framework's own isSupportedOnDevice already hides it from the Upscaler option elsewhere.
// Installed as a wrapper around Debug.unityLogger's handler before the first scene (players) and on Editor load.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoF2Remake
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class LogFilter
    {
        static readonly string[] DroppedWarnings = { "FSR4 is not available on this device." };

        sealed class Handler : ILogHandler
        {
            readonly ILogHandler inner;
            public Handler(ILogHandler inner) { this.inner = inner; }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType == LogType.Warning && Dropped(format, args)) return;
                inner.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, Object context) => inner.LogException(exception, context);
        }

        static bool Dropped(string format, object[] args)
        {
            // Debug.LogWarning(message) arrives as "{0}" with the message as the one argument.
            string text = format == "{0}" && args != null && args.Length == 1 ? args[0] as string : format;
            if (text == null) return false;
            foreach (var d in DroppedWarnings) if (text == d) return true;
            return false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            var logger = Debug.unityLogger;
            if (logger.logHandler is Handler) return;   // no domain reload: already wrapped
            logger.logHandler = new Handler(logger.logHandler);
        }
    }
}
