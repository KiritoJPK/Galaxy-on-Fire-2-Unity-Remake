// CooldownView.cs
// Remake-only: the booster's and the cloak's recharge for keyboard and controller play, under the status bars next to the
// secondary weapon's plate. The original shows the boost charge only on its touch button (Hud::draw 18: the button's alpha)
// and the cloak's in the quick menu's entry (getCloakRechargeRate); with keys or a pad neither is on screen. Each is the
// equipment's own shop icon (GoF2Icons/item_NNN), darkened from the top while it recharges (booster: FlightModel
// BoostRechargePercent, dim while boosting; cloak: Cloak.ChargeRate / RechargeRate, dim while cloaked) and lit with a short
// flash once it is ready. Hidden in touch mode (the touch buttons show it) and without the equipment.

using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public sealed class CooldownView
    {
        const int BoosterCategory = 14;
        const float FlashSeconds = 0.6f;

        sealed class Slot
        {
            public VisualElement root, shade;
            public int item = -1;
            public bool wasReady = true;
            public float flashUntil;
        }

        readonly VisualElement row;
        readonly Slot boost, cloak;

        public CooldownView(VisualElement parent)
        {
            row = new VisualElement { name = "cooldowns", pickingMode = PickingMode.Ignore };
            row.AddToClassList("cooldowns");
            parent.Add(row);
            boost = Make();
            cloak = Make();
        }

        Slot Make()
        {
            var s = new Slot { root = new VisualElement { pickingMode = PickingMode.Ignore } };
            s.root.AddToClassList("cooldown");
            s.shade = new VisualElement { pickingMode = PickingMode.Ignore };
            s.shade.AddToClassList("cooldown-shade");
            s.root.Add(s.shade);
            row.Add(s.root);
            return s;
        }

        /// <summary>Each frame. 'shown' = keyboard / controller flight with the HUD up.</summary>
        public void Update(Database db, ShipController ship, PlayerCloak playerCloak, bool shown)
        {
            var model = ship != null ? ship.Model : null;
            var booster = shown && model != null && model.HasBooster && db != null ? Shop.FirstMounted(db, BoosterCategory) : null;
            var rules = shown && playerCloak != null ? playerCloak.Rules : null;
            Set(boost, booster != null ? booster.index : -1,
                model == null ? 1f : model.IsBoosting ? 0f : model.BoostRechargePercent, model != null && model.BoostReady);
            Set(cloak, rules != null ? rules.item : -1,
                rules == null ? 1f : rules.State == Cloak.Phase.Charging ? rules.ChargeRate : rules.Cloaked ? 0f : rules.RechargeRate,
                rules != null && rules.Available);
            row.style.display = booster != null || rules != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void Set(Slot s, int item, float charge, bool ready)
        {
            if (item < 0) { s.root.style.display = DisplayStyle.None; s.item = -1; return; }
            s.root.style.display = DisplayStyle.Flex;
            if (item != s.item)
            {
                s.item = item;
                var tex = ItemInfo.ItemIcon(item);
                s.root.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground();
                s.wasReady = ready;
            }
            s.shade.style.height = Length.Percent((1f - Mathf.Clamp01(charge)) * 100f);
            s.root.EnableInClassList("cooldown--waiting", !ready);
            if (ready && !s.wasReady) s.flashUntil = Time.unscaledTime + FlashSeconds;
            s.wasReady = ready;
            s.root.EnableInClassList("cooldown--flash", Time.unscaledTime < s.flashUntil);
        }
    }
}
