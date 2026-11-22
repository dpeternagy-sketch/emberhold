using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Ground enemies follow this distance field to the HQ. Walls can never be broken, so they are
    /// solid here; gates and other buildings can be smashed, so they only cost extra. That makes
    /// walking dinosaurs funnel to the gates, where the player puts the turrets.
    /// The player can never seal the city completely (see GameManager.WouldSeal).
    /// </summary>
    public static class PathField
    {
        const int N = GridMap.Half * 2;
        const float Inf = 1e9f;
        static readonly float[] dist = new float[N * N];
        static bool dirty = true;
        static float builtAt = -99f;

        public static void MarkDirty() { dirty = true; }

        static int Idx(Vector2Int c) { return (c.x + GridMap.Half) * N + (c.y + GridMap.Half); }
        public static bool Inside(Vector2Int c) { return c.x >= -GridMap.Half && c.x < GridMap.Half && c.y >= -GridMap.Half && c.y < GridMap.Half; }

        /// <summary>Ground terrain enemies can never walk on: the mountain and the sea.</summary>
        public static bool TerrainBlocked(Vector2Int c) { return c.y >= GridMap.MountainZ || c.y < GridMap.SeaZ; }

        public static bool IsSolidWall(object o)
        {
            var b = o as Building;
            return b != null && !b.IsGhost && b.Def.indestructible;
        }

        static float Cost(GridMap g, Vector2Int c)
        {
            if (TerrainBlocked(c)) return Inf;
            var o = g.At(c);
            if (IsSolidWall(o)) return Inf;
            var b = o as Building;
            if (b != null && !b.IsGhost && !b.Def.passable && !b.Ruined) return b == GameManager.Instance.Castle ? 1f : 7f;   // smash through
            if (o is RiverCell) return 2f;
            return 1f;
        }

        static readonly Vector2Int[] Dirs4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        static readonly Vector2Int[] Dirs8 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                                               new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };

        static void Rebuild()
        {
            var gm = GameManager.Instance;
            dirty = false; builtAt = Time.time;
            for (int i = 0; i < dist.Length; i++) dist[i] = Inf;
            if (gm == null || gm.Castle == null) return;
            // simple bucketed Dijkstra (small integer-ish costs)
            var open = new List<KeyValuePair<float, Vector2Int>>();
            foreach (var c in gm.Castle.Cells) { if (!Inside(c)) continue; dist[Idx(c)] = 0f; open.Add(new KeyValuePair<float, Vector2Int>(0f, c)); }
            var heap = new SortedSet<(float, int, int)>();
            foreach (var kv in open) heap.Add((0f, kv.Value.x, kv.Value.y));
            while (heap.Count > 0)
            {
                var top = heap.Min; heap.Remove(top);
                var c = new Vector2Int(top.Item2, top.Item3);
                float d = top.Item1;
                if (d > dist[Idx(c)]) continue;
                foreach (var dir in Dirs4)
                {
                    var n = c + dir;
                    if (!Inside(n)) continue;
                    float cost = Cost(gm.Grid, n);
                    if (cost >= Inf) continue;
                    float nd = d + cost;
                    int ni = Idx(n);
                    if (nd < dist[ni]) { dist[ni] = nd; heap.Add((nd, n.x, n.y)); }
                }
            }
        }

        static void Ensure()
        {
            if (dirty || Time.time - builtAt > 2f) Rebuild();
        }

        public static float DistanceAt(Vector2Int c)
        {
            if (!Inside(c)) return Inf;
            Ensure();
            return dist[Idx(c)];
        }

        /// <summary>The next cell to walk to from here (null if there is no known way).</summary>
        public static Vector2Int? Next(Vector2Int from)
        {
            Ensure();
            if (!Inside(from)) return null;
            float here = dist[Idx(from)];
            Vector2Int? best = null; float bestD = here;
            foreach (var dir in Dirs8)
            {
                var n = from + dir;
                if (!Inside(n)) continue;
                float d = dist[Idx(n)];
                if (d >= Inf) continue;
                if (dir.x != 0 && dir.y != 0)
                {
                    // no cutting corners past a wall
                    if (dist[Idx(from + new Vector2Int(dir.x, 0))] >= Inf || dist[Idx(from + new Vector2Int(0, dir.y))] >= Inf) continue;
                    d += 0.4f;
                }
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        /// <summary>True when a straight walk from a to b crosses no wall.</summary>
        public static bool LineClear(GridMap g, Vector3 a, Vector3 b)
        {
            var d = b - a; d.y = 0;
            int steps = Mathf.CeilToInt(d.magnitude / 0.4f);
            for (int i = 1; i < steps; i++)
            {
                var p = a + d * (i / (float)steps);
                if (IsSolidWall(g.At(GridMap.CellOf(p)))) return false;
            }
            return true;
        }

        /// <summary>
        /// Would a wall on these extra cells shut the HQ in completely? Gates and normal buildings
        /// count as open (they can be broken); only walls, the mountain and the sea are closed.
        /// </summary>
        public static bool WouldSeal(GridMap g, Building castle, ICollection<Vector2Int> extraWalls, int buildRadius)
        {
            if (castle == null) return false;
            var seen = new HashSet<Vector2Int>();
            var q = new Queue<Vector2Int>();
            foreach (var c in castle.Cells) { seen.Add(c); q.Enqueue(c); }
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (!GridMap.InBuildArea(c, buildRadius + 1)) return false;   // got out of the area walls can reach
                foreach (var dir in Dirs4)
                {
                    var n = c + dir;
                    if (seen.Contains(n)) continue;
                    seen.Add(n);
                    if (!Inside(n) || TerrainBlocked(n)) continue;
                    if (extraWalls.Contains(n) || IsSolidWall(g.At(n))) continue;
                    q.Enqueue(n);
                }
            }
            return true;
        }
    }
}
