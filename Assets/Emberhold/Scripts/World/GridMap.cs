using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Square grid, 1 world unit per cell. Cell (x,z) spans [x,x+1] x [z,z+1].
    /// Tracks what occupies each cell (buildings and trees).
    /// </summary>
    public class GridMap
    {
        public const int Half = 30;            // world spans -Half..Half-1
        public const int MountainZ = 22;       // z >= this is mountain
        public const int SeaZ = -22;           // z < this is sea
        public const int ForestX = 24;         // |x| >= this is deep forest

        readonly Dictionary<Vector2Int, object> occupants = new Dictionary<Vector2Int, object>();

        public static IEnumerable<Vector2Int> Footprint(int x, int z, int w, int d, int rotation)
        {
            if ((rotation & 1) == 1) { int t = w; w = d; d = t; }
            for (int i = 0; i < w; i++)
                for (int j = 0; j < d; j++)
                    yield return new Vector2Int(x + i, z + j);
        }

        public static Vector2Int Size(BuildingDef def, int rotation)
        {
            return (rotation & 1) == 1 ? new Vector2Int(def.depth, def.width) : new Vector2Int(def.width, def.depth);
        }

        public static Vector3 FootprintCenter(int x, int z, BuildingDef def, int rotation)
        {
            var s = Size(def, rotation);
            return new Vector3(x + s.x * 0.5f, 0f, z + s.y * 0.5f);
        }

        /// <summary>Grid origin for a footprint whose center should be near the given world point.</summary>
        public static Vector2Int OriginFromCenter(Vector3 world, BuildingDef def, int rotation)
        {
            var s = Size(def, rotation);
            return new Vector2Int(Mathf.RoundToInt(world.x - s.x * 0.5f), Mathf.RoundToInt(world.z - s.y * 0.5f));
        }

        public static Vector2Int CellOf(Vector3 world)
        {
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));
        }

        public static bool IsTerrainBuildable(Vector2Int c)
        {
            return c.x > -ForestX && c.x < ForestX - 1 && c.y >= SeaZ + 2 && c.y < MountainZ - 1;
        }

        /// <summary>The castle sits at the back, against the mountain; the land grows toward the sea (the camera).</summary>
        public const int BuildBackZ = MountainZ - 2;

        public static bool InBuildArea(Vector2Int c, int buildRadius)
        {
            return c.x >= -buildRadius && c.x < buildRadius && c.y < BuildBackZ && c.y >= BuildBackZ - 2 * buildRadius;
        }

        public object At(Vector2Int c)
        {
            object o;
            return occupants.TryGetValue(c, out o) ? o : null;
        }

        public void Occupy(IEnumerable<Vector2Int> cells, object who)
        {
            foreach (var c in cells) occupants[c] = who;
        }

        public void Free(IEnumerable<Vector2Int> cells, object who)
        {
            foreach (var c in cells)
            {
                object o;
                if (occupants.TryGetValue(c, out o) && o == who) occupants.Remove(c);
            }
        }

        /// <summary>buildRadius: half-size of the square area the castle level allows.</summary>
        public bool CanPlace(int x, int z, BuildingDef def, int rotation, object ignore, int buildRadius)
        {
            foreach (var c in Footprint(x, z, def.width, def.depth, rotation))
            {
                if (!IsTerrainBuildable(c)) return false;
                if (!InBuildArea(c, buildRadius)) return false;
                var o = At(c);
                if (o != null && o != ignore) return false;
            }
            return true;
        }
    }
}
