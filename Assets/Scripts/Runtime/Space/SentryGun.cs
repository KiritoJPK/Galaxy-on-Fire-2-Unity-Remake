// SentryGun.cs
// A deployed sentry gun (items 211-213, sort 39; Reference/research/weapons_special.md 5.2):
//   Level::createSentryGuns 0xcb0d8   the objects: sn_sentry_gun_00X (scale 0.5), Player(radius 1000 -> 800, 100 HP),
//                                     always friend, name 1666 "Turret"; at most 3 deployed (all types together)
//   SentryGun::update 0x188942        placed where the deploy shot spawned (ship + R * (mount + (0, 0, 100)))
//   PlayerTurret::update (sentry)     invulnerable for 3000 ms, no lifetime; every 3000 ms (first after 3 s) the nearest
//                                     ship hostile to the player within 50000 units; yaw and pitch at 2 pi / 4096 rad per
//                                     ms toward its position + heading * 1500 (a), no pitch limit; fire when aligned
//   Level::assignGuns 0xcb638         its gun: 4 bullets with the sentry item's stats (attr 9 / 11 / 12 / 13), the look of
//                                     item 2 / 20 / 14 (projectile_002 / _021 / _014), muzzle (0, 0, 250) (213: 300);
//                                     kills are "by NPC" (no kill credit)
//   death                             sound 22, a type-0 explosion with fire streaks, gone after 4500 ms, the slot freed
// Hostile NPCs add it to their target list (it is a KIPlayer of the level).

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.World;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class SentryGun : MonoBehaviour
    {
        const float M = Gun.MetersPerUnit;
        const float InvulnerableMs = 3000f, PickMs = 3000f, RangeUnits = 50000f, LeadUnits = 1500f, GoneMs = 4500f;
        public const int MaxActive = 3;

        /// <summary>Level+0x6c: sentries deployed and not yet gone.</summary>
        public static int ActiveCount { get; private set; }

        Target self;
        /// <summary>The sentry's hittable object (its HUD marker: CombatView).</summary>
        public Target Target => self;
        Gun gun;
        GunRig rig;
        TurretAim aim;
        Transform muzzle, fxRoot;
        WeaponFx lookFx;
        Target target;
        float ageMs, pickMs, deadMs = -1f, sinceShotMs = 1e9f;
        Visuals.PartAnimation[] anims = new Visuals.PartAnimation[0];

        public static bool CanDeploy => ActiveCount < MaxActive;
        /// <summary>The deployed sentries (multiplayer: their shots are shown to the others, NetPlayer).</summary>
        public static readonly List<SentryGun> All = new List<SentryGun>();
        public Gun Gun => gun;

        /// <summary>Deploys a sentry of 'item' at 'position' (world), facing 'forward'.</summary>
        public static SentryGun Deploy(Database db, int item, Vector3 position, Quaternion rotation, Traffic traffic)
        {
            var it = db.Item(item);
            var fx = WeaponFx.Load(item);
            if (it == null || !CanDeploy) return null;
            var go = new GameObject($"Sentry gun {item}");
            go.transform.SetPositionAndRotation(position, rotation);
            var s = go.AddComponent<SentryGun>();
            s.Setup(db, it, fx, traffic);
            ActiveCount++;
            All.Add(s);
            return s;
        }

        void Setup(Database db, ItemData item, WeaponFx fx, Traffic traffic)
        {
            // The deployed object and its turning parts: the whole model yaws and pitches (no separate base).
            var yawNode = new GameObject("yaw").transform;
            yawNode.SetParent(transform, false);
            var pitchNode = new GameObject("gun").transform;
            pitchNode.SetParent(yawNode, false);
            if (fx != null && fx.sentry != null)
            {
                var model = Instantiate(fx.sentry, pitchNode, false);
                GunRig.StripForFx(model);
                anims = model.GetComponentsInChildren<Visuals.PartAnimation>(true);
                foreach (var a in anims) { a.loop = true; a.speed = 0f; }   // animated only while it fires
            }
            aim = new TurretAim(yawNode, pitchNode) { yawRatePerMs = TurretAim.RadPerMs, pitchRatePerMs = TurretAim.RadPerMs };

            self = gameObject.AddComponent<Target>();
            self.isShip = true;
            self.race = 0;
            self.friendToPlayer = true;
            self.radius = 800f * M;
            self.hitpoints = new Hitpoints(100);
            self.hp = self.maxHp = 100;
            self.customDeath = true;
            self.invulnerable = true;
            self.displayName = Localization.Get(1666);
            self.Died += OnDied;

            int look = item.Look == 211 ? 2 : item.Look == 212 ? 20 : 14;
            var lookItem = db.Item(look);
            lookFx = WeaponFx.Load(look);
            gun = new Gun(lookItem, item.Attr(9), item.Attr(11, 430), 4, item.Attr(12, 1000), item.Attr(13, 22)) { owner = self, Ignores = t => t.playerProof };
            muzzle = new GameObject("muzzle").transform;
            muzzle.SetParent(pitchNode, false);
            muzzle.localPosition = new Vector3(0f, 0f, item.Look == 213 ? 300f : 250f) * M;
            fxRoot = new GameObject("Sentry fx").transform;
            rig = new GunRig(gun, lookFx, fxRoot, muzzle);
            gun.Hit += (b, t, p) => { t.Damage(gun.damage, true, gun.bullets[b].velocity); rig.ShowImpact(p); };

            // A KIPlayer of the level: the hostile ships may shoot at it.
            if (traffic != null) foreach (var s in traffic.Ships) if (s.Target.hostileToPlayer && !s.enemies.Contains(self)) s.enemies.Add(self);
            if (fx != null && fx.shot != null) Sfx.PlayAt(fx.Shot, transform.position);   // deploy sound 2263 SentryGun_SG400 (all three)
        }

        void Update()
        {
            float dtMs = Time.deltaTime * 1000f;
            if (dtMs <= 0f) return;
            if (deadMs >= 0f)
            {
                deadMs += dtMs;
                if (deadMs >= GoneMs) Destroy(gameObject);
                return;
            }
            ageMs += dtMs;
            sinceShotMs += dtMs;
            float animSpeed = sinceShotMs <= gun.reloadMs + 50f ? 1f : 0f;
            foreach (var a in anims) if (a != null) a.speed = animSpeed;
            if (ageMs >= InvulnerableMs) self.invulnerable = false;
            gun.Update(dtMs, Target.All, null);
            rig.UpdateVisuals(dtMs, Camera.main, aim.BarrelForward);
            if (ageMs < InvulnerableMs) return;
            pickMs += dtMs;
            if (pickMs >= PickMs || target == null || !target.Alive || !target.isActiveAndEnabled || target.cloaked) { pickMs = 0f; target = Pick(); }
            if (target == null) return;
            if (aim.Step(target.transform.position + target.transform.forward * LeadUnits * M, dtMs))
            {
                int b = gun.TryFire(muzzle.position, Quaternion.LookRotation(aim.BarrelForward, aim.BarrelUp), false);
                if (b >= 0)
                {
                    rig.OnShot();
                    sinceShotMs = 0f;
                    if (lookFx != null && lookFx.shot != null) Sfx.PlayAt(lookFx.Shot, transform.position, 0.6f);
                }
            }
        }

        Target Pick()
        {
            Target best = null;
            float bestD = RangeUnits * M;
            foreach (var t in Target.All)
            {
                if (t == null || !t.isShip || !t.hostileToPlayer || !t.Alive || t.untargetable || t.cloaked || !t.isActiveAndEnabled) continue;
                float d = (t.transform.position - transform.position).magnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        void OnDied(Target t)
        {
            deadMs = 0f;
            rig.HideAll();
            var assets = CombatAssets.Load();
            Explosion.Spawn(0, transform.position, transform.forward, 1f, assets != null ? CombatAssets.Pick(assets.explosionMid) : null, true);
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            All.Remove(this);
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            if (fxRoot != null) Destroy(fxRoot.gameObject);
        }

        /// <summary>A new level: nothing deployed (no domain reload between Play sessions).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCount() { ActiveCount = 0; All.Clear(); }
    }
}
