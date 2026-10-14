// TiltSteering.cs
// Accelerometer steering (options[0x11] = 0), MGame::handleAccelerometer 0x1a7c1c, run every frame from MGame::OnUpdate
// (not at campaign 48). The original's values (AccelerometerManager -> ToJNI.handleAccelerometer -> Engine::SetAccelValue,
// unfiltered): X = the screen-up reaction (about +0.98 held upright, 0 flat), Y = along screen-left, Z about -0.98 face up.
//   yaw    v = clamp(Y * 2.5, -1, 1): right(v^2) / left(v^2) (full turn at |Y| = 0.4, about 24 deg), not calibrated
//   pitch  x' = X, or 2 - min(X, 1) once Z > 0 (past vertical); invert (options[0x10], default on): p1 = x' - cal1,
//          p2 = Z - cal2, else the other way; v = 3 * (the larger of p1 / p2), clamped +-1: up(v^2) / down(v^2)
//   no dead zone (the square is the only soft centre); the flight model then ramps with the tilt sensitivity and the
//   pitch factors 1.45 (down) / 1.25 (up) (PlayerEgo::down / up in tilt mode)
// Calibration (492 -> 493, MenuTouchWindow::OnTouchEnd): cal1 = X (folded to 2 - X when Z > 0), cal2 = Z.
// Unity: Accelerometer.current in g, gravity's direction, compensated for the screen orientation (both landscapes work):
// X = -a.y, Y = a.x, Z = a.z (x 0.981 for the original's /10 of m/s^2). Plain C#.

using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoF2Remake.Flight
{
    public static class TiltSteering
    {
        public static bool Available => Accelerometer.current != null;

        /// <summary>Tilt steering is chosen and there is a sensor.</summary>
        public static bool Active => Settings.TiltSteering && Available;

        /// <summary>The sensor in the original's axes (X up the screen, Y along screen-left, Z out of the back).</summary>
        public static Vector3 Read()
        {
            var acc = Accelerometer.current;
            if (acc == null) return Vector3.zero;
            if (!acc.enabled) { InputSystem.EnableDevice(acc); acc.samplingFrequency = 60f; }
            var a = acc.acceleration.ReadValue() * 0.981f;
            return new Vector3(-a.y, a.x, a.z);
        }

        public static void Calibrate()
        {
            var v = Read();
            Settings.TiltCalX = v.z > 0f ? 2f - Mathf.Min(v.x, 1f) : v.x;
            Settings.TiltCalZ = v.z;
            Settings.TiltCalibrated = true;
        }

        /// <summary>The stick this frame (x right, y up) from the device's tilt, both axes squared like the original's
        /// left / right / up / down amounts.</summary>
        public static Vector2 Steer(Vector3 v, float calX, float calZ, bool invert)
        {
            float yaw = Mathf.Clamp(v.y * 2.5f, -1f, 1f);
            float x = v.z > 0f ? 2f - Mathf.Min(v.x, 1f) : v.x;
            float p1 = invert ? x - calX : calX - x;
            float p2 = invert ? v.z - calZ : calZ - v.z;
            float pitch = Mathf.Clamp(3f * (Mathf.Abs(p1) >= Mathf.Abs(p2) ? p1 : p2), -1f, 1f);
            return new Vector2(Mathf.Sign(yaw) * yaw * yaw, Mathf.Sign(pitch) * pitch * pitch);
        }

        public static Vector2 Steer() => Steer(Read(), Settings.TiltCalX, Settings.TiltCalZ, true);
    }
}
