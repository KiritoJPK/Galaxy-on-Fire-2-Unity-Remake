// MiningGame.cs
// The ore-mining minigame (MiningGame 0x143994, update 0x143e28) as plain C#: concentric rock layers (one per quality
// step: A 7, B 6, C 5, D 4), a drill cursor the player keeps inside the current layer while it drifts randomly.
// Research: Reference/research/mining.md section 4. Positions are HD screen pixels relative to the centre (+y down),
// like the original's layout; the view scales them to the panel.
//   inside the layer:  ore += yield * ((layer + 1) / 7 * 2.35 + 0.15) t/s, the layer timer runs (6 s per layer)
//   outside:           no ore, no progress, a 2.5 s energy budget for the whole session drains; empty = lost, no ore
//   drift:             re-rolled every 0.5..2.5 s (first after 2.5 s) at +-(0.5..1.1) / steadiness, x0.3 on the core layer
//   input:             +-3 * v^2 per axis (MiningGame::left/right/up/down), movement dt * (input + drift) / 10 px
// All layers done = won (class A also yields a core). Deviation: releasing the stick stops the player's part of the
// movement (the original keeps the last non-zero input, mining.md 4.4).

using UnityEngine;

namespace GoF2Remake.Flight
{
    public class MiningGame
    {
        /// <summary>LAYER_SPEEDS 0x26ad2c: drill animation / depth strip speed / drill sound parameter per layer.</summary>
        public static readonly float[] LayerSpeeds = { 5, 8, 12, 17, 23, 30, 38 };

        /// <summary>LAYER_DIAMETERS 0x26ad48 row 7 - quality; on the HD layout the value is the disc radius in px.</summary>
        static readonly int[][] LayerRadii =
        {
            new[] { 250, 210, 170, 140, 110, 80, 50 },   // A
            new[] { 250, 210, 170, 140, 110, 80 },       // B
            new[] { 250, 200, 150, 100, 70 },            // C
            new[] { 250, 170, 120, 80 },                 // D
        };

        public const float LayerMs = 6000f;
        public const float EnergyMs = 2500f;
        const float PixelsPerUnit = 10f;   // Layout+0xe4 on the HD retina layout

        public readonly int Quality, OreItem;
        public readonly float Yield, Steadiness;
        readonly bool tutorial;

        public Vector2 Drill { get; private set; }         // px from the centre, +y down
        public int Layer { get; private set; }
        public float LayerTime { get; private set; }
        public float OutsideMs { get; private set; }
        public float Ore { get; private set; }
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool GotCore { get; private set; }
        public bool Inside { get; private set; } = true;
        public bool HasCore => Quality == 7;
        public int OreAmount => (int)Ore;
        public float Energy01 => 1f - OutsideMs / EnergyMs;
        public int LayerCount => Quality;
        public int RadiusOf(int layer) => LayerRadii[Mathf.Clamp(7 - Quality, 0, 3)][Mathf.Clamp(layer, 0, Quality - 1)];

        /// <summary>Fires on every new whole ton (the view flashes the amount in).</summary>
        public event System.Action NewTon;
        /// <summary>The drill left or re-entered the layer (sounds 3 / 1).</summary>
        public event System.Action<bool> InsideChanged;

        Vector2 input, drift;
        float driftTimer;

        /// <param name="drill">The first mounted mining laser (attr 32 handling, attr 33 yield).</param>
        public MiningGame(int quality, int oreItem, int drillHandling, int drillYield, bool tutorial = false)
        {
            Quality = Mathf.Clamp(quality, 4, 7);
            OreItem = oreItem;
            Yield = drillYield / 100f;
            Steadiness = drillHandling / 100f * 1.5f + 0.3f;
            this.tutorial = tutorial;
        }

        /// <summary>Stick / keys, each axis -1..1 (+y = down on screen). The original squares it: 3 * v^2 with the sign.</summary>
        /// <summary>MiningGame::right / left / up / down: input = 3 x the value as the caller shaped it (the touch stick and the
        /// keys / pad squared per axis like Hud::getAnalog, the PC mouse linear). Squared here as well, the touch stick ran at
        /// 3 x raw^4: a quarter of the speed at half deflection.</summary>
        public void SetInput(Vector2 v) => input = 3f * v;

        public bool IsInCurrentLayer() => Drill.magnitude < RadiusOf(Layer);

        /// <summary>Ore in t/s while drilling layer 'layer' (0 = the outer one) with 'yield' (attr 33 / 100); the mining
        /// beam's extraction uses it too (MiningBeamExtraction).</summary>
        public static float OreRate(int layer, float yield) => yield * ((layer + 1) / 7f * 2.35f + 0.15f);

        /// <summary>MiningGame::update: returns false when the game is over (won or lost).</summary>
        public bool Update(float dtMs)
        {
            if (Won || Lost) return false;
            bool wasInside = IsInCurrentLayer();
            driftTimer += dtMs;
            if (driftTimer > 2500f)
            {
                driftTimer = Random.Range(0, 2000) + 500;
                drift.x = (Random.Range(0, 2) == 0 ? 1f : -1f) * (Random.Range(0, 7) + 5) / 10f / Steadiness;
                drift.y = (Random.Range(0, 2) == 0 ? 1f : -1f) * (Random.Range(0, 7) + 5) / 10f / Steadiness;
                if (HasCore && Layer == Quality - 1) drift *= 0.3f;   // the core layer is calmer
            }
            if (tutorial && !IsInCurrentLayer()) drift = -Drill * 0.03f;   // campaign <= 4: spring back to the centre
            Drill += dtMs * (input + drift) / PixelsPerUnit;

            Inside = IsInCurrentLayer();
            if (Inside != wasInside) InsideChanged?.Invoke(Inside);
            if (!Inside)
            {
                OutsideMs += dtMs;
                if (OutsideMs > EnergyMs) { OutsideMs = EnergyMs; Ore = 0f; Lost = true; return false; }
                return true;
            }

            float old = Ore;
            Ore += dtMs * OreRate(Layer, Yield) / 1000f;
            if ((int)Ore > (int)old) NewTon?.Invoke();
            LayerTime += dtMs;
            if (LayerTime > LayerMs)
            {
                LayerTime = 0f;
                Layer++;
                if (Layer >= Quality)
                {
                    Layer = Quality - 1;
                    Won = true;
                    GotCore = HasCore;
                    return false;
                }
            }
            return true;
        }
    }
}
