using UnityEngine;

namespace Emberhold
{
    /// <summary>Arrow (0), ballista bolt (1) or cannon ball (2) flying in an arc to its target.</summary>
    public class Projectile : MonoBehaviour
    {
        Enemy target;
        int damage, kind;
        float splash;
        Vector3 start;
        Vector3 lastTargetPos;
        float t;
        float duration;
        float arc;

        public static void Fire(Vector3 from, Enemy target, int damage, int kind = 0, float splash = 0f)
        {
            var go = new GameObject(kind == 2 ? "CannonBall" : kind == 1 ? "Bolt" : "Arrow");
            go.transform.position = from;
            if (kind == 2)
                Util.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.22f, new Color(0.12f, 0.12f, 0.13f));
            else
            {
                float len = kind == 1 ? 0.9f : 0.5f, w = kind == 1 ? 0.07f : 0.035f;
                Util.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(w, w, len), new Color(0.55f, 0.4f, 0.25f));
                Util.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, len * 0.5f), new Vector3(w * 1.6f, w * 1.6f, w * 2f), new Color(0.3f, 0.3f, 0.32f)); // head
                Util.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, -len * 0.45f), new Vector3(w * 3f, 0.01f, w * 2.5f), new Color(0.9f, 0.88f, 0.82f)); // fletching
            }
            var p = go.AddComponent<Projectile>();
            p.target = target;
            p.damage = damage;
            p.kind = kind;
            p.splash = splash;
            p.start = from;
            p.lastTargetPos = target.transform.position + Vector3.up * 0.8f;
            float speed = kind == 1 ? 22f : kind == 2 ? 11f : 15f;
            p.arc = kind == 1 ? 0.6f : kind == 2 ? 2.8f : 1.5f;
            p.duration = Mathf.Max(0.25f, Vector3.Distance(from, p.lastTargetPos) / speed);
        }

        /// <summary>Instant laser-bolt streak for soldiers, drones and the Commander.</summary>
        public static void Tracer(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var mid = (from + to) * 0.5f;
            go.transform.position = mid;
            var d = to - from;
            if (d.sqrMagnitude < 0.0001f) { Object.Destroy(go); return; }
            go.transform.rotation = Quaternion.LookRotation(d);
            var bolt = Util.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.05f, 0.05f, d.magnitude), color);
            bolt.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(color, 2.5f);
            Object.Destroy(go, 0.07f);
        }

        void Update()
        {
            if (target != null) lastTargetPos = target.transform.position + Vector3.up * (kind == 2 ? 0.2f : 0.8f);
            t += Time.deltaTime / duration;
            var prev = transform.position;
            var pos = Vector3.Lerp(start, lastTargetPos, t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * arc;
            transform.position = pos;
            if ((pos - prev).sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(pos - prev);
            if (t < 1f) return;

            if (kind == 2)
            {
                // cannon ball: hurts everyone close to where it lands
                AudioManager.PlayAt("blast", pos, 0.8f);
                DamageEffects.Puff(pos, 1.2f);
                foreach (var e in Enemy.All.ToArray())
                {
                    if (e.Flying && e != target) continue;   // the blast hits ground units; a missile aimed at a flyer hits it
                    var d = e.transform.position - pos; d.y = 0;
                    if (d.magnitude <= Mathf.Max(splash, 0.6f)) e.Hit(damage);
                }
            }
            else
            {
                if (target != null) target.Hit(damage);
                AudioManager.PlayAt(Random.value < 0.5f ? "arrow_hit" : "arrow_hit2", pos, 0.6f);
            }
            Destroy(gameObject);
        }
    }
}
