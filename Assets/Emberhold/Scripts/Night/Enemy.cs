using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    public enum EnemyKind { Raider, Brute, Boss }

    /// <summary>
    /// Walks toward the castle and attacks whatever building blocks its way.
    /// It is only visible inside light (torches, towers, castle); in the dark it is hidden in fog.
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
                case EnemyKind.Raider: e.maxHp = 30; e.damage = 6; e.speed = 1.3f; e.reward = 3; break;
                case EnemyKind.Brute: e.maxHp = 90; e.damage = 15; e.speed = 0.9f; e.reward = 8; break;
                default: e.maxHp = 600; e.damage = 40; e.speed = 0.7f; e.reward = 60; break;
            }
            e.maxHp = Mathf.RoundToInt(e.maxHp * scale);
            e.damage = Mathf.RoundToInt(e.damage * scale);
            e.hp = e.maxHp;
            e.BuildBody();
            All.Add(e);
            return e;
        }

        void BuildBody()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            float s = kind == EnemyKind.Boss ? 2.2f : kind == EnemyKind.Brute ? 1.35f : 1f;
            body.localScale = Vector3.one * s;
            var cloak = kind == EnemyKind.Boss ? new Color(0.25f, 0.05f, 0.05f) : new Color(0.18f, 0.16f, 0.15f);
            var skin = new Color(0.55f, 0.45f, 0.38f);
            Util.Prim(PrimitiveType.Capsule, body, new Vector3(0, 0.55f, 0), new Vector3(0.45f, 0.5f, 0.35f), cloak);
            Util.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.15f, 0), new Vector3(0.28f, 0.3f, 0.28f), skin);
            Util.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.22f, -0.02f), new Vector3(0.32f, 0.22f, 0.32f), cloak * 0.8f); // hood
            var weapon = Util.Prim(PrimitiveType.Cube, body, new Vector3(0.3f, 0.7f, 0.15f), new Vector3(0.06f, 0.7f, 0.06f), new Color(0.3f, 0.3f, 0.3f));
            weapon.transform.localRotation = Quaternion.Euler(30, 0, 0);
            if (kind == EnemyKind.Boss)
                Util.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.42f, 0), new Vector3(0.36f, 0.1f, 0.36f), new Color(0.6f, 0.5f, 0.1f)); // crown

            hpBar = Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, 1.6f * s, 0), new Vector3(0.6f, 0.06f, 0.06f), new Color(0.8f, 0.15f, 0.1f)).transform;
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
            if (gm == null || gm.Castle == null) return;

            // Always visible; just rise in smoothly when spawned.
            visibility = Mathf.MoveTowards(visibility, 1f, Time.deltaTime * 1.5f);
            Visible = visibility > 0.5f;
            SetRenderersVisible(visibility > 0.05f);
            body.localPosition = new Vector3(0, (visibility - 1f) * 0.6f, 0); // rise out of the dark
            hpBar.localScale = new Vector3(0.6f * hp / (float)maxHp, 0.06f, 0.06f);
            hpBar.gameObject.SetActive(hp < maxHp && Visible);

            var target = gm.Castle.Center;
            var dir = target - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);

            // Is something in the way?
            var ahead = transform.position + dir * 0.7f;
            var blocker = gm.Grid.At(GridMap.CellOf(ahead)) as Building;
            if (blocker != null && !blocker.IsGhost && !blocker.Def.passable)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = 1.1f;
                    blocker.TakeDamage(damage);
                    body.localPosition += dir * 0.15f;
                    if (Visible) AudioManager.Play("impact", 0.4f);
                }
                return;
            }
            // wading through the river or a canal is slow
            var here = gm.Grid.At(GridMap.CellOf(transform.position));
            float wade = (here is RiverCell || (here is Building && ((Building)here).Def.passable)) ? 0.4f : 1f;
            var np = transform.position + dir * speed * wade * Time.deltaTime;
            np.y = WorldBuilder.HeightAt(np);
            transform.position = np;
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 8f * speed) * 4f); // walk sway
        }

        public void Hit(int amount)
        {
            hp -= amount;
            if (hp <= 0)
            {
                GameManager.Instance.OnEnemyKilled(this);
                AudioManager.Play("downed", 0.6f);
                Destroy(gameObject);
            }
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
