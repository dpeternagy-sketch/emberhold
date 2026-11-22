using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// The player's army: training queues in the Barracks / Robot Factory (real-time, keeps going while
    /// the game is closed), troops living in Army Camps who defend the city, and the Commander hero
    /// (lives at the Command Center, levels up with XP, calls Orbital Strikes).
    /// </summary>
    public class Army : MonoBehaviour
    {
        public static Army Instance { get; private set; }
        GameManager gm;
        Transform root;
        readonly Dictionary<string, List<Unit>> units = new Dictionary<string, List<Unit>>();
        public Unit Hero { get; private set; }
        float tick;

        // ---- Commander ----
        public static readonly int[] XpToReach = { 0, 0, 100, 300, 700, 1500 };   // index = level
        public const int MaxHeroLevel = 5;
        public const float StrikeCooldown = 30f, StrikeDelay = 1.5f, StrikeRadius = 2.5f;
        public float StrikeReadyAt { get; private set; }
        public int StrikeDamage { get { return 120 + 25 * (gm.State.heroLevel - 1); } }
        public bool HasCommandCenter { get { return CommandCenter() != null; } }

        public void Init(GameManager game)
        {
            Instance = this;
            gm = game;
            root = new GameObject("Army").transform;
            if (gm.State.firstPlayUtcTicks == 0) gm.State.firstPlayUtcTicks = DateTime.UtcNow.Ticks;
            ProcessTraining();     // offline training catches up at once
            Sync();
        }

        void Update()
        {
            tick -= Time.deltaTime;
            if (tick > 0f) return;
            tick = 0.5f;
            ProcessTraining();
            Sync();
            AutoStrike();
        }

        // ------------------------------------------------------------------ camp space

        public int Capacity
        {
            get
            {
                int cap = 0;
                foreach (var b in Building.All)
                    if (!b.IsGhost && !b.Ruined && !b.UnderConstruction && b.Def.campSpace > 0) cap += b.Def.campSpace + b.Def.campPerLevel * (b.Data.level - 1);
                return cap;
            }
        }

        bool CampBuilding()
        {
            foreach (var b in Building.All) if (b.Def.campSpace > 0 && !b.IsGhost && !b.Ruined && b.UnderConstruction) return true;
            return false;
        }

        bool CampDamaged()
        {
            foreach (var b in Building.All) if (b.Def.campSpace > 0 && !b.IsGhost && b.Ruined) return true;
            return false;
        }

        public int Count(string id)
        {
            var s = gm.State;
            return id == "rifleman" ? s.riflemen : id == "heavy" ? s.heavies : id == "drone" ? s.drones : 0;
        }

        void AddCount(string id, int n)
        {
            var s = gm.State;
            if (id == "rifleman") s.riflemen += n; else if (id == "heavy") s.heavies += n; else if (id == "drone") s.drones += n;
        }

        public int Used
        {
            get { int u = 0; foreach (var d in UnitDef.Ordered) u += Count(d.id) * d.space; return u; }
        }

        public int Queued
        {
            get
            {
                int q = 0;
                foreach (var b in Building.All)
                    if (b.Def.trains != null) foreach (var id in Queue(b)) { var d = UnitDef.Get(id); if (d != null) q += d.space; }
                return q;
            }
        }

        // ------------------------------------------------------------------ training

        public static List<string> Queue(Building b)
        {
            var q = new List<string>();
            if (string.IsNullOrEmpty(b.Data.trainQueue)) return q;
            foreach (var p in b.Data.trainQueue.Split(',')) if (p.Length > 0) q.Add(p);
            return q;
        }

        static void SetQueue(Building b, List<string> q) { b.Data.trainQueue = string.Join(",", q.ToArray()); }

        /// <summary>Why this unit can't be queued here right now, or null if it can.</summary>
        public string CantTrain(Building b, UnitDef d)
        {
            if (b.Ruined) return "Repair it first";
            if (b.Data.level < d.barracksLevel) return d.displayName + " needs " + b.Def.displayName + " Lv " + d.barracksLevel;
            if (Capacity == 0) return CampBuilding() ? "Army camp still being built" : CampDamaged() ? "Army camp damaged: repair it" : "Build an Army Camp first";
            if (Used + Queued + d.space > Capacity) return "Army camps are full";
            if (!gm.State.CanAfford(d.cost)) return "Need more " + gm.State.Missing(d.cost);
            return null;
        }

        public bool Train(Building b, UnitDef d)
        {
            var why = CantTrain(b, d);
            if (why != null) { gm.Toast(why); return false; }
            gm.State.Pay(d.cost);
            var q = Queue(b);
            q.Add(d.id);
            if (q.Count == 1) b.Data.trainEndUtcTicks = DateTime.UtcNow.AddSeconds(d.trainSeconds).Ticks;
            SetQueue(b, q);
            AudioManager.Play("ui", 0.6f);
            return true;
        }

        /// <summary>Removes the last queued unit and refunds it in full.</summary>
        public void CancelLast(Building b)
        {
            var q = Queue(b);
            if (q.Count == 0) return;
            var d = UnitDef.Get(q[q.Count - 1]);
            q.RemoveAt(q.Count - 1);
            if (d != null) gm.State.Refund(d.cost, 1f);
            if (q.Count == 0) b.Data.trainEndUtcTicks = 0;
            SetQueue(b, q);
        }

        public float SecondsLeft(Building b)
        {
            if (Queue(b).Count == 0 || b.Data.trainEndUtcTicks == 0) return 0f;
            return Mathf.Max(0f, (float)(new DateTime(b.Data.trainEndUtcTicks) - DateTime.UtcNow).TotalSeconds);
        }

        void ProcessTraining()
        {
            var now = DateTime.UtcNow.Ticks;
            foreach (var b in Building.All)
            {
                if (b.Def.trains == null || b.IsGhost || b.Ruined) continue;
                var q = Queue(b);
                bool changed = false;
                while (q.Count > 0)
                {
                    var d = UnitDef.Get(q[0]);
                    if (d == null) { q.RemoveAt(0); changed = true; continue; }
                    if (b.Data.trainEndUtcTicks == 0) b.Data.trainEndUtcTicks = now + TimeSpan.FromSeconds(d.trainSeconds).Ticks;
                    if (b.Data.trainEndUtcTicks > now) break;
                    if (Used + d.space > Capacity) break;        // waits until there is room
                    AddCount(d.id, 1);
                    q.RemoveAt(0);
                    changed = true;
                    if (q.Count > 0)
                    {
                        var next = UnitDef.Get(q[0]);
                        b.Data.trainEndUtcTicks = Math.Max(b.Data.trainEndUtcTicks, now - TimeSpan.FromHours(8).Ticks) + TimeSpan.FromSeconds(next != null ? next.trainSeconds : 1).Ticks;
                    }
                    else b.Data.trainEndUtcTicks = 0;
                    if (Application.isPlaying && Time.timeSinceLevelLoad > 3f) gm.Toast(d.displayName + " ready!");
                }
                if (changed) SetQueue(b, q);
            }
        }

        // ------------------------------------------------------------------ troops on the map

        Building CommandCenter()
        {
            foreach (var b in Building.All) if (b.Def.commandCenter && !b.IsGhost && !b.Ruined) return b;
            return null;
        }

        Vector3 HomeFor(int index)
        {
            var camps = new List<Building>();
            foreach (var b in Building.All) if (!b.IsGhost && !b.Ruined && b.Def.campSpace > 0) camps.Add(b);
            if (camps.Count == 0) return gm.Castle.Center + new Vector3(0, 0, -3.5f);
            var c = camps[index % camps.Count];
            int slot = index / camps.Count;
            // parade-ground rows on the camp's own pad (5 x 5, 0.42 apart), a second staggered layer after 25,
            // never spilling outside the pad
            float half = Mathf.Max(c.Def.width, c.Def.depth) * 0.5f - 0.35f;
            int layer = slot / 25, k = slot % 25;
            float x = -0.84f + (k % 5) * 0.42f + layer * 0.21f, z = 0.84f - (k / 5) * 0.42f - layer * 0.21f;
            return c.Center + new Vector3(Mathf.Clamp(x, -half, half), 0, Mathf.Clamp(z, -half, half));
        }

        void Sync()
        {
            int index = 0;
            foreach (var d in UnitDef.Ordered)
            {
                List<Unit> list;
                if (!units.TryGetValue(d.id, out list)) { list = new List<Unit>(); units[d.id] = list; }
                int want = Count(d.id);
                list.RemoveAll(u => u == null);
                while (list.Count > want) { Destroy(list[list.Count - 1].gameObject); list.RemoveAt(list.Count - 1); }
                for (int i = 0; i < list.Count; i++) list[i].home = HomeFor(index + i);
                while (list.Count < want) list.Add(Unit.Spawn(d, HomeFor(index + list.Count), root));
                index += want;
            }
            // two patrol drones per Drone Pad (they guard the pad, hit air and ground)
            var pads = new List<Building>();
            foreach (var b in Building.All) if (b.Def.id == "dronepad" && !b.IsGhost && !b.Ruined && !b.UnderConstruction) pads.Add(b);
            List<Unit> patrols;
            if (!units.TryGetValue("patrol", out patrols)) { patrols = new List<Unit>(); units["patrol"] = patrols; }
            patrols.RemoveAll(u => u == null);
            while (patrols.Count > pads.Count * 2) { Destroy(patrols[patrols.Count - 1].gameObject); patrols.RemoveAt(patrols.Count - 1); }
            for (int i = 0; i < pads.Count * 2; i++)
            {
                var h = pads[i / 2].Center + new Vector3(i % 2 == 0 ? -0.8f : 0.8f, 0, 0);
                if (i < patrols.Count) patrols[i].home = h; else patrols.Add(Unit.Spawn(UnitDef.Patrol, h, root));
            }

            var cc = CommandCenter();
            if (cc == null) { if (Hero != null) { Destroy(Hero.gameObject); Hero = null; } }
            else if (Hero == null) Hero = Unit.SpawnHero(gm.State.heroLevel, cc.Center + new Vector3(0, 0, -1.6f), root);
            else Hero.home = cc.Center + new Vector3(0, 0, -1.6f);
        }

        // ------------------------------------------------------------------ XP and strike

        public void AddXp(int xp)
        {
            if (!HasCommandCenter) return;
            var s = gm.State;
            if (s.heroLevel >= MaxHeroLevel) return;
            s.heroXp += xp;
            if (s.heroXp >= XpToReach[s.heroLevel + 1])
            {
                s.heroLevel++;
                gm.Toast("Commander reached level " + s.heroLevel + "!");
                if (Hero != null)
                {
                    DamageEffects.LevelUp(Hero.transform.position + Vector3.up, 1.2f);
                    Destroy(Hero.gameObject); Hero = null;   // respawns with the new stats
                    Sync();
                }
            }
        }

        public float XpProgress
        {
            get
            {
                var s = gm.State;
                if (s.heroLevel >= MaxHeroLevel) return 1f;
                int a = XpToReach[s.heroLevel], b = XpToReach[s.heroLevel + 1];
                return Mathf.Clamp01((s.heroXp - a) / (float)(b - a));
            }
        }

        public bool StrikeReady { get { return Hero != null && !Hero.KnockedOut && Time.time >= StrikeReadyAt && Enemy.All.Count > 0; } }

        /// <summary>Orbital Strike on the biggest group of attackers: red marker, then the blast.</summary>
        public bool Strike()
        {
            if (!StrikeReady) return false;
            Vector3 best = Vector3.zero; int bestN = 0;
            foreach (var e in Enemy.All)
            {
                if (!e.Visible) continue;
                int n = 0;
                foreach (var o in Enemy.All) if ((o.transform.position - e.transform.position).sqrMagnitude < StrikeRadius * StrikeRadius) n += o.kind == EnemyKind.Boss ? 3 : 1;
                if (n > bestN) { bestN = n; best = e.transform.position; }
            }
            if (bestN == 0) return false;
            StrikeReadyAt = Time.time + StrikeCooldown;
            StartCoroutine(StrikeAt(best));
            return true;
        }

        void AutoStrike()
        {
            if (!StrikeReady) return;
            int near = 0;
            foreach (var e in Enemy.All) if (e.Visible && (e.transform.position - gm.Castle.Center).sqrMagnitude < 26f * 26f) near++;
            if (near >= 3 || (near > 0 && Enemy.All.Exists(x => x.kind == EnemyKind.Boss))) Strike();
        }

        System.Collections.IEnumerator StrikeAt(Vector3 p)
        {
            p.y = WorldBuilder.HeightAt(p);
            var marker = Util.Prim(PrimitiveType.Cylinder, null, p + Vector3.up * 0.05f, new Vector3(StrikeRadius * 2f, 0.02f, StrikeRadius * 2f), new Color(1f, 0.2f, 0.15f));
            marker.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(1f, 0.15f, 0.1f), 1.5f);
            AudioManager.Play("horn", 0.4f);
            float t = 0f;
            while (t < StrikeDelay)
            {
                t += Time.deltaTime;
                float pulse = 0.85f + 0.15f * Mathf.Sin(t * 20f);
                marker.transform.localScale = new Vector3(StrikeRadius * 2f * pulse, 0.02f, StrikeRadius * 2f * pulse);
                yield return null;
            }
            Destroy(marker);
            AudioManager.PlayAt("blast", p, 1f);
            DamageEffects.Puff(p + Vector3.up * 0.3f, 2.4f);
            foreach (var e in Enemy.All.ToArray())
            {
                var d = e.transform.position - p; d.y = 0f;
                if (d.magnitude <= StrikeRadius) e.Hit(StrikeDamage);
            }
            // a thin cyan laser from the sky that narrows and fades in a blink (not a solid pillar)
            var beam = Util.Prim(PrimitiveType.Cylinder, null, p + Vector3.up * 12f, new Vector3(0.35f, 12f, 0.35f), new Color(0.6f, 0.95f, 1f));
            beam.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(0.45f, 0.9f, 1f), 3f);
            for (float k = 0f; k < 0.18f; k += Time.deltaTime)
            {
                float w = Mathf.Lerp(0.35f, 0.02f, k / 0.18f);
                beam.transform.localScale = new Vector3(w, 12f, w);
                yield return null;
            }
            Destroy(beam);
        }
    }
}
