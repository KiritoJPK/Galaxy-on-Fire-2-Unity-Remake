// ControllerGyro.cs
// Remake: motion steering with a PC controller (DualSense, DualShock 4, Switch Pro Controller, Joy-Cons) through Jibb Smart's
// JoyShockLibrary 3.0 (MIT, Assets/Plugins/JoyShockLibrary: the Windows x64 DLL; Unity's Input System doesn't expose these
// controllers' motion sensors). Tilt steering from the accelerometer (gravity), like the phone's TiltSteering: tilting the
// controller forward / back pitches, rolling it like a steering wheel steers sideways, by the angle from its rest pose
// (taken when it connects and on Level out); a full offset at 20 deg / sensitivity, a 2 deg dead zone. Gravity can't drift:
// the first version integrated the gyro's turn rate, and the uncalibrated bias walked the pitch up and down while the
// controller lay still (#35). The offset adds to the stick, it never replaces it. With Steam Input (or DS4Windows) on for
// the game, that owns the controller and only a virtual pad without motion reaches the game; an accelerometer that reads
// no gravity is logged once. Windows only (the DLL); elsewhere Supported is false. Plain C#: ShipController polls it.

using System.Runtime.InteropServices;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ControllerGyro
    {
        /// <summary>Degrees of controller tilt for a full steering offset at sensitivity 1.</summary>
        const float FullDeflectionDegrees = 20f;
        /// <summary>Tilt below this many degrees steers nothing (a hand never holds perfectly still).</summary>
        const float DeadZoneDegrees = 2f;
        /// <summary>Low-pass time constant of the accelerometer, seconds (hand shake and the controller's own jolts).</summary>
        const float SmoothSeconds = 0.08f;
        const float RescanSeconds = 3f;

        static readonly int[] handles = new int[8];
        static int device = -1;
        static float nextScan;
        static System.Threading.Tasks.Task<int> scan;   // a device scan in progress
        static bool failed, quitHooked, recenterPending = true, gravityWarned;
        static Vector3 gravity, rest;

        /// <summary>The steering offset, -1..1 per axis (x right, y up).</summary>
        public static Vector2 Offset { get; private set; }

        public static bool Supported =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            !failed;
#else
            false;
#endif

        /// <summary>A motion controller is connected (looked for every few seconds while the option is on).</summary>
        public static bool Connected => device >= 0;

        /// <summary>Level out (and a new connection): the controller's current pose becomes the centre.</summary>
        public static void Recenter()
        {
            Offset = Vector2.zero;
            recenterPending = true;
        }

        /// <summary>Once a frame while flying: the steering from the controller's tilt (zero when the option is off, no
        /// controller is there or it reports no motion).</summary>
        public static Vector2 Steer(float dt)
        {
            if (!Settings.GyroSteering || !Supported) { Offset = Vector2.zero; return Vector2.zero; }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (device >= 0 && !JslStillConnected(device)) device = -1;
                if (device < 0)
                {
                    // The device scan (JslConnectDevices, HID enumeration) blocks for tens to hundreds of ms: on a worker
                    // thread (#37: every 3 s while steering by hand without a motion controller, the main thread stalled,
                    // a stutter the FPS counter's average hid; the autopilot skips the gyro, so it never stuttered).
                    if (scan == null)
                    {
                        if (Time.unscaledTime < nextScan) return Offset = Vector2.zero;
                        nextScan = Time.unscaledTime + RescanSeconds;
                        if (!quitHooked) { quitHooked = true; Application.quitting += () => { try { JslDisconnectAndDisposeAll(); } catch { } }; }
                        scan = System.Threading.Tasks.Task.Run(() =>
                        {
                            if (JslConnectDevices() <= 0) return -1;
                            JslGetConnectedDeviceHandles(handles, handles.Length);
                            return handles[0];
                        });
                        return Offset = Vector2.zero;
                    }
                    if (!scan.IsCompleted) return Offset = Vector2.zero;
                    var done = scan;
                    scan = null;
                    if (done.IsFaulted) throw done.Exception?.InnerException ?? done.Exception;
                    if (done.Result < 0) return Offset = Vector2.zero;
                    device = done.Result;
                    JslSetAutomaticCalibration(device, true);
                    Debug.Log($"ControllerGyro: controller {device} connected (type {JslGetControllerType(device)})");
                    gravity = Vector3.zero;
                    recenterPending = true;
                }
                // The accelerometer in g, gravity included (sign conventions differ between controllers: every angle below is
                // taken relative to the rest pose, which cancels them).
                var a = new Vector3(JslGetAccelX(device), JslGetAccelY(device), JslGetAccelZ(device));
                gravity = gravity == Vector3.zero ? a : Vector3.Lerp(gravity, a, dt <= 0f ? 0f : 1f - Mathf.Exp(-dt / SmoothSeconds));
                float g = gravity.magnitude;
                if (g < 0.5f || g > 1.6f)
                {
                    // No sensible gravity: Steam Input / DS4Windows holding the controller, or the motion reports not on.
                    if (!gravityWarned) { gravityWarned = true; Debug.LogWarning($"ControllerGyro: the accelerometer reads {a} (|g| {g:0.00}): no motion data"); }
                    return Offset = Vector2.zero;
                }
                if (recenterPending) { rest = gravity; recenterPending = false; }
                // X right, Y out of the face, Z toward the player. Pitch: the angle about X in the Y-Z plane (+ = the far edge
                // up; relative to the rest pose, so it holds for "gravity" and "reaction" readings alike). Roll: how far gravity
                // leans out of that plane, toward X (+ = the left side down), at any hold angle (a controller is held tilted
                // toward the player); the reading's sign convention from the rest pose's Y (the face points up in a grip).
                float pitch = Mathf.DeltaAngle(Mathf.Atan2(-rest.z, rest.y) * Mathf.Rad2Deg, Mathf.Atan2(-gravity.z, gravity.y) * Mathf.Rad2Deg);
                float sign = rest.y >= 0f ? 1f : -1f;
                float Lean(Vector3 v) => Mathf.Atan2(sign * v.x, new Vector2(v.y, v.z).magnitude) * Mathf.Rad2Deg;
                float roll = Lean(gravity) - Lean(rest);
                float k = Mathf.Max(0.05f, Settings.GyroSensitivity) / FullDeflectionDegrees;
                float Axis(float deg) => Mathf.Clamp(Mathf.Sign(deg) * Mathf.Max(0f, Mathf.Abs(deg) - DeadZoneDegrees) * k, -1f, 1f);
                Offset = new Vector2(Axis(-roll), Axis(pitch));   // rolling right (the right side down) steers right
                return Offset;
            }
            catch (System.Exception e)
            {
                // The DLL missing or not loadable (a 32-bit or non-Windows player): off for this session.
                failed = true;
                device = -1;
                Debug.LogWarning("ControllerGyro: JoyShockLibrary unavailable: " + e.Message);
                return Offset = Vector2.zero;
            }
#else
            return Vector2.zero;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const string Dll = "JoyShockLibrary";
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int JslConnectDevices();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int JslGetConnectedDeviceHandles(int[] deviceHandleArray, int size);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void JslDisconnectAndDisposeAll();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] static extern bool JslStillConnected(int deviceId);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern float JslGetAccelX(int deviceId);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern float JslGetAccelY(int deviceId);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern float JslGetAccelZ(int deviceId);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void JslSetAutomaticCalibration(int deviceId, [MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int JslGetControllerType(int deviceId);
#endif
    }
}
