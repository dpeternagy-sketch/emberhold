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
        /// <summary>Where the player sent this unit (null = stay home). It guards that spot until called back.</summary>
        public Vector3? order;
        Vector3 Post { get { return order ?? home; } }
        GameObject ring;

        /// <summary>Blue ring under the unit while the player has it selected.</summary>
        public void SetSelected(bool on)
        {
            if (on && ring == null)
            {
                ring = Util.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.01f, 0.9f), new Color(0.18f, 0.49f, 0.96f));
                var c = ring.GetComponent<Collider>(); if (c != null) Destroy(c);
                ring.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(0.18f, 0.49f, 0.96f), 1.4f);
            }
            if (ring != null) ring.SetActive(on);
        }

        /// <summary>Send to a spot (null = back home). The unit fights anything near that spot and stays there.</summary>
        public void Order(Vector3? spot) { order = spot; wanderAt = 0f; }
        float altitude = 0.25f;
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
            u.portrait = def.id == "heavy" ? "soldier_2" : def.id == "rifleman" ? "soldier_1" : null;
            u.Build(def.model, def.height, def.id == "heavy" ? 1.3f : 1f, Color.clear);
            u.Place(home);
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
            u.portrait = "commander_3";
            u.Build("Models/Space/astronautB", 0.95f, 1.1f, new Color(1f, 0.78f, 0.2f));
            u.Place(home);
            return u;
        }

        void Place(Vector3 p)
        {
            p.y = WorldBuilder.HeightAt(p) + (flies ? 1.6f : 0f);
            transform.position = p;
            wanderGoal = p;
            hp = maxHp;
        }

        string portrait;
        DinoSprite sprite;

        void Build(string model, float height, float bulk, Color trim)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            // Peter's painted white-and-gold soldiers and Commander (Resources/Sprites/Portraits)
            var pic = !flies && portrait != null ? DinoSprite.CreateUnit(transform, portrait, height * (isHero ? 1.65f : 2.1f)) : null;
            if (pic != null)
            {
                sprite = pic;
                var team = trim.a > 0f ? trim : new Color(0.18f, 0.49f, 0.96f);
                var tr = Util.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.02f, 0), new Vector3(height * 0.9f, 0.01f, height * 0.9f), team);
                tr.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(team, trim.a > 0f ? 1.6f : 0.8f);
                hpBar = Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, height * 1.7f + 0.2f, 0), new Vector3(0.6f, 0.06f, 0.06f), new Color(0.3f, 0.85f, 0.35f)).transform;
                renderers = GetComponentsInChildren<Renderer>();
                bobPhase = Random.value * 6f;
                return;
            }
            Transform m = flies ? ModelLibrary.Spawn(model, body, height, height, height * 0.5f) : ModelLibrary.Spawn(model, body, height, height, height);
            if (m == null)
                m = Util.Prim(PrimitiveType.Capsule, body, new Vector3(0, height * 0.5f, 0), new Vector3(height * 0.4f, height * 0.5f, height * 0.4f), new Color(0.9f, 0.92f, 0.95f)).transform;
            m.localScale = Vector3.Scale(m.localScale, new Vector3(bulk, 1f, bulk));
            if (!flies)
            {
                // style guide: white suit, team-blue chest, dark visor (no faces or beards)
                var visor = new Color(0.106f, 0.137f, 0.2f);   // #1B2333
                ModelLibrary.Recolor(m,
                    ModelLibrary.Pair("skin", visor), ModelLibrary.Pair("rock", visor), ModelLibrary.Pair("rockDark", visor),
                    ModelLibrary.Pair("metal", new Color(0.957f, 0.965f, 0.973f)),
                    ModelLibrary.Pair("metalRed", trim.a > 0f ? trim : new Color(0.18f, 0.49f, 0.96f)),
                    ModelLibrary.Pair("metalDark", new Color(0.35f, 0.4f, 0.48f)));
            }
            if (trim.a > 0f)
            {
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    foreach (var mat in r.materials)
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.Lerp(mat.GetColor("_BaseColor"), trim, 0.45f));
                var ring = Util.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.015f, 0.9f), trim);
                ring.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(trim, 1.6f);
            }
            else if (!flies)
            {
                // small team-blue ring at the feet: ours, never mixed up with AI cities (orange)
                var ring = Util.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.02f, 0), new Vector3(height * 0.55f, 0.01f, height * 0.55f), new Color(0.18f, 0.49f, 0.96f));
                ring.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(0.18f, 0.49f, 0.96f), 0.8f);
            }
            anim = GetComponentInChildren<Animation>();
            hpBar = Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, (flies ? 0.5f : height) + 0.25f, 0), new Vector3(0.6f, 0.06f, 0.06f), new Color(0.3f, 0.85f, 0.35f)).transform;
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
            if (sprite != null) sprite.Flash();
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
            transform.position = new Vector3(home.x, WorldBuilder.HeightAt(home) + (flies ? 1.6f : 0f), home.z);
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
                // drones don't roam: they fly straight back to their pad and land (Peter)
                if (flies) wanderGoal = Post;
                else if (Time.time > wanderAt) { wanderAt = Time.time + Random.Range(3f, 7f); wanderGoal = Post + Random.insideUnitSphere.Flat() * (isHero ? 0.8f : 0.25f); }
                var to = wanderGoal - transform.position; to.y = 0f;
                if (to.magnitude > 0.3f) { Face(to); Move(to.normalized, to.magnitude > 2f ? 1.25f : 0.5f); }
                else Play("Idle");
            }

            if (flies)
            {
                // take off only to fight; parked on the pad otherwise
                var flat = transform.position - Post; flat.y = 0f;
                bool parked = target == null && flat.magnitude < 0.35f;
                altitude = Mathf.MoveTowards(altitude, parked ? 0.25f : 1.6f, Time.deltaTime * (parked ? 0.8f : 1.6f));
                var p = transform.position;
                float bob = parked ? 0f : Mathf.Sin(Time.time * 2f + bobPhase) * 0.12f * Mathf.InverseLerp(0.25f, 1.6f, altitude);
                p.y = WorldBuilder.HeightAt(p) + altitude + bob;
                transform.position = p;
                if (parked && anim == null) body.localPosition = Vector3.zero;
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
                float fromHome = Vector3.Distance(e.transform.position, Post);
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
            if (sprite != null) sprite.Attack();
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
