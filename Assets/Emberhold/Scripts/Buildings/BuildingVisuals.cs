using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Creates the 3D look of a building. If a prefab exists at Resources/Models/{id}
    /// (e.g. from a free Kenney or Quaternius pack) it is used; otherwise a placeholder is built.
    /// </summary>
    public static class BuildingVisuals
    {
        public static GameObject Create(BuildingDef def, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(def.PrefabPath);
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab, parent, false);
                inst.name = "Model";
                return inst;
            }

            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            float w = def.width, d = def.depth, h = def.height;
            var c = def.color;
            var roof = new Color(0.55f, 0.22f, 0.16f);
            var wood = new Color(0.42f, 0.3f, 0.19f);
            var stone = new Color(0.62f, 0.6f, 0.57f);

            switch (def.id)
            {
                case BuildingCatalog.Castle:
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.35f, 0), new Vector3(w * 0.8f, h * 0.7f, d * 0.8f), c);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.85f, 0), new Vector3(w * 0.4f, h * 0.5f, d * 0.4f), c * 0.95f);
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i % 2 == 0 ? -1 : 1) * w * 0.4f, sz = (i < 2 ? -1 : 1) * d * 0.4f;
                        Util.Prim(PrimitiveType.Cylinder, t, new Vector3(sx, h * 0.5f, sz), new Vector3(0.9f, h * 0.5f, 0.9f), stone);
                        Util.Prim(PrimitiveType.Cube, t, new Vector3(sx, h * 1.02f, sz), new Vector3(1.0f, 0.15f, 1.0f), stone * 0.9f);
                    }
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.2f, -d * 0.41f), new Vector3(0.9f, h * 0.4f, 0.05f), wood); // gate
                    Util.Prim(PrimitiveType.Cylinder, t, new Vector3(0, h * 1.25f, 0), new Vector3(0.05f, 0.6f, 0.05f), wood);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0.3f, h * 1.25f + 0.4f, 0), new Vector3(0.6f, 0.35f, 0.02f), new Color(0.7f, 0.15f, 0.15f)); // flag
                    break;

                case "house":
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.4f, 0), new Vector3(w * 0.75f, h * 0.8f, d * 0.7f), c);
                    var r1 = Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.95f, 0), new Vector3(w * 0.85f, w * 0.55f, w * 0.55f), roof);
                    r1.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    r1.transform.localScale = new Vector3(w * 0.6f, w * 0.6f, d * 0.78f);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.25f, -d * 0.36f), new Vector3(0.3f, h * 0.5f, 0.04f), wood);
                    break;

                case "farm":
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.03f, 0), new Vector3(w * 0.95f, 0.06f, d * 0.95f), new Color(0.45f, 0.32f, 0.2f));
                    for (int row = 0; row < 5; row++)
                        Util.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.15f, -d * 0.38f + row * d * 0.19f), new Vector3(w * 0.85f, 0.22f, 0.22f), c);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f;
                        var postPos = new Vector3(Mathf.Lerp(-w, w, a) * 0.48f, 0.25f, d * 0.48f);
                        Util.Prim(PrimitiveType.Cube, t, postPos, new Vector3(0.06f, 0.5f, 0.06f), wood);
                        Util.Prim(PrimitiveType.Cube, t, new Vector3(postPos.x, 0.25f, -d * 0.48f), new Vector3(0.06f, 0.5f, 0.06f), wood);
                    }
                    break;

                case "lumberjack":
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(-0.2f, h * 0.4f, 0), new Vector3(w * 0.55f, h * 0.8f, d * 0.6f), c);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(-0.2f, h * 0.9f, 0), new Vector3(w * 0.65f, 0.15f, d * 0.7f), roof * 0.8f);
                    for (int i = 0; i < 3; i++)
                    {
                        var log = Util.Prim(PrimitiveType.Cylinder, t, new Vector3(0.65f, 0.12f + i * 0.2f, -0.3f + (i % 2) * 0.1f), new Vector3(0.18f, 0.5f, 0.18f), wood);
                        log.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    }
                    break;

                case "quarry":
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0), new Vector3(w * 0.9f, 0.1f, d * 0.9f), new Color(0.4f, 0.38f, 0.36f));
                    for (int i = 0; i < 5; i++)
                        Util.Prim(PrimitiveType.Cube, t, new Vector3(Random.Range(-0.6f, 0.6f), 0.2f + Random.Range(0, 0.3f), Random.Range(-0.6f, 0.6f)),
                                  new Vector3(0.4f, 0.35f, 0.4f) * Random.Range(0.8f, 1.4f), stone);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0.7f, 0.5f, 0.7f), new Vector3(0.08f, 1f, 0.08f), wood);
                    break;

                case "workshop":
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.4f, 0), new Vector3(w * 0.8f, h * 0.8f, d * 0.75f), c);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.85f, 0), new Vector3(w * 0.9f, 0.2f, d * 0.85f), new Color(0.3f, 0.3f, 0.32f));
                    Util.Prim(PrimitiveType.Cylinder, t, new Vector3(0.5f, h * 1.05f, 0.4f), new Vector3(0.22f, 0.4f, 0.22f), stone * 0.7f); // chimney
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(-0.55f, 0.25f, -0.85f), new Vector3(0.35f, 0.35f, 0.25f), new Color(0.2f, 0.2f, 0.22f)); // anvil
                    break;

                case "wall":
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.5f, 0), new Vector3(1f, h, 0.7f), c);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(-0.3f, h + 0.12f, 0), new Vector3(0.3f, 0.24f, 0.72f), c * 0.95f);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0.3f, h + 0.12f, 0), new Vector3(0.3f, 0.24f, 0.72f), c * 0.95f);
                    break;

                case "torch":
                    Util.Prim(PrimitiveType.Cylinder, t, new Vector3(0, h * 0.5f, 0), new Vector3(0.1f, h * 0.5f, 0.1f), c);
                    var flame = Util.Prim(PrimitiveType.Sphere, t, new Vector3(0, h + 0.1f, 0), new Vector3(0.22f, 0.3f, 0.22f), Color.white);
                    flame.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(1f, 0.6f, 0.2f), 3f);
                    break;

                case "watchtower":
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i % 2 == 0 ? -1 : 1) * 0.6f, sz = (i < 2 ? -1 : 1) * 0.6f;
                        Util.Prim(PrimitiveType.Cube, t, new Vector3(sx, h * 0.4f, sz), new Vector3(0.15f, h * 0.8f, 0.15f), wood);
                    }
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.8f, 0), new Vector3(1.6f, 0.12f, 1.6f), wood * 1.1f);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.8f + 0.3f, 0.75f), new Vector3(1.6f, 0.5f, 0.08f), wood);
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.8f + 0.3f, -0.75f), new Vector3(1.6f, 0.5f, 0.08f), wood);
                    var top = Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 1.05f, 0), new Vector3(1.3f, 1.3f, 1.3f), roof);
                    top.transform.localRotation = Quaternion.Euler(45, 45, 0);
                    top.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
                    var fire = Util.Prim(PrimitiveType.Sphere, t, new Vector3(0, h * 0.8f + 0.25f, 0), Vector3.one * 0.25f, Color.white);
                    fire.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(1f, 0.6f, 0.2f), 2.5f);
                    break;

                default:
                    Util.Prim(PrimitiveType.Cube, t, new Vector3(0, h * 0.5f, 0), new Vector3(w * 0.9f, h, d * 0.9f), c);
                    break;
            }
            return root;
        }
    }
}
