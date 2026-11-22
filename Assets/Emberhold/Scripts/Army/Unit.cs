using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// A trained soldier, robot drone or the Commander. Lives at a home spot (army camp / command center),
    /// runs out to fight dinosaurs that come near, and goes back when the area is clear.
    /// Defenders are never lost: at 0 HP they are knocked out and come back 60 s later at full health.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        public static readonly List<Unit> All = new List<Unit>();

        public UnitDef def;
        public bool isHero;
        public int hp, maxHp, damage;
        public float range, speed, interval;
        public bool flies, hitsAir;
        public Vector3 home;
        public bool KnockedOut { get; private set; }

        const float GuardRadius = 14f, Leash = 24f, ReviveSeconds = 60f;
        Enemy target;
        float attackTimer, retarget, reviveAt, calmFor, bobPhase;
        Transform body, hpBar;
        Animation anim;
        Renderer[] renderers;
        Vector3 wanderGoal;
        float wanderAt;

        public static Unit Spawn(UnitDef def, Vector3 home, Transform parent)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetParent(parent, false);
            var u = go.AddComponent<Unit>();
            u.def = def; u.maxHp = def.hp; u.damage = def.damage; u.range = def.range; u.speed = def.speed;
            u.interval = def.interval; u.flies = def.flies; u.hitsAir = def.hitsAir;
            u.home = home;
            u.Build(def.model, def.height, def.id == "heavy" ? 1.3f : 1f, Color.clear);
            u.Place(home + Random.insideUnitSphere.Flat() * 1.2f);
            return u;
        }

        public static Unit SpawnHero(int level, Vector3 home, Transform parent)
        {
            var go = new GameObject("Commander");
            go.transform.SetParent(parent, false);
            var u = go.AddComponent<Unit>();
            u.isHero = true;
            u.maxHp = Mathf.RoundToInt(450 * Mathf.Pow(1.15f, level - 1));
            u.damage = Mathf.RoundToInt(16 * Mathf.Pow(1.12f, level - 1));
            u.range = 3f; u.speed = 1.05f; u.interval = 1f; u.hitsAir = true;
            u.home = home;
            u.Build("Models/Space/astronautB", 1.75f, 1.1f, new Color(1f, 0.78f, 0.2f));
            u.Place(home);
            return u;
        }

        void Place(Vector3 p)
        {
            p.y = WorldBuilder.HeightAt(p) + (flies ? 2.2f : 0f);
            transform.position = p;
            wanderGoal = p;
            hp = maxHp;
        }

        void Build(string model, float height, float bulk, Color trim)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            Transform m = flies ? ModelLibrary.Spawn(model, body, 1.3f, 1.3f, 0.7f) : ModelLibrary.Spawn(model, body, height, height, height);
            if (m == null)
                m = Util.Prim(PrimitiveType.Capsule, body, new Vector3(0, height * 0.5f, 0), new Vector3(height * 0.4f, height * 0.5f, height * 0.4f), new Color(0.9f, 0.92f, 0.95f)).transform;
            m.localScale = Vector3.Scale(m.localScale, new Vector3(bulk, 1f, bulk));
            if (trim.a > 0f)
            {
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    foreach (var mat in r.materials)
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.Lerp(mat.GetColor("_BaseColor"), trim, 0.45f));
                var ring = Util.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.03f, 0), new Vector3(1.3f, 0.015f, 1.3f), trim);
                ring.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(trim, 1.6f);
            }
            else
            {
                // team colour stripe so our troops read as ours (blue), never mixed up with AI cities (orange)
                var tag = Util.Prim(PrimitiveType.Cube, body, new Vector3(0, height * 0.62f, 0), new Vector3(height * 0.42f, 0.06f, height * 0.42f), new Color(0.18f, 0.49f, 0.96f));
                if (flies) tag.SetActive(false);
            }
            anim = GetComponentInChildren<Animation>();
            hpBar = Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, (flies ? 0.9f : height) + 0.35f, 0), new Vector3(0.6f, 0.06f, 0.06f), new Color(0.3f, 0.85f, 0.35f)).transform;
            renderers = GetComponentsInChildren<Renderer>();
            bobPhase = Random.value * 6f;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public Vector3 Center { get { return transform.position + Vector3.up * 0.6f; } }

        public void TakeDamage(int amount)
        {
            if (KnockedOut) return;
            hp -= amount;
            calmFor = 0f;
            if (hp > 0) return;
            KnockedOut = true;
            reviveAt = Time.time + ReviveSeconds;
            DamageEffects.Puff(transform.position + Vector3.up * 0.5f, 0.8f);
            foreach (var r in renderers) r.enabled = false;
            if (isHero && GameManager.Instance != null) GameManager.Instance.Toast("Your Commander is down. Back in 60 s");
        }

        void Revive()
        {
            KnockedOut = false;
            hp = maxHp;
            transform.position = new Vector3(home.x, WorldBuilder.HeightAt(home) + (flies ? 2.2f : 0f), home.z);
            foreach (var r in renderers) r.enabled = true;
            DamageEffects.Puff(transform.position + Vector3.up * 0.5f, 0.6f);
        }

        void Update()
        {
            if (KnockedOut) { if (Time.time >= reviveAt) Revive(); return; }
            hpBar.localScale = new Vector3(0.6f * hp / (float)maxHp, 0.06f, 0.06f);
            hpBar.gameObject.SetActive(hp < maxHp);

            retarget -= Time.deltaTime;
            if (retarget <= 0f || target == null || !Enemy.All.Contains(target))
            {
                retarget = 0.6f;
                target = PickTarget();
            }

            if (target != null)
            {
                calmFor = 0f;
                var to = target.transform.position - transform.position; to.y = 0f;
                float d = to.magnitude;
                Face(to);
                if (d > range) { Move(to / Mathf.Max(0.01f, d), 1.25f); }
                else
                {
                    Play("Idle");
                    attackTimer -= Time.deltaTime;
                    if (attackTimer <= 0f)
                    {
                        attackTimer = interval;
                        Shoot(target);
                    }
                }
            }
            else
            {
                // calm: heal up and stroll around home
                calmFor += Time.deltaTime;
                if (calmFor > 4f && hp < maxHp) hp = Mathf.Min(maxHp, hp + Mathf.CeilToInt(maxHp * 0.08f * Time.deltaTime * 4f));
                if (Time.time > wanderAt) { wanderAt = Time.time + Random.Range(3f, 7f); wanderGoal = home + Random.insideUnitSphere.Flat() * (isHero ? 1f : 2f); }
                var to = wanderGoal - transform.position; to.y = 0f;
                if (to.magnitude > 0.3f) { Face(to); Move(to.normalized, 0.5f); }
                else Play("Idle");
            }

            if (flies)
            {
                var p = transform.position;
                p.y = WorldBuilder.HeightAt(p) + 2.2f + Mathf.Sin(Time.time * 2f + bobPhase) * 0.15f;
                transform.position = p;
            }
        }

        Enemy PickTarget()
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemy.All)
            {
                if (e == null || !e.Visible) continue;
                if (e.Flying && !hitsAir) continue;
                float fromHome = Vector3.Distance(e.transform.position, home);
                float fromMe = Vector3.Distance(e.transform.position, transform.position);
                if (fromHome > GuardRadius && fromMe > 7f) continue;
                if (fromHome > Leash) continue;
                if (fromMe < bestD) { bestD = fromMe; best = e; }
            }
            return best;
        }

        void Shoot(Enemy e)
        {
            int dmg = damage;
            e.Hit(dmg);
            if (range > 1.6f) Projectile.Tracer(Center, e.transform.position + Vector3.up * 0.5f, isHero ? new Color(1f, 0.8f, 0.3f) : new Color(0.4f, 0.8f, 1f));
            AudioManager.PlayAt(range > 1.6f ? "arrow_hit2" : "sword", transform.position, 0.2f);
        }

        void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 8f);
        }

        void Move(Vector3 dir, float pace)
        {
            var np = transform.position + dir * speed * pace * Time.deltaTime;
            if (!flies) np.y = WorldBuilder.HeightAt(np);
            transform.position = np;
            if (!Play(pace > 1f ? "Run" : "Walk") && !flies)
                body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 10f * speed)) * 0.08f, 0);   // hop as they walk
        }

        bool Play(string a)
        {
            if (anim == null) { if (a == "Idle") body.localPosition = Vector3.zero; return false; }
            return ModelLibrary.Play(anim, a) || (a == "Walk" && ModelLibrary.Play(anim, "Walking")) || (a == "Run" && ModelLibrary.Play(anim, "Running"));
        }

        /// <summary>The nearest standing defender close to a point (dinosaurs fight back).</summary>
        public static Unit NearestStanding(Vector3 from, float radius, bool includeFlyers)
        {
            Unit best = null;
            float bestD = radius;
            foreach (var u in All)
            {
                if (u.KnockedOut || (u.flies && !includeFlyers)) continue;
                var d = u.transform.position - from; d.y = 0f;
                float m = d.magnitude;
                if (m < bestD) { bestD = m; best = u; }
            }
            return best;
        }
    }

    static class VecExt
    {
        public static Vector3 Flat(this Vector3 v) { return new Vector3(v.x, 0f, v.z); }
    }
}
