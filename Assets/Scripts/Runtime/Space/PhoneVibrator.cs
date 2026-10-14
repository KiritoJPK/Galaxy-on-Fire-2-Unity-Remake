// PhoneVibrator.cs
// Remake-only: the phone's vibration motor (Android) for Haptics, through the platform's Vibrator service over JNI:
//   API 26+  VibrationEffect.createOneShot(ms, amplitude 1..255) when the motor has amplitude control (most phones with a
//            linear motor), else createOneShot(ms, DEFAULT_AMPLITUDE) with the length scaled down for weaker pulses
//   older    Vibrator.vibrate(ms), the length scaled the same way
// A new one-shot replaces the one running, so a weaker pulse never cuts a stronger one short. The VIBRATE permission is
// added to the Gradle project's manifest by AndroidVibratePermission (Editor). Plain C#; elsewhere Supported is false.

using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class PhoneVibrator
    {
        /// <summary>Below this amplitude a motor without amplitude control stays still (it can only buzz at full strength).</summary>
        const float FixedMotorMinimum = 0.2f;
        /// <summary>Shortest one-shot sent: shorter ones don't spin a rotating-mass motor up at all.</summary>
        const float MinimumMs = 8f;

        static bool initialised, supported, amplitudeControl;
        static float busyUntil, busyAmplitude;
#if UNITY_ANDROID && !UNITY_EDITOR
        static int sdk;
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effects;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            busyUntil = 0f;
            busyAmplitude = 0f;
        }

        /// <summary>The device has a vibration motor this code can drive (Android only).</summary>
        public static bool Supported
        {
            get
            {
                if (!initialised) Init();
                return supported;
            }
        }

        /// <summary>The motor can vibrate at different strengths (else Pulse shortens weak pulses instead).</summary>
        public static bool AmplitudeControl => Supported && amplitudeControl;

        static void Init()
        {
            initialised = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                sdk = version.GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                // "vibrator" (Context.VIBRATOR_SERVICE): deprecated from API 31 for VibratorManager, still the default vibrator.
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                supported = vibrator != null && vibrator.Call<bool>("hasVibrator");
                if (supported && sdk >= 26)
                {
                    amplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                    effects = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (System.Exception e)
            {
                supported = false;
                Debug.LogWarning("PhoneVibrator: no vibration (" + e.Message + ")");
            }
#else
            supported = amplitudeControl = false;
#endif
        }

        /// <summary>A one-shot vibration of 'ms' at 'amplitude' (0..1, the intensity option already applied).</summary>
        public static void Pulse(float ms, float amplitude)
        {
            amplitude = Mathf.Clamp01(amplitude);
            if (ms <= 0f || amplitude <= 0.02f || !Supported) return;
            float now = Time.unscaledTime;
            if (now < busyUntil && amplitude < busyAmplitude) return;   // a stronger one is still running
            if (!amplitudeControl)
            {
                if (amplitude < FixedMotorMinimum) return;
                ms *= Mathf.Lerp(0.35f, 1f, amplitude);   // full-strength motor: a shorter buzz feels weaker
            }
            ms = Mathf.Max(MinimumMs, ms);
            busyUntil = now + ms / 1000f;
            busyAmplitude = amplitude;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                long length = (long)ms;
                if (sdk >= 26 && effects != null)
                {
                    // VibrationEffect.DEFAULT_AMPLITUDE = -1: the motor's own strength.
                    int amp = amplitudeControl ? Mathf.Clamp(Mathf.RoundToInt(amplitude * 255f), 1, 255) : -1;
                    using var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", length, amp);
                    vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", length);
            }
            catch (System.Exception e)
            {
                supported = false;   // don't try again every frame
                Debug.LogWarning("PhoneVibrator: vibrate failed (" + e.Message + ")");
            }
#endif
        }

        /// <summary>Stops the vibration running (pause, focus lost, quitting).</summary>
        public static void Cancel()
        {
            busyUntil = 0f;
            busyAmplitude = 0f;
            if (!initialised || !supported) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { vibrator.Call("cancel"); }
            catch (System.Exception) { }
#endif
        }
    }
}
