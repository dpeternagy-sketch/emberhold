using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// A dinosaur caught in a trap and kept in a Dino Pen. It wanders in its pen and runs out to
    /// bite any wild dinosaur that comes close, then walks back home. Marked by a glowing blue ring.
    /// </summary>
    public class TamedDino : MonoBehaviour
    {
        public static readonly List<TamedDino> All = new List<TamedDino>();

        public EnemyKind kind;
        public Building pen;

        Animation anim;
        Transform body;
        Vector3 wander;
        float nextWander;
        float attackTimer;
        Enemy target;
        float retarget;

        const float GuardRadius = 12f;
        float Speed { get { return kind == EnemyKind.Raider ? 2.4f : 1.5f; } }
        int Damage { get { return kind == EnemyKind.Raider ? 9 : 22; } }
        float AttackInterval { get { return kind == EnemyKind.Raider ? 0.9f : 1.4f; } }

        public static TamedDino Spawn(EnemyKind kind, Building pen, Transform parent)
        {
            var go = new GameObject("Tamed " + kind);
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<TamedDino>();
            d.kind = kind;
            d.pen = pen;
            d.transform.position = d.RandomSpotInPen();
            d.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            d.body = new GameObject("Body").transform;
            d.body.SetParent(go.transform, false);
            var path = kind == EnemyKind.Brute ? "Models/Dinos/Triceratops" : "Models/Dinos/Velociraptor";
            var model = ModelLibrary.SpawnByLength(path, d.body, kind == EnemyKind.Brute ? 2.2f : 1.4f);
            if (model != null)
            {
                d.anim = model.GetComponentInChildren<Animation>();
                if (d.anim != null) ModelLibrary.Play(d.anim, "Idle", 0f);
            }
            else Util.Prim(PrimitiveType.Capsule, d.body, new Vector3(0, 0.5f, 0), new Vector3(0.4f, 0.45f, 0.8f), new Color(0.3f, 0.5f, 0.6f));
            // friendly marker: glowing ring on the ground
            var ring = Util.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.03f, 0), new Vector3(1.1f, 0.01f, 1.1f), new Color(0.3f, 0.85f, 1f));
            ring.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(0.3f, 0.85f, 1f), 1.5f);
            d.wander = d.transform.position;
            All.Add(d);
            return d;
        }

        void OnDestroy() { All.Remove(this); }

        Vector3 RandomSpotInPen()
        {
            var c = pen != null ? pen.Center : Vector3.zero;
            var r = Random.insideUnitCircle * 0.9f;
            var p = c + new Vector3(r.x, 0f, r.y);
            p.y = WorldBuilder.HeightAt(p);
            return p;
        }

        void Update()
        {
            if (pen == null) { Destroy(gameObject); return; }

            retarget -= Time.deltaTime;
            if (retarget <= 0f || (target != null && !Enemy.All.Contains(target)))
            {
                retarget = 0.5f;
                target = pen.Ruined ? null : Enemy.NearestVisible(pen.Center, GuardRadius);
            }

            Vector3 goal;
            string move;
            if (target != null)
            {
                goal = target.transform.position;
                move = "Run";
                var to = goal - transform.position; to.y = 0f;
                if (to.magnitude < (target.kind == EnemyKind.Boss ? 2.2f : 1.1f))
                {
                    Face(to);
                    attackTimer -= Time.deltaTime;
                    if (anim != null) ModelLibrary.Play(anim, "Attack", 0.1f);
                    if (attackTimer <= 0f)
                    {
                        attackTimer = AttackInterval;
                        target.Hit(Damage);
                        AudioManager.PlayAt("hurt", transform.position, 0.3f);
                    }
                    return;
                }
            }
            else
            {
                if (Time.time > nextWander) { wander = RandomSpotInPen(); nextWander = Time.time + Random.Range(3f, 7f); }
                goal = wander;
                move = "Walk";
            }

            var d = goal - transform.position; d.y = 0f;
            if (d.magnitude < 0.15f)
            {
                if (anim != null) ModelLibrary.Play(anim, "Idle");
                return;
            }
            Face(d);
            float sp = target != null ? Speed : Speed * 0.35f;
            var np = transform.position + d.normalized * Mathf.Min(d.magnitude, sp * Time.deltaTime);
            np.y = WorldBuilder.HeightAt(np);
            transform.position = np;
            if (anim != null) ModelLibrary.Play(anim, move);
        }

        void Face(Vector3 d)
        {
            if (d.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 8f);
        }
    }
}
