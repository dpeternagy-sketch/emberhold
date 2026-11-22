using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Shows a building as one of Peter's pre-rendered isometric images instead of a 3D model.
    /// Images live in Resources/Sprites/Buildings/{id}_t{1|3|5}.png with a glow mask ({..}_emis.png)
    /// and a sidecar ({..}.txt: anchor x, anchor y, plate width, in pixels from the top-left), all made
    /// by Tools/sprite_cutout.py. The level picks the highest tier image at or below it.
    ///
    /// Sits on the building's "Model" object so Building's existing animations still drive it:
    /// model scale Y = construction reveal, model position = hit shake, odd rotation = mirror flip.
    /// The quad itself hangs off the building root, so those transforms never distort the picture.
    /// </summary>
    public class SpriteBuilding : MonoBehaviour
    {
        public const string QuadName = "IsoSprite";
        static readonly int[] Tiers = { 1, 3, 5 };

        class Art { public Texture2D tex, emis; public float cx, cy, plate; }
        static readonly Dictionary<string, Art> cache = new Dictionary<string, Art>();
        static Shader shader;

        BuildingDef def;
        Building building;
        Transform quad;
        Material mat;
        Art art;
        string shownKey;
        Color overlay = new Color(1, 1, 1, 0);

        static Art Load(string key)
        {
            Art a;
            if (cache.TryGetValue(key, out a)) return a;
            var tex = Resources.Load<Texture2D>("Sprites/Buildings/" + key);
            var meta = Resources.Load<TextAsset>("Sprites/Buildings/" + key);
            a = null;
            if (tex != null && meta != null)
            {
                var p = meta.text.Trim().Split(' ');
                a = new Art
                {
                    tex = tex,
                    emis = Resources.Load<Texture2D>("Sprites/Buildings/" + key + "_emis"),
                    cx = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
                    cy = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                    plate = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
                };
            }
            cache[key] = a;
            return a;
        }

        static int LowestTier(string id)
        {
            foreach (var t in Tiers) if (Load(id + "_t" + t) != null) return t;
            return 0;
        }

        public static bool Has(string id)
        {
            if (id == "wall") return Load("wallpost_t1") != null;
            if (id == "wallgun") return Load("wallpost_t1") != null && LowestTier("watchtower") > 0;
            return LowestTier(id) > 0;
        }

        /// <summary>Build-menu picture: the lowest-tier image itself.</summary>
        public static Texture2D Thumbnail(string id)
        {
            if (id == "wall") { var w = Load("wallseg_t1") ?? Load("wallpost_t1"); return w != null ? w.tex : null; }
            if (id == "wallgun") id = "watchtower";
            int t = LowestTier(id); return t > 0 ? Load(id + "_t" + t).tex : null;
        }

        /// <summary>
        /// The three images (files _t1, _t3, _t5) are spread over the building's level range:
        /// max 3 -> L1, L2, L3; max 5 -> L1-2, L3-4, L5. Falls back to the nearest lower image we have,
        /// or the lowest one until the L1 art exists.
        /// </summary>
        static int TierFor(BuildingDef def, int level)
        {
            int idx = def.maxLevel <= 1 ? 0 : Mathf.Clamp(2 * (level - 1) / (def.maxLevel - 1), 0, 2);
            int want = Tiers[idx];
            int best = LowestTier(def.id);
            foreach (var t in Tiers) if (t <= want && Load(def.id + "_t" + t) != null) best = t;
            return best;
        }

        public static GameObject Create(BuildingDef def, Transform parent)
        {
            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var sb = root.AddComponent<SpriteBuilding>();
            sb.def = def;
            sb.quad = NewQuad(parent, out sb.mat);
            if (def.id == "wall" || def.id == "wallgun")
            {
                sb.wall = true;
                sb.gun = def.id == "wallgun";
                sb.segX = NewQuad(parent, out sb.matX);
                sb.segZ = NewQuad(parent, out sb.matZ);
            }
            else AddGroundShadow(def, parent);
            return root;
        }

        public const string ShadowName = "SpriteShadow";
        static Material shadowMat;

        /// <summary>
        /// Peter: a fixed soft shadow on the grass, falling lower-right (his pictures are lit from the
        /// top-left), so the building sits on the ground instead of looking pasted on. Drawn after the
        /// ground and before the buildings, so it never darkens a plate.
        /// </summary>
        static void AddGroundShadow(BuildingDef def, Transform parent)
        {
            if (shadowMat == null)
            {
                shadowMat = DamageEffects.ParticleMaterial(false, DamageEffects.SoftDot);
                var dark = new Color(0.02f, 0.06f, 0.04f, 0.42f);
                if (shadowMat.HasProperty("_BaseColor")) shadowMat.SetColor("_BaseColor", dark);
                if (shadowMat.HasProperty("_Color")) shadowMat.SetColor("_Color", dark);
                shadowMat.renderQueue = 2400;   // after the ground (2000), before the building pictures (2450)
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = ShadowName;
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = shadowMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            float size = Mathf.Max(def.width, def.depth);
            var right = Quaternion.Euler(0f, CameraRig.Yaw, 0f) * Vector3.right;      // screen right, on the ground
            var away = Quaternion.Euler(0f, CameraRig.Yaw, 0f) * Vector3.back;        // screen down, on the ground
            q.transform.localPosition = (right * 0.32f + away * 0.12f) * size + Vector3.up * 0.03f;   // the building root is never rotated
            q.transform.rotation = Quaternion.Euler(90f, CameraRig.Yaw, 0f);
            q.transform.localScale = new Vector3(size * 1.75f, size * 1.25f, 1f);
        }

        static Transform NewQuad(Transform parent, out Material m)
        {
            if (shader == null) shader = Resources.Load<Shader>("Shaders/IsoSprite");
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = QuadName;
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            m = new Material(shader);
            r.sharedMaterial = m;
            return q.transform;
        }

        /// <summary>
        /// Puts an image on a camera-facing quad so that its plate centre (art.cx, art.cy) lands on ground,
        /// with the plate's pixel width (art.plate) covering diamond world units across the screen.
        /// </summary>
        static void Place(Transform q, Material m, Art a, Vector3 ground, float diamond, bool flip, Vector3 shake)
        {
            var rot = Camera.main.transform.rotation;
            float w = diamond * a.tex.width / a.plate;
            float ox = (a.cx / a.tex.width - 0.5f) * w * (flip ? -1f : 1f);
            float oy = (0.5f - a.cy / a.tex.height) * w;
            q.rotation = rot;
            q.localScale = new Vector3(flip ? -w : w, w, 1f);
            q.position = ground - (rot * Vector3.right) * ox - (rot * Vector3.up) * oy + shake;
            var fwd = rot * Vector3.forward;
            m.SetVector("_Center", ground);
            m.SetVector("_Normal", -new Vector3(fwd.x, 0f, fwd.z).normalized);
        }

        static void Show(Material m, Art a)
        {
            m.SetTexture("_MainTex", a.tex);
            m.SetTexture("_EmisTex", a.emis != null ? a.emis : Texture2D.blackTexture);
        }

        /// <summary>Ghost colouring (placement) and charred ruins: Building.SetTint routes here.</summary>
        public void SetTint(Color? tint)
        {
            overlay = tint.HasValue ? new Color(tint.Value.r, tint.Value.g, tint.Value.b, 0.4f) : new Color(1, 1, 1, 0);
        }

        /// <summary>Day/night look for every building image. dark: 0 = noon, 1 = midnight.</summary>
        public static void SetLighting(float dark)
        {
            Shader.SetGlobalColor("_EH_SpriteTint", Color.Lerp(Color.white, new Color(0.46f, 0.52f, 0.78f, 1f), dark));
            Shader.SetGlobalFloat("_EH_SpriteGlow", Mathf.Lerp(0.05f, 0.75f, dark));
        }

        /// <summary>Destroyed buildings show a shared ruin image sized by footprint (small, medium, large).</summary>
        static string RuinKey(BuildingDef d)
        {
            if (d.indestructible || d.passable || d.id == "gate") return null;
            int s = Mathf.Max(d.width, d.depth);
            string k = s <= 2 ? "ruin_t1" : s == 3 ? "ruin_t3" : "ruin_t5";
            return Load(k) != null ? k : null;
        }

        void OnDestroy()
        {
            if (quad != null) Destroy(quad.gameObject);
            if (mat != null) Destroy(mat);
            if (segX != null) Destroy(segX.gameObject);
            if (segZ != null) Destroy(segZ.gameObject);
            if (matX != null) Destroy(matX);
            if (matZ != null) Destroy(matZ);
        }

        // ---------- walls ----------
        // A wall cell shows a straight segment (cap to cap, one cell long) to each neighbour wall or gate on +x / +z,
        // and towards a gate on -x / -z (gates don't draw segments). Corners, ends and junctions also get a post.
        // The segment image runs along +x (lower-left to upper-right on screen); +z segments are its mirror.
        // Walls have no upgrades of their own: their look follows the HQ level (HQ 1-2, 3-4, 5+).
        const float PostDiamond = 0.36f;           // post footprint width across the screen, world units
        const float SegDiamond = 0.70710678f;      // one cell along an axis, seen from 45 degrees
        bool wall, gun;
        Transform segX, segZ;
        const float GunDiamond = 1.0f;   // a wall gun: the gun turret image shrunk onto one wall cell
        Material matX, matZ;

        static int WallTier()
        {
            var gm = GameManager.Instance;
            int hq = gm != null ? gm.CastleLevel : 1;
            int t = Tiers[Mathf.Clamp((hq - 1) / 2, 0, 2)];
            while (t > 1 && Load("wallseg_t" + t) == null) t -= 2;
            return t;
        }

        static int WallAt(Vector2Int c)
        {
            var gm = GameManager.Instance;
            var o = gm != null && gm.Grid != null ? gm.Grid.At(c) as Building : null;
            if (o == null || o.IsGhost) return 0;
            return o.Def.id == "wall" || o.Def.id == "wallgun" ? 1 : o.Def.id == "gate" ? 2 : 0;
        }

        void WallUpdate()
        {
            if (building == null) building = GetComponentInParent<Building>();
            if (building == null || building.Data == null) return;
            int t = WallTier();
            var c = new Vector2Int(building.Data.x, building.Data.z);
            int l = WallAt(c + Vector2Int.left), r = WallAt(c + Vector2Int.right);
            int d = WallAt(c + Vector2Int.down), u = WallAt(c + Vector2Int.up);
            bool straight = (l > 0 && r > 0 && u == 0 && d == 0) || (u > 0 && d > 0 && l == 0 && r == 0);
            var ground = transform.parent.position;
            var shake = transform.localPosition;
            var seg = Load("wallseg_t" + t);
            var post = Load("wallpost_t" + t);

            // +x segment (or -x into a gate)
            // no segment runs into a gate: the gate picture itself spans from this cell to the next wall cell
            bool gateArt = LowestTier("gate") > 0;
            Vector3? xc = (r == 1 || (r == 2 && !gateArt)) ? ground + new Vector3(0.5f, 0, 0) : (l == 2 && !gateArt) ? ground - new Vector3(0.5f, 0, 0) : (Vector3?)null;
            Vector3? zc = (u == 1 || (u == 2 && !gateArt)) ? ground + new Vector3(0, 0, 0.5f) : (d == 2 && !gateArt) ? ground - new Vector3(0, 0, 0.5f) : (Vector3?)null;
            SetSeg(segX, matX, seg, xc, false, shake);
            SetSeg(segZ, matZ, seg, zc, true, shake);

            if (gun)
            {
                // the gun turret image, shrunk to sit on this wall cell (its level picks the image)
                string key = "watchtower_t" + TierFor(BuildingCatalog.Get("watchtower") ?? def, building.Data.level);
                var g = Load(key);
                quad.gameObject.SetActive(g != null);
                if (g == null) return;
                if (key != shownKey) { shownKey = key; Show(mat, g); }
                Place(quad, mat, g, ground, GunDiamond, false, shake);
                Finish(mat);
                if (building.Ruined) { mat.SetFloat("_Shade", 0.42f); mat.SetColor("_Overlay", new Color(0.2f, 0.17f, 0.15f, 0.35f)); }
                return;
            }
            bool showPost = post != null && !straight;
            quad.gameObject.SetActive(showPost);
            if (showPost)
            {
                string key = "wallpost_t" + t;
                if (key != shownKey) { shownKey = key; Show(mat, post); }
                Place(quad, mat, post, ground, PostDiamond, false, shake);
                Finish(mat);
            }
        }

        void SetSeg(Transform q, Material m, Art a, Vector3? centre, bool flip, Vector3 shake)
        {
            bool on = a != null && centre.HasValue;
            q.gameObject.SetActive(on);
            if (!on) return;
            if (m.GetTexture("_MainTex") != a.tex) Show(m, a);
            Place(q, m, a, centre.Value, SegDiamond, flip, shake);
            Finish(m);
        }

        void Finish(Material m)
        {
            bool building_ = building.UnderConstruction && !building.IsGhost;
            float reveal = building_ ? Mathf.Clamp01(transform.localScale.y) : 1f;
            m.SetFloat("_Cut", Mathf.Lerp(0.18f, 1f, reveal));
            m.SetFloat("_Shade", 1f);
            m.SetColor("_Overlay", overlay);
        }

        void LateUpdate()
        {
            if (quad == null || Camera.main == null) return;
            if (wall) { WallUpdate(); return; }
            if (building == null) building = GetComponentInParent<Building>();
            int level = building != null && building.Data != null ? building.Data.level : 1;
            string key = def.id + "_t" + TierFor(def, level);
            bool ruinArt = false;
            if (building != null && building.Ruined)
            {
                var rk = RuinKey(def);
                if (rk != null) { key = rk; ruinArt = true; }
            }
            if (key != shownKey)
            {
                shownKey = key;
                art = Load(key);
                if (art == null) { quad.gameObject.SetActive(false); return; }
                mat.SetTexture("_MainTex", art.tex);
                mat.SetTexture("_EmisTex", art.emis != null ? art.emis : Texture2D.blackTexture);
            }
            if (art == null) return;

            var cam = Camera.main;
            if (cam == null) return;
            var rot = cam.transform.rotation;
            int rotation = building != null && building.Data != null ? building.Data.rotation : 0;
            bool flip = (rotation & 1) == 1;
            if (def.id == "gate" && building != null && building.Data != null)
            {
                // the gate picture runs along +x; in a wall that runs along z it is mirrored
                var gc = new Vector2Int(building.Data.x, building.Data.z);
                flip = WallAt(gc + Vector2Int.up) > 0 || WallAt(gc + Vector2Int.down) > 0;
            }
            var size = GridMap.Size(def, rotation);
            float diamond = Mathf.Max(size.x, size.y) * 1.41421356f;     // footprint diagonal as seen from 45 degrees
            float w = diamond * art.tex.width / art.plate;
            float ox = (art.cx / art.tex.width - 0.5f) * w * (flip ? -1f : 1f);
            float oy = (0.5f - art.cy / art.tex.height) * w;

            var ground = transform.parent.position;
            quad.rotation = rot;
            quad.localScale = new Vector3(flip ? -w : w, w, 1f);
            quad.position = ground - (rot * Vector3.right) * ox - (rot * Vector3.up) * oy + transform.localPosition;

            bool ruined = building != null && building.Ruined;
            bool building_ = building != null && building.UnderConstruction && !building.IsGhost;
            float levelScale = 1f + 0.06f * (level - 1);
            float reveal = building_ ? Mathf.Clamp01(transform.localScale.y / levelScale) : 1f;
            var fwd = rot * Vector3.forward;
            var n = -new Vector3(fwd.x, 0f, fwd.z).normalized;
            mat.SetVector("_Center", ground);
            mat.SetVector("_Normal", n);
            mat.SetFloat("_Cut", ruined ? 1f : Mathf.Lerp(0.18f, 1f, reveal));
            mat.SetFloat("_Shade", ruined && !ruinArt ? 0.42f : 1f);
            mat.SetColor("_Overlay", ruined && !ruinArt ? new Color(0.2f, 0.17f, 0.15f, 0.35f) : (ruined ? new Color(1, 1, 1, 0) : overlay));
        }
    }
}
