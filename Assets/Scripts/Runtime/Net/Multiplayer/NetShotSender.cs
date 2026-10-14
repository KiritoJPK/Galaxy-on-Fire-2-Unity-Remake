// NetShotSender.cs
// Multiplayer: listens to a shooter's guns (Gun.Fired, Gun.Ignited) and hands each shot and blast to its NetworkBehaviour
// (NetProxy for a host NPC, NetPlayer for the local player), which sends it to the others' NetShotMirror. Not sent: the
// sentry guns' deploy "bullet" and the plasma collectors (neither is a shot). Guns can change (an NPC's second slot, a new
// turret): Hook adds the new ones, Unhook drops them all.

using System;
using System.Collections.Generic;
using GoF2Remake.Flight;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    public sealed class NetShotSender
    {
        /// <summary>(item, position, velocity, up, life left ms, homing delay ms, target id); beams: start point and unit direction.</summary>
        public delegate void ShotHandler(int item, Vector3 position, Vector3 velocity, Vector3 up, float lifetimeMs, float homingDelayMs, ulong target);

        readonly Dictionary<Gun, (Action<Gun, int> fired, Action<Vector3> ignited)> hooked = new Dictionary<Gun, (Action<Gun, int>, Action<Vector3>)>();
        readonly ShotHandler onShot;
        readonly Action<int, Vector3> onBlast;
        readonly Func<Target> lockTarget;

        public NetShotSender(ShotHandler onShot, Action<int, Vector3> onBlast, Func<Target> lockTarget)
        {
            this.onShot = onShot;
            this.onBlast = onBlast;
            this.lockTarget = lockTarget;
        }

        public void Hook(IEnumerable<Gun> guns)
        {
            if (guns == null) return;
            foreach (var gun in guns)
            {
                if (gun == null || hooked.ContainsKey(gun) || gun.kind == Gun.Kind.Sentry || gun.kind == Gun.Kind.PlasmaCollector) continue;
                Action<Gun, int> fired = OnFired;
                var g = gun;
                Action<Vector3> ignited = point => onBlast(g.itemIndex, point);
                gun.Fired += fired;
                gun.Ignited += ignited;
                hooked[gun] = (fired, ignited);
            }
        }

        public void Unhook()
        {
            foreach (var pair in hooked) { pair.Key.Fired -= pair.Value.fired; pair.Key.Ignited -= pair.Value.ignited; }
            hooked.Clear();
        }

        void OnFired(Gun gun, int index)
        {
            var b = gun.bullets[index];
            var position = gun.isBeam ? b.position - gun.BeamDir * gun.BeamLengthUnits * Gun.MetersPerUnit : b.position;
            var velocity = gun.isBeam ? gun.BeamDir : b.velocity;
            // A beam's target is the one it hit (its bullet sits on it); homing weapons follow the gun's own lock (a capital
            // ship's missiles have their own target), else the shooter's.
            var target = gun.isBeam ? TargetAt(b.position) : gun.Homing || gun.kind == Gun.Kind.Rocket ? gun.LastLock ?? lockTarget?.Invoke() : null;
            onShot(gun.itemIndex, position, velocity, b.up, b.timer, gun.homingDelayMs, NetShots.TargetId(target));
        }

        static Target TargetAt(Vector3 point)
        {
            foreach (var t in Target.All)
                if (t != null && (t.transform.position - point).sqrMagnitude < 0.01f) return t;
            return null;
        }
    }
}
