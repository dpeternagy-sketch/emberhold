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

        class Art { public Texture2D tex, emis; public float cx, cy, plate, mountX, mountY; }
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
                if (p.Length >= 5)   // a turret base: where its head sits
                {
                    a.mountX = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
                    a.mountY = float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture);
                }
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
        static Material shadowMat, contactMat;

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

            // contact shadow: a tighter, darker patch right under the plate, so the plate's edge meets
            // darkened ground instead of bright grass (Peter: "the buildings look like they float")
            if (contactMat == null)
            {
                contactMat = DamageEffects.ParticleMaterial(false, DamageEffects.SoftDot);
                var dark = new Color(0.02f, 0.05f, 0.03f, 0.55f);
                if (contactMat.HasProperty("_BaseColor")) contactMat.SetColor("_BaseColor", dark);
                if (contactMat.HasProperty("_Color")) contactMat.SetColor("_Color", dark);
                contactMat.renderQueue = 2401;
            }
            var c = GameObject.CreatePrimitive(PrimitiveType.Quad);
            c.name = ShadowName;
            Object.Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false);
            var cr = c.GetComponent<MeshRenderer>();
            cr.sharedMaterial = contactMat;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;
            c.transform.localPosition = (right * 0.06f + away * 0.04f) * size + Vector3.up * 0.035f;
            c.transform.rotation = Quaternion.Euler(90f, CameraRig.Yaw, 0f);
            c.transform.localScale = new Vector3(size * 1.65f, size * 1.65f, 1f);
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

        // ---------- aiming (turrets and wall guns) ----------
        // Head pictures: {id}_head_t{tier}_d0..d8 face 6:00, 5:15, 4:30 ... 12:00 on screen (16 directions with the
        // mirrors), or d0..d4 face S, SE, E, NE, N (8 directions with the mirrors). The left half is always mirrored.
        // Each head's sidecar is "pivot x, pivot y, width": the pivot (mount ring centre) lands on the base's
        // mount point (the base sidecar's 4th and 5th numbers), and width is its size against the base picture.
        bool aimRight;          // the target is right of the turret on screen (fallback: mirror the picture)
        float aimAngle = 200f;  // screen angle to the target: 0 = right, 90 = up (the pictures aim a bit left of down-left)
        Transform head, flash;
        Material headMat, flashMat;
        string headKey;
        /// <summary>World point where shots leave the barrel (null until the turret is drawn).</summary>
        public Vector3? Muzzle { get; private set; }

        void AimScreen(Vector3 target, Vector3 ground, Camera cam)
        {
            var a = cam.WorldToScreenPoint(ground);
            var b = cam.WorldToScreenPoint(target);
            var d = new Vector2(b.x - a.x, b.y - a.y);
            if (d.sqrMagnitude < 4f) return;
            if (Mathf.Abs(d.x) > 6f) aimRight = d.x > 0f;
            aimAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        /// <summary>Which head frame shows the aim angle, and whether it is mirrored (targets on the left).</summary>
        void HeadFrame(int frames, out int frame, out bool mirror)
        {
            float a = Mathf.Repeat(aimAngle + 180f, 360f) - 180f;   // -180..180
            mirror = Mathf.Abs(a) > 90f;
            if (mirror) a = Mathf.Sign(a) * 180f - a;                // fold the left half onto the right
            float step = 180f / (frames - 1);
            frame = Mathf.Clamp(Mathf.RoundToInt((a + 90f) / step), 0, frames - 1);
        }

        void Heads(int tier, Vector3 ground, float diamond, float baseW, Quaternion rot, Vector3 recoil)
        {
            if (head == null) head = NewQuad(transform.parent, out headMat);
            int frames = Load(def.id + "_head_t" + tier + "_d8") != null ? 9 : 5;
            int frame; bool mirror;
            HeadFrame(frames, out frame, out mirror);
            string key = def.id + "_head_t" + tier + "_d" + frame;
            var h = Load(key);
            head.gameObject.SetActive(h != null);
            if (h == null) return;
            if (key != headKey) { headKey = key; Show(headMat, h); }
            // mount point on the base picture (sidecar numbers 4 and 5), as a world point on the base card
            float mx = art.mountX > 0f ? art.mountX : art.cx, my = art.mountY > 0f ? art.mountY : art.cy - art.plate * 0.35f;
            var mount = quad.TransformPoint(new Vector3(mx / art.tex.width - 0.5f, 0.5f - my / art.tex.height, 0f));
            float hw = baseW * h.plate / art.tex.width;              // head width in world units
            float hx = (h.cx / h.tex.width - 0.5f) * hw * (mirror ? -1f : 1f), hy = (0.5f - h.cy / h.tex.height) * hw;
            head.rotation = rot;
            head.localScale = new Vector3(mirror ? -hw : hw, hw, 1f);
            // a hair towards the camera so the head always draws over its own base
            head.position = mount - (rot * Vector3.right) * hx - (rot * Vector3.up) * hy + recoil - (rot * Vector3.forward) * 0.05f;
            headMat.SetVector("_Center", ground);
            headMat.SetVector("_Normal", mat.GetVector("_Normal"));
            Finish(headMat);
        }

        void MuzzleFlash(bool split, float w, Quaternion rot, float kick)
        {
            // barrel tip on the picture (fraction of the image, picture aiming left) for the single-picture turrets
            Vector2 tip = def.id == "cannon" ? new Vector2(0.17f, 0.33f) : def.id == "ballista" ? new Vector2(0.10f, 0.45f) : new Vector2(0.11f, 0.40f);
            Vector3 at;
            if (split && head != null && head.gameObject.activeSelf)
            {
                float ang = aimAngle * Mathf.Deg2Rad;
                at = head.position + (rot * new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f, 0f)) * Mathf.Abs(head.localScale.x) * 0.42f;
            }
            else at = quad.TransformPoint(new Vector3(tip.x - 0.5f, 0.5f - tip.y, 0f));
            Muzzle = at;
            bool on = kick > 0.5f;
            if (flash == null && !on) return;
            if (flash == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "MuzzleFlash";
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
                go.transform.SetParent(transform.parent, false);
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                flashMat = DamageEffects.ParticleMaterial(true);
                var c = def.id == "ballista" ? new Color(0.45f, 0.75f, 1f) : def.id == "cannon" ? new Color(1f, 0.85f, 0.6f) : new Color(1f, 0.95f, 0.7f);
                if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", c);
                if (flashMat.HasProperty("_Color")) flashMat.SetColor("_Color", c);
                r.sharedMaterial = flashMat;
                flash = go.transform;
            }
            flash.gameObject.SetActive(on);
            if (!on) return;
            float size = (def.id == "wallgun" ? 0.45f : 0.8f) * (0.7f + 0.6f * kick);
            flash.rotation = rot;
            flash.localScale = new Vector3(size, size, 1f);
            flash.position = at - (rot * Vector3.forward) * 0.3f;
        }

        void OnDestroy()
        {
            if (quad != null) Destroy(quad.gameObject);
            if (head != null) Destroy(head.gameObject);
            if (headMat != null) Destroy(headMat);
            if (flash != null) Destroy(flash.gameObject);
            if (flashMat != null) Destroy(flashMat);
            if (mat != null) Destroy(mat);
            if (segX != null) Destroy(segX.gameObject);
            if (segZ != null) Destroy(segZ.gameObject);
            if (matX != null) Destroy(matX);
            if (matZ != null) Destroy(matZ);
        }

        // ---------- walls ----------
        // A wall cell shows a seamless straight segment (one cell long, no end caps: Tools/wall_strip.py) to each neighbour wall or gate on +x / +z,
        // and towards a gate on -x / -z (gates don't draw segments). Corners, ends and junctions also get a post.
        // The segment image runs along +x (lower-left to upper-right on screen); +z segments are its mirror.
        // A wall's look follows its side's level (1-2, 3-4, 5).
        const float PostDiamond = 0.36f;           // post footprint width across the screen, world units
        const float SegDiamond = 0.70710678f;      // one cell along an axis, seen from 45 degrees
        bool wall, gun;
        Transform segX, segZ;
        const float GunDiamond = 1.35f;   // a wall gun: the gun turret image shrunk onto one wall cell
        Material matX, matZ;

        static int WallTier(int level)
        {
            int t = Tiers[Mathf.Clamp((level - 1) / 2, 0, 2)];
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
            int t = WallTier(Mathf.Max(1, building.Data.level));   // each wall side has its own level
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
                // a wall gun turns to its target too (mirrors the left-aiming picture) and flashes when it fires
                var wcam = Camera.main;
                if (building.AimAt.HasValue && !building.Ruined) AimScreen(building.AimAt.Value, ground, wcam);
                float wkick = Mathf.Clamp01(1f - (Time.time - building.LastShotTime) / 0.14f);
                var wrot = wcam.transform.rotation;
                // the gun stands ON the wall, not inside it (QC run 26: the wall ran through the wall gun)
                Place(quad, mat, g, ground + Vector3.up * 0.5f, GunDiamond, aimRight, shake + (wrot * Vector3.right) * ((aimRight ? -1f : 1f) * 0.04f * wkick));
                MuzzleFlash(false, 0f, wrot, building.Ruined ? 0f : wkick);
                Finish(mat);
                if (building.Ruined) { mat.SetFloat("_Shade", 0.42f); mat.SetColor("_Overlay", new Color(0.2f, 0.17f, 0.15f, 0.35f)); }
                return;
            }
            // posts only at ends, corners and junctions, plus either side of a gate (a clean frame for the gate picture)
            bool nextToGate = l == 2 || r == 2 || u == 2 || d == 2;
            // the gate picture has its own posts: a wall post beside it read as a seam (QC run 26)
            bool showPost = post != null && (nextToGate ? !gateArt : !straight);
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
            Place(q, m, a, centre.Value, SegDiamond * 1.04f, flip, shake);   // QC: a hair of overlap so neighbouring segments never show a seam
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
            int tier = TierFor(def, level);
            string key = def.id + "_t" + tier;
            // rotating turrets: a fixed base picture plus a head picture per direction (see Heads)
            bool split = Load(def.id + "_base_t" + tier) != null && Load(def.id + "_head_t" + tier + "_d0") != null;
            if (split) key = def.id + "_base_t" + tier;
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
            // a building's painted plate (rim, lamps, rocks) is a bit wider than its cells: shrink it so neighbours
            // never overlap (QC run 26: houses and the turret ran into each other). The gate must fill its wall gap.
            if (def.id != "gate") diamond *= 0.9f;
            var ground = transform.parent.position;

            // weapons turn to their target. Until a turret has head pictures, the whole picture (drawn aiming left)
            // mirrors to face the side its target is on; it keeps the last side when nothing is in range.
            bool weapon = def.attackRange > 0f && building != null && !building.IsGhost && !building.Ruined;
            if (weapon && building.AimAt.HasValue) AimScreen(building.AimAt.Value, ground, cam);
            if (weapon && !split) flip ^= aimRight;
            float kick = weapon ? Mathf.Clamp01(1f - (Time.time - building.LastShotTime) / 0.14f) : 0f;
            var recoil = (rot * Vector3.right) * ((aimRight ? -1f : 1f) * 0.07f * kick);

            float w = diamond * art.tex.width / art.plate;
            float ox = (art.cx / art.tex.width - 0.5f) * w * (flip ? -1f : 1f);
            float oy = (0.5f - art.cy / art.tex.height) * w;

            quad.rotation = rot;
            quad.localScale = new Vector3(flip ? -w : w, w, 1f);
            quad.position = ground - (rot * Vector3.right) * ox - (rot * Vector3.up) * oy + transform.localPosition + (split ? Vector3.zero : recoil);
            if (split) Heads(tier, ground, diamond, w, rot, recoil);
            else if (head != null) head.gameObject.SetActive(false);
            if (weapon) MuzzleFlash(split, w, rot, kick);

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
