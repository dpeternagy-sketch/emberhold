using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// A townsperson. Lives in a house, walks to its workplace by day, goes home at night.
    /// Clothing color depends on the job. Placeholder body; replace with a rigged
    /// Quaternion character (Resources/Models/villager) for real walk animations.
    /// </summary>
    public class Villager : MonoBehaviour
    {
        public static readonly List<Villager> All = new List<Villager>();

        public Building home;
        public Building work;
        Transform body;
        Renderer clothes;
        Vector3 destination;
        float idleUntil;
        float speed;

        public static Villager Spawn(Transform parent, Vector3 at)
        {
            var go = new GameObject("Villager");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var v = go.AddComponent<Villager>();
            v.speed = Random.Range(1.0f, 1.4f);
            v.Build();
            v.destination = at;
            All.Add(v);
            return v;
        }

        void OnDestroy() { All.Remove(this); }

        void Build()
        {
            var prefab = Resources.Load<GameObject>("Models/villager");
            if (prefab != null)
            {
                body = Instantiate(prefab, transform, false).transform;
                clothes = body.GetComponentInChildren<Renderer>();
                return;
            }
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * 0.55f;
            var skinTones = new[] { new Color(0.93f, 0.78f, 0.65f), new Color(0.78f, 0.6f, 0.45f), new Color(0.55f, 0.4f, 0.3f) };
            clothes = Util.Prim(PrimitiveType.Capsule, body, new Vector3(0, 0.6f, 0), new Vector3(0.45f, 0.55f, 0.35f), Color.gray).GetComponent<Renderer>();
            clothes.sharedMaterial = Materials.Unique(Color.gray);
            Util.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.3f, 0), new Vector3(0.3f, 0.32f, 0.3f), skinTones[Random.Range(0, skinTones.Length)]);
            Util.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.4f, 0), new Vector3(0.31f, 0.18f, 0.31f), new Color(0.25f, 0.17f, 0.1f)); // hair/hat
        }

        public void SetJob(Building workplace)
        {
            work = workplace;
            Color c = new Color(0.55f, 0.5f, 0.45f); // townsperson
            if (work != null)
            {
                switch (work.Def.id)
                {
                    case "farm": c = new Color(0.6f, 0.5f, 0.25f); break;        // farmer: straw/tan
                    case "lumberjack": c = new Color(0.25f, 0.4f, 0.2f); break;  // woodcutter: green
                    case "quarry": c = new Color(0.45f, 0.45f, 0.5f); break;     // mason: grey
                    case "workshop": c = new Color(0.35f, 0.2f, 0.15f); break;   // smith: leather apron
                    case "watchtower": c = new Color(0.55f, 0.15f, 0.12f); break; // guard: red
                }
            }
            if (clothes != null) Materials.SetColor(clothes.material, c);
        }

        void Update()
        {
            var cycle = DayNightCycle.Instance;
            bool night = cycle != null && cycle.IsNight;
            bool guard = work != null && work.Def.attackRange > 0f;

            Building goal = night && !guard ? home : (work != null ? work : home);
            if (Time.time > idleUntil && goal != null)
            {
                // pick a spot just outside the goal building, wander a little around it
                var s = GridMap.Size(goal.Def, goal.Data.rotation);
                var offset = new Vector3(Random.Range(-0.5f, 0.5f) * s.x, 0, -0.5f * s.y - 0.3f);
                destination = goal.Center + offset;
                idleUntil = Time.time + Random.Range(4f, 9f);
            }

            var delta = destination - transform.position;
            delta.y = 0;
            bool walking = delta.sqrMagnitude > 0.02f;
            if (walking)
            {
                var step = delta.normalized * speed * Time.deltaTime;
                if (step.sqrMagnitude > delta.sqrMagnitude) step = delta;
                transform.position += step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 8f);
                body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 10f * speed)) * 0.05f, 0);
            }
            else body.localPosition = Vector3.zero;

            // Indoors at night (except guards).
            bool hidden = night && !guard && !walking;
            if (body.gameObject.activeSelf == hidden) body.gameObject.SetActive(!hidden);
        }
    }
}
