using System;
using UnityEngine;

namespace Unity.Netcode.PhysicsInternalAccess
{
    internal static class PhysicsStaticInternalAccess
    {
        public static float GetWakeCounterInternal(this Rigidbody self)
        {
            return self.wakeCounter;
        }

        public static void SetWakeCounterInternal(this Rigidbody self, float value)
        {
            self.wakeCounter = value;
        }

        public static bool GetAutoWakeInternal(this Rigidbody self)
        {
            return self.autoWake;
        }

        public static void SetAutoWakeInternal(this Rigidbody self, bool value)
        {
            self.autoWake = value;
        }
    }
}
