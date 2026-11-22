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

        /// <summary>
        /// Free gathering: until the city has a Lumberjack (or Quarry), a few townspeople fetch wood
        /// (or stone) by hand, slowly but for free, so the economy can never get stuck at zero.
        /// </summary>
        [System.NonSerialized] public ResourceType? freeGather;
        Vector3 gatherSpot;
        float gatherUntil = -1f;
        int trips;
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
                HideBesidePaintedArt();
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
            HideBesidePaintedArt();
        }

        /// <summary>
        /// The 3D townsfolk look like toys next to Peter's painted buildings: until worker pictures exist
        /// they keep working (gathering, walking) but are not drawn.
        /// </summary>
        void HideBesidePaintedArt()
        {
            if (!DinoSprite.Available) return;
            foreach (var r in body.GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        /// <summary>Walks to the scrap at the mountain foot, works there, and brings in a little steel.</summary>
        bool GatherStep()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Castle == null) return false;
            if (gatherUntil > 0f)
            {
                if (Time.time < gatherUntil) return true;
                // done: the scrap goes straight to the store
                gatherUntil = -1f;
                trips++;
                gm.State.Add(ResourceType.Steel, 2);
                gatherSpot = Vector3.zero;
            }
            if (gatherSpot == Vector3.zero)
                gatherSpot = new Vector3(Random.Range(-10f, 10f), 0f, GridMap.BuildBackZ + 0.5f);
            destination = gatherSpot;
            var d = gatherSpot - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.05f)
            {
                gatherUntil = Time.time + 3f;
                AudioManager.PlayAt("hammer", transform.position, 0.35f);
                var look = Vector3.forward;
                transform.rotation = Quaternion.LookRotation(look);
            }
            return true;
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
                    case "well": c = new Color(0.2f, 0.45f, 0.7f); break;        // pump tech: blue overalls
                    case "mine": c = new Color(0.95f, 0.6f, 0.1f); break;        // driller: hi-vis orange
                    case "ballista": case "cannon": c = new Color(0.55f, 0.15f, 0.12f); break;
                    case "watchtower": c = new Color(0.55f, 0.15f, 0.12f); break; // guard: red
                }
            }
            if (work == null && freeGather == ResourceType.Steel) c = new Color(0.95f, 0.6f, 0.1f);
            if (clothes != null) Materials.SetColor(clothes.material, c);
        }

        void Update()
        {
            var cycle = DayNightCycle.Instance;
            bool night = cycle != null && cycle.IsNight;
            bool guard = work != null && work.Def.attackRange > 0f;

            Building goal = night && !guard ? home : (work != null ? work : home);
            if (!night && freeGather.HasValue && GatherStep()) { /* walking or working at a tree / rock */ }
            else if (Time.time > idleUntil && goal != null)
            {
                // pick a spot just outside the goal building, wander a little around it
                var s = GridMap.Size(goal.Def, goal.Data.rotation);
                var offset = new Vector3(Random.Range(-0.5f, 0.5f) * s.x, 0, -0.5f * s.y - 0.3f);
                destination = goal.Center + offset;
                idleUntil = Time.time + Random.Range(4f, 9f);
            }

            if (gatherUntil > 0f && Time.time < gatherUntil)
            {
                // chopping / picking: a little bob, facing the work
                body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.06f, 0);
                if (body.gameObject.activeSelf == false) body.gameObject.SetActive(true);
                return;
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
