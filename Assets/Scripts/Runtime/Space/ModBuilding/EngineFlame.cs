// EngineFlame.cs
// Remake mods: drives a mod ship's engine flames (materials with "flame", GoF2/ModEngineFlame; EVERSPACE 2's EngineExhaust
// sections): per renderer (a MaterialPropertyBlock on each flame submesh, so the ship's materials stay shared) the flame's
// backward direction in that renderer's object space (the ship root's -Z: the parts never turn against the root), and per
// frame the thrust (0..1) and boost (0..1) from EngineDrive (the player's throttle and boost, else the ship's measured
// speed; in the docked station capped at full throttle: a hangar flight's 200 m/s isn't a boost) and the engines' on / off
// level: on while the ship's engine glow part (AssembledObject.playerVariantParts) is active (dark and pulled into the
// nozzles on a ship parked in a hangar, while mining or docking at an object, on a dead ship), fading over a quarter
// second. Without a glow part the flames are always on.
// A customized exhaust (ModExhausts: the hangar's Customize screen, any ship) brings flames of its own under a holder
// (ModExhausts.HolderName) with an EngineFlame of its own: the ship's EngineFlame leaves them alone and, 'suppressed', keeps
// the model's own flames shrunk into the nozzles and dark. Each of the holder's nozzles (ModExhausts.NozzleName) blows along
// its own backward axis (the mount's turn), the model's own flames along the ship's.

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Visuals;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public class EngineFlame : MonoBehaviour
    {
        const float FadePerSecond = 4f;
        const float IdleThrust = 0.02f;   // a drive below this is idle: no flame
        const float Response = 6f;   // 1/s: how fast the flame follows the throttle

        static readonly int DirId = Shader.PropertyToID("_FlameDir");
        static readonly int ThrustId = Shader.PropertyToID("_FlameThrust");
        static readonly int BoostId = Shader.PropertyToID("_FlameBoost");
        static readonly int LevelId = Shader.PropertyToID("_FlameLevel");
        static readonly int OverrideId = Shader.PropertyToID("_BoostOverride");

        readonly List<(Renderer r, int index, Vector4 dir)> flames = new List<(Renderer, int, Vector4)>();
        /// <summary>The model's own flames stay off (a customized exhaust replaces them).</summary>
        public bool suppressed;
        /// <summary>A boost turns the main flames to the boost colour and the boost cards stay hidden (an exhaust style on a ship
        /// without boost jets of its own: the game's ships, other mods'; ModExhausts).</summary>
        public bool boostOverride;
        /// <summary>Meshes made for this ship (a customized exhaust's flames bent to its nozzles), freed with it.</summary>
        public readonly List<Mesh> ownedMeshes = new List<Mesh>();

        void OnDestroy()
        {
            foreach (var m in ownedMeshes) if (m != null) Destroy(m);
        }
        MaterialPropertyBlock block;
        EngineDrive drive;
        GameObject engineGlow;
        bool ready, inStation;
        float thrust = -1f, boost, level = -1f;

        /// <summary>Adds the component to a ship template (once).</summary>
        public static void Add(GameObject ship)
        {
            if (ship != null && ship.GetComponent<EngineFlame>() == null) ship.AddComponent<EngineFlame>();
        }

        /// <summary>Whether a material is a flame (a copy of the flame template, also a colour slot's recoloured copy).</summary>
        public static bool IsFlame(Material m) => m != null && m.shader != null && m.shader.name == "GoF2/ModEngineFlame";

        void OnEnable()
        {
            drive?.Reset();
            inStation = FindAnyObjectByType<World.StationLevel>() != null;
        }

        void Setup()
        {
            ready = true;
            block = new MaterialPropertyBlock();
            drive = new EngineDrive(transform);
            var asm = GetComponent<AssembledObject>();
            if (asm != null && asm.playerVariantParts != null)
                engineGlow = asm.playerVariantParts.FirstOrDefault(p => p != null && p.name.Contains("engine_glow"));
            bool holder = name == ModExhausts.HolderName;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!IsFlame(mats[i]) || r.name.StartsWith("engine_glow_thruster")) continue;   // LateralThrusters drives its own
                    if (!holder && ModExhausts.InHolder(r.transform)) continue;                     // a customized exhaust's own
                    var nozzle = holder ? ModExhausts.NozzleOf(r.transform) : null;
                    var back = nozzle != null ? -nozzle.forward : -transform.forward;
                    var d = r.transform.InverseTransformDirection(back).normalized;
                    flames.Add((r, i, new Vector4(d.x, d.y, d.z, 0f)));
                    // The stretched flame reaches past the mesh's bounds: grow them so it isn't culled with its stub.
                    float reach = mats[i].GetFloat("_Length") * (1f + mats[i].GetFloat("_BoostLength"));
                    var bounds = r.localBounds;
                    bounds.Encapsulate(bounds.min + d * reach);
                    bounds.Encapsulate(bounds.max + d * reach);
                    r.localBounds = bounds;
                }
            }
        }

        bool On => engineGlow == null || engineGlow.activeInHierarchy;

        void LateUpdate()
        {
            if (!ready) Setup();
            if (flames.Count == 0) return;
            float dt = Time.deltaTime;
            float d = drive.Sample(dt, engineGlow);
            if (inStation && !drive.Previewing) d = Mathf.Min(d, 1f);
            float t = Mathf.Clamp01(d), b = Mathf.Clamp01(d - 1f);
            if (thrust < 0f) { thrust = t; boost = b; }
            else
            {
                float k = 1f - Mathf.Exp(-dt * Response);
                thrust = Mathf.Lerp(thrust, t, k);
                boost = Mathf.Lerp(boost, b, k);
            }
            // Off (free flight with no thrust) or idle (no thrust, the throttle at 0 or a ship at rest): the flames shrink
            // into the nozzles, as EVERSPACE 2's do (they kept a short flame at idle before).
            float target = On && !drive.Off && !suppressed && d > IdleThrust ? 1f : 0f;
            level = level < 0f ? target : Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * FadePerSecond);
            foreach (var (r, index, dir) in flames)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block, index);
                block.SetVector(DirId, dir);
                block.SetFloat(ThrustId, thrust);
                block.SetFloat(BoostId, boost);
                block.SetFloat(LevelId, level);
                block.SetFloat(OverrideId, boostOverride ? 1f : 0f);
                r.SetPropertyBlock(block, index);
            }
        }
    }
}
