using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    public enum EnemyKind { Raider, Brute, Boss }

    /// <summary>
    /// Walks toward the castle and attacks whatever building blocks its way.
    /// Raids the nearest buildings on its way (homes, farms, towers) and smashes walls that block it.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public EnemyKind kind;
        public int hp, maxHp, damage, reward;
        public float speed;
        public bool Visible { get; private set; }

        Renderer[] renderers;
        Transform body;
        float attackTimer;
        float visibility;      // 0..1, for fading in/out at the light's edge
        Transform hpBar;

        public static Enemy Spawn(EnemyKind kind, Vector3 position, int night, Transform parent)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var e = go.AddComponent<Enemy>();
            e.kind = kind;
            float scale = 1f + night * 0.04f;
            switch (kind)
            {
                case EnemyKind.Raider: e.maxHp = 30; e.damage = 10; e.speed = 1.3f; e.reward = 3; break;
                case EnemyKind.Brute: e.maxHp = 90; e.damage = 28; e.speed = 0.9f; e.reward = 8; break;
                default: e.maxHp = 600; e.damage = 70; e.speed = 0.7f; e.reward = 60; break;
            }
            e.maxHp = Mathf.RoundToInt(e.maxHp * scale);
            e.damage = Mathf.RoundToInt(e.damage * scale);
            e.hp = e.maxHp;
            e.BuildBody();
            All.Add(e);
            return e;
        }

        /// <summary>Wild dinosaurs: a fast Velociraptor, a heavy Triceratops that rams buildings, and the T-Rex boss.</summary>
        static string ModelPath(EnemyKind k) { return k == EnemyKind.Boss ? "Models/Dinos/Trex" : k == EnemyKind.Brute ? "Models/Dinos/Triceratops" : "Models/Dinos/Velociraptor"; }
        static float ModelLength(EnemyKind k) { return k == EnemyKind.Boss ? 4.8f : k == EnemyKind.Brute ? 2.6f : 1.6f; }

        Animation anim;
        bool dead;
        float height = 1.6f;

        void BuildBody()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            var model = ModelLibrary.SpawnByLength(ModelPath(kind), body, ModelLength(kind));
            if (model != null)
            {
                height = ModelLibrary.LocalBounds(model, transform).size.y;
                anim = model.GetComponentInChildren<Animation>();
                if (anim != null) { anim.cullingType = AnimationCullingType.BasedOnRenderers; ModelLibrary.Play(anim, kind == EnemyKind.Raider ? "Run" : "Walk", 0f); }
            }
            else
            {
                Util.Prim(PrimitiveType.Capsule, body, new Vector3(0, 0.55f, 0), new Vector3(0.45f, 0.5f, 0.35f), new Color(0.35f, 0.45f, 0.3f));
            }
            hpBar = Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, height + 0.3f, 0), new Vector3(0.6f, 0.06f, 0.06f), new Color(0.8f, 0.15f, 0.1f)).transform;
            renderers = GetComponentsInChildren<Renderer>();
            SetRenderersVisible(false);
        }

        void OnDestroy() { All.Remove(this); }

        void SetRenderersVisible(bool v)
        {
            foreach (var r in renderers) r.enabled = v;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Castle == null || dead) return;
            if (fleeing) { FleeStep(); return; }

            // Always visible; just rise in smoothly when spawned.
            visibility = Mathf.MoveTowards(visibility, 1f, Time.deltaTime * 1.5f);
            Visible = visibility > 0.5f;
            SetRenderersVisible(visibility > 0.05f);
            body.localPosition = new Vector3(0, (visibility - 1f) * 0.6f, 0); // rise in when spawned
            hpBar.localScale = new Vector3(0.6f * hp / (float)maxHp, 0.06f, 0.06f);
            hpBar.gameObject.SetActive(hp < maxHp && Visible);

            // Pick a target: the nearest standing building close by (they raid homes, farms and
            // towers on their way), otherwise march on the castle.
            retarget -= Time.deltaTime;
            if (target == null || target.Ruined || retarget <= 0f)
            {
                retarget = 1.5f;
                target = PickTarget(gm);
            }
            if (target == null) return;
            var goal = ClosestPointOn(target, transform.position);
            var dir = goal - transform.position;
            dir.y = 0;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);

            // Reached the target, or something solid is in the way: smash it.
            Building hit = null;
            if (dist < 0.55f) hit = target;
            else
            {
                var blocker = gm.Grid.At(GridMap.CellOf(transform.position + dir * 0.6f)) as Building;
                if (blocker != null && !blocker.IsGhost && !blocker.Def.passable && !blocker.Ruined) hit = blocker;
            }
            if (hit != null)
            {
                attackTimer -= Time.deltaTime;
                if (anim != null) ModelLibrary.Play(anim, "Attack", 0.1f);
                else body.localRotation = Quaternion.Euler(-20f * Mathf.Clamp01(1f - attackTimer / AttackInterval), 0, 0);
                if (attackTimer <= 0f)
                {
                    attackTimer = AttackInterval;
                    hit.TakeDamage(damage);
                    if (anim != null) { var a = anim; foreach (AnimationState st in a) if (st.name.EndsWith("_Attack")) { a.Stop(st.name); a.Play(st.name); } }
                    AudioManager.PlayAt(hit.Def.id == "wall" || hit == gm.Castle ? "sword" : "chop", transform.position, 0.45f);
                }
                return;
            }
            // wading through the river or a canal is slow
            var here = gm.Grid.At(GridMap.CellOf(transform.position));
            var trap = here as Building;
            if (trap != null && trap.Def.trap && kind != EnemyKind.Boss && gm.TryCapture(this, trap)) return;
            float wade = (here is RiverCell || (here is Building && ((Building)here).Def.passable)) ? 0.4f : 1f;
            var np = transform.position + dir * speed * wade * Time.deltaTime;
            np.y = WorldBuilder.HeightAt(np);
            transform.position = np;
            if (anim != null) ModelLibrary.Play(anim, kind == EnemyKind.Raider ? "Run" : "Walk");
            else body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 8f * speed) * 4f); // walk sway
        }

        Building target;
        float retarget;
        float AttackInterval { get { return kind == EnemyKind.Boss ? 1.6f : kind == EnemyKind.Brute ? 1.3f : 1.0f; } }
        const float AggroRadius = 9f;

        Building PickTarget(GameManager gm)
        {
            Building best = null;
            float bestD = AggroRadius;
            foreach (var b in Building.All)
            {
                if (b.IsGhost || b.Ruined || b.Def.passable || b == gm.Castle) continue;
                float d = Vector3.Distance(ClosestPointOn(b, transform.position), transform.position);
                // towers that shoot at them are hated a bit more
                if (b.Def.attackRange > 0f) d -= 2f;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best != null ? best : gm.Castle;
        }

        static Vector3 ClosestPointOn(Building b, Vector3 p)
        {
            var c = b.Center;
            var s = GridMap.Size(b.Def, b.Data.rotation);
            float hx = s.x * 0.5f + 0.25f, hz = s.y * 0.5f + 0.25f;
            return new Vector3(Mathf.Clamp(p.x, c.x - hx, c.x + hx), p.y, Mathf.Clamp(p.z, c.z - hz, c.z + hz));
        }

        public void Hit(int amount)
        {
            if (dead) return;
            hp -= amount;
            if (hp <= 0)
            {
                dead = true;
                All.Remove(this);   // towers stop aiming at it right away
                GameManager.Instance.OnEnemyKilled(this);
                AudioManager.PlayAt("hurt", transform.position, 0.6f);
                hpBar.gameObject.SetActive(false);
                if (anim != null && ModelLibrary.Play(anim, "Death", 0.05f)) Destroy(gameObject, 1.8f);
                else Destroy(gameObject);
            }
        }

        bool fleeing;
        float fleeEnd;

        /// <summary>Caught in a trap: it leaves the fight and is moved to a pen.</summary>
        public void Captured()
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            DamageEffects.Puff(transform.position + Vector3.up * 0.5f, 1.2f);
            Destroy(gameObject);
        }

        /// <summary>The attack is over: run back into the wild and disappear (attacks stay short).</summary>
        public void Flee()
        {
            if (dead || fleeing) return;
            fleeing = true;
            fleeEnd = Time.time + 5f;
            All.Remove(this);           // towers stop shooting
            hpBar.gameObject.SetActive(false);
        }

        /// <summary>Sends every attacker home. Returns how many ran.</summary>
        public static int FleeAll()
        {
            var list = new List<Enemy>(All);
            foreach (var e in list) e.Flee();
            return list.Count;
        }

        void FleeStep()
        {
            var away = transform.position - GameManager.Instance.Castle.Center; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            away.Normalize();
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), Time.deltaTime * 6f);
            var np = transform.position + away * speed * 1.8f * Time.deltaTime;
            np.y = WorldBuilder.HeightAt(np);
            transform.position = np;
            if (anim != null) ModelLibrary.Play(anim, "Run");
            float left = fleeEnd - Time.time;
            if (left < 1f) body.localPosition = new Vector3(0, (left - 1f) * 1.2f, 0);   // sink away at the edge
            if (left <= 0f) Destroy(gameObject);
        }

        public static Enemy NearestVisible(Vector3 from, float range)
        {
            Enemy best = null;
            float bestD = range * range;
            foreach (var e in All)
            {
                if (!e.Visible) continue;
                var d = e.transform.position - from; d.y = 0;
                float sq = d.sqrMagnitude;
                if (sq < bestD) { bestD = sq; best = e; }
            }
            return best;
        }
    }
}
