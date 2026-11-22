using UnityEngine;

namespace Emberhold
{
    /// <summary>An arrow that flies in an arc to its target.</summary>
    public class Projectile : MonoBehaviour
    {
        Enemy target;
        int damage;
        Vector3 start;
        Vector3 lastTargetPos;
        float t;
        float duration;

        public static void Fire(Vector3 from, Enemy target, int damage)
        {
            var go = new GameObject("Arrow");
            go.transform.position = from;
            Util.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.04f, 0.04f, 0.5f), new Color(0.85f, 0.75f, 0.5f));
            var p = go.AddComponent<Projectile>();
            p.target = target;
            p.damage = damage;
            p.start = from;
            p.lastTargetPos = target.transform.position + Vector3.up * 0.8f;
            p.duration = Mathf.Max(0.25f, Vector3.Distance(from, p.lastTargetPos) / 14f);
        }

        void Update()
        {
            if (target != null) lastTargetPos = target.transform.position + Vector3.up * 0.8f;
            t += Time.deltaTime / duration;
            var prev = transform.position;
            var pos = Vector3.Lerp(start, lastTargetPos, t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.5f;
            transform.position = pos;
            if ((pos - prev).sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(pos - prev);
            if (t >= 1f)
            {
                if (target != null) target.Hit(damage);
                Destroy(gameObject);
            }
        }
    }
}
