using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Builds the static world: ground, mountain behind the city (north), sea (south),
    /// forest on both sides, scattered trees inside, and the build-area border.
    /// Placeholder geometry for now; swap in free 3D packs (Kenney / Quaternius) via Resources/Models.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public Transform TreesRoot { get; private set; }
        Transform borderRoot;
        int borderRadius = -1;

        public void Build(GridMap grid, int seed)
        {
            Random.InitState(seed);
            var root = transform;

            var grass = new Color(0.36f, 0.52f, 0.25f);
            var ground = Util.Prim(PrimitiveType.Plane, root, Vector3.zero, new Vector3(16f, 1f, 16f), grass, keepCollider: true);
            ground.name = "Ground";
            ground.layer = 0;

            // Sea to the south + beach
            var sea = Util.Prim(PrimitiveType.Plane, root, new Vector3(0f, 0.02f, GridMap.SeaZ - 40f), new Vector3(16f, 1f, 8f), new Color(0.13f, 0.33f, 0.48f));
            sea.name = "Sea";
            sea.GetComponent<Renderer>().sharedMaterial = Materials.Get(new Color(0.13f, 0.33f, 0.48f));
            var beach = Util.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.01f, GridMap.SeaZ + 0.5f), new Vector3(160f, 0.02f, 3f), new Color(0.86f, 0.79f, 0.6f));
            beach.name = "Beach";

            // Mountain range to the north
            var rock = new Color(0.45f, 0.43f, 0.42f);
            var rockDark = new Color(0.36f, 0.35f, 0.35f);
            var snow = new Color(0.93f, 0.94f, 0.96f);
            for (int i = 0; i < 14; i++)
            {
                float x = -60f + i * 9f + Random.Range(-3f, 3f);
                float h = Random.Range(10f, 22f);
                float w = Random.Range(12f, 20f);
                float z = GridMap.MountainZ + 6f + Random.Range(0f, 10f);
                var m = Util.Prim(PrimitiveType.Sphere, root, new Vector3(x, 0f, z), new Vector3(w, h * 2f, w * 0.8f), i % 2 == 0 ? rock : rockDark);
                m.name = "Mountain";
                if (h > 15f)
                    Util.Prim(PrimitiveType.Sphere, root, new Vector3(x, h * 0.86f, z), new Vector3(w * 0.32f, h * 0.35f, w * 0.28f), snow).name = "Snow";
            }
            // foothill boulders
            for (int i = 0; i < 30; i++)
            {
                float s = Random.Range(0.8f, 2.4f);
                Util.Prim(PrimitiveType.Sphere, root, new Vector3(Random.Range(-40f, 40f), 0.1f, GridMap.MountainZ + Random.Range(0f, 4f)),
                          new Vector3(s * 1.3f, s, s), rock).name = "Boulder";
            }

            // Forest (both sides) and scattered groves inside
            TreesRoot = new GameObject("Trees").transform;
            TreesRoot.SetParent(root, false);
            for (int x = -GridMap.Half - 10; x < GridMap.Half + 10; x++)
            {
                for (int z = GridMap.SeaZ + 1; z < GridMap.MountainZ + 2; z++)
                {
                    int ax = Mathf.Abs(x);
                    bool deepForest = ax >= GridMap.ForestX;
                    bool forestEdge = ax >= GridMap.ForestX - 4 && ax < GridMap.ForestX;
                    float chance = deepForest ? 0.35f : forestEdge ? 0.12f : 0f;
                    if (chance > 0f && Random.value < chance)
                        PlaceTree(grid, new Vector2Int(x, z));
                }
            }
            for (int g = 0; g < 7; g++)
            {
                var center = new Vector2(Random.Range(-18f, 18f), Random.Range(-16f, 16f));
                if (center.magnitude < 9f) continue; // keep space around the castle
                int n = Random.Range(4, 9);
                for (int i = 0; i < n; i++)
                {
                    var c = new Vector2Int(Mathf.RoundToInt(center.x + Random.Range(-2.5f, 2.5f)), Mathf.RoundToInt(center.y + Random.Range(-2.5f, 2.5f)));
                    if (grid.At(c) == null) PlaceTree(grid, c);
                }
            }

            borderRoot = new GameObject("BuildBorder").transform;
            borderRoot.SetParent(root, false);
        }

        void PlaceTree(GridMap grid, Vector2Int cell)
        {
            if (grid.At(cell) != null) return;
            var t = TreeNode.Create(TreesRoot, cell, Random.Range(0.8f, 1.3f));
            grid.Occupy(new[] { cell }, t);
        }

        /// <summary>Low stone markers around the area the castle level allows building in.</summary>
        public void ShowBuildBorder(int radius)
        {
            if (radius == borderRadius) return;
            borderRadius = radius;
            for (int i = borderRoot.childCount - 1; i >= 0; i--) Destroy(borderRoot.GetChild(i).gameObject);
            var c = new Color(0.72f, 0.68f, 0.6f);
            for (int i = -radius; i <= radius; i += 2)
            {
                Marker(new Vector3(i, 0f, -radius), c);
                Marker(new Vector3(i, 0f, radius), c);
                Marker(new Vector3(-radius, 0f, i), c);
                Marker(new Vector3(radius, 0f, i), c);
            }
        }

        void Marker(Vector3 p, Color c)
        {
            if (!GridMap.IsTerrainBuildable(GridMap.CellOf(p)) && !GridMap.IsTerrainBuildable(GridMap.CellOf(p) - Vector2Int.one)) return;
            Util.Prim(PrimitiveType.Cube, borderRoot, p + Vector3.up * 0.08f, new Vector3(0.25f, 0.16f, 0.25f), c);
        }
    }
}
