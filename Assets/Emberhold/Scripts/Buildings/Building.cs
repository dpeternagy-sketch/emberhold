using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>A placed building in the world. Its persistent data lives in <see cref="BuildingData"/>.</summary>
    public class Building : MonoBehaviour
    {
        public static readonly List<Building> All = new List<Building>();

        public BuildingDef Def { get; private set; }
        public BuildingData Data { get; private set; }
        public bool IsGhost { get; private set; }
        public int AssignedWorkers { get; set; }
        /// <summary>A workshop that is waiting for its input (e.g. a smelter with no ore).</summary>
        public bool Starved { get; set; }
        /// <summary>Canals: connected to the river or a well. Farms and wells: next to water.</summary>
        public bool HasWater { get; private set; }

        /// <summary>Corner / T-junction post for walls (joins the two directions).</summary>
        public void SetWallJoint(bool on, bool unused)
        {
            if (sprite != null) return;   // image walls draw their own posts
            var j = transform.Find("Joint");
            if (on && j == null)
            {
                var root = new GameObject("Joint").transform;
                root.SetParent(transform, false);
                var stone = new Color(0.93f, 0.94f, 0.95f);
                Util.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.7f, 0), new Vector3(0.7f, 1.4f, 0.7f), stone);
                Util.Prim(PrimitiveType.Cube, root, new Vector3(0, 1.44f, 0), new Vector3(0.78f, 0.1f, 0.78f), new Color(0.18f, 0.49f, 0.96f));
                // also fill the other direction so both arms meet the post
                Util.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.62f, 0), new Vector3(0.52f, 0.9f, 1.0f), stone);
                RefreshRenderers();
            }
            else if (!on && j != null) { DestroyImmediate(j.gameObject); RefreshRenderers(); }
        }

        /// <summary>Call after adding/removing child meshes so tinting covers the new parts.</summary>
        void RefreshRenderers()
        {
            if (originalMaterials != null)
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sharedMaterials = originalMaterials[i];
            originalMaterials = null;
            renderers = GetComponentsInChildren<Renderer>();
        }

        int canalBankMask = -1;

        public void SetCanalBanks(bool n, bool s, bool e, bool w)
        {
            int mask = (n ? 1 : 0) | (s ? 2 : 0) | (e ? 4 : 0) | (w ? 8 : 0);
            if (mask == canalBankMask || model == null) return;
            canalBankMask = mask;
            BuildingVisuals.CanalBanks(model, n, s, e, w);
            RefreshRenderers();
        }

        public void SetWater(bool on)
        {
            HasWater = on;
            var w = model != null ? model.Find("Water") : null;
            if (w != null) w.gameObject.SetActive(on);
        }

        public int MaxHp { get { return Mathf.RoundToInt(Def.maxHp * (1f + 0.5f * (Data.level - 1))); } }
        public float LightRadius { get { return Def.lightRadius * (1f + 0.2f * (Data.level - 1)); } }
        public float AttackRange { get { return Def.attackRange * (1f + 0.1f * (Data.level - 1)); } }
        /// <summary>Counter rule (x2): Gun beats raptors, Laser beats triceratops and T-rex, Missile beats pterosaurs.</summary>
        public static int Counter(int projectile, EnemyKind kind)
        {
            if (projectile == 0 && kind == EnemyKind.Raider) return 2;
            if (projectile == 1 && (kind == EnemyKind.Brute || kind == EnemyKind.Boss)) return 2;
            if (projectile == 2 && kind == EnemyKind.Flyer) return 2;
            return 1;
        }

        public int AttackDamage { get { return Mathf.RoundToInt(Def.attackDamage * (1f + 0.4f * (Data.level - 1))); } }
        public int Housing { get { return Ruined ? 0 : Def.housing * Data.level; } }
        /// <summary>Destroyed in a raid. A ruin houses nobody, makes nothing and does not shoot until rebuilt.</summary>
        public bool Ruined { get { return Data != null && Data.ruined; } }
        public bool Damaged { get { return !Ruined && Data.hp < MaxHp; } }
        public Vector3 Center { get { return GridMap.FootprintCenter(Data.x, Data.z, Def, Data.rotation); } }

        public float WorkerEfficiency
        {
            get { if (Ruined) return 0f; return Def.workers <= 0 ? 1f : Mathf.Clamp01((float)AssignedWorkers / Def.workers); }
        }

        public IEnumerable<Vector2Int> Cells
        {
            get { return GridMap.Footprint(Data.x, Data.z, Def.width, Def.depth, Data.rotation); }
        }

        public Cost UpgradeCost { get { return UpgradeCostAt(Data.level); } }

        /// <summary>Price to go from this level to the next one.</summary>
        public Cost UpgradeCostAt(int level)
        {
            var baseCost = Def.cost;
            if (Def.id == BuildingCatalog.Castle) baseCost = new Cost(steel: 80, energy: 20, coins: 40);
            var c = baseCost.Scaled(1.2f * level);
            c.coins += 10 * level;
            return c;
        }

        /// <summary>Everything ever paid for this building: the build price, every finished upgrade, and one in progress.</summary>
        public Cost TotalPaid
        {
            get
            {
                var t = Def.cost;
                int last = Data.IsUpgrading ? Data.level : Data.level - 1;
                for (int l = 1; l <= last; l++)
                {
                    var u = UpgradeCostAt(l);
                    t.steel += u.steel; t.energy += u.energy; t.coins += u.coins; t.food += u.food;
                }
                return t;
            }
        }

        public float UpgradeDuration { get { return Def.upgradeSeconds * Data.level * Data.level; } }

        public float UpgradeSecondsLeft
        {
            get
            {
                if (!Data.IsUpgrading) return 0f;
                return Mathf.Max(0f, (float)(new DateTime(Data.upgradeEndUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds);
            }
        }

        Transform model;
        Transform turret;
        Transform damageFx;
        int damageFxLevel = -1;
        Transform upgradeScaffold;
        LightSource lightSource;
        float produceTimer;
        float attackTimer;
        Renderer[] renderers;
        Material[][] originalMaterials;
        Material tintMaterial;
        Material xrayMaterial;
        /// <summary>Traps reset for a while after catching something.</summary>
        [NonSerialized] public float trapReadyAt;
        float hitFlash;
        float buildTime, buildDuration;
        Transform dust;

        public bool UnderConstruction { get { return buildTime < buildDuration; } }

        /// <summary>The building rises out of the ground behind scaffolding.</summary>
        public void StartConstruction(float seconds)
        {
            buildDuration = seconds;
            buildTime = 0f;
            if (sprite != null)
            {
                // painted buildings: no wooden poles, just a glowing build-zone outline while it rises
                dust = BuildFrame("Construction");
                model.localScale = new Vector3(1f, 0.02f, 1f);
                return;
            }
            dust = new GameObject("Construction").transform;
            dust.SetParent(transform, false);
            var s = GridMap.Size(Def, Data.rotation);
            var wood = new Color(0.72f, 0.56f, 0.32f);
            float h = Mathf.Max(1f, Def.height);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -0.5f : 0.5f) * s.x * 0.95f, sz = (i < 2 ? -0.5f : 0.5f) * s.y * 0.95f;
                Util.Prim(PrimitiveType.Cube, dust, new Vector3(sx, h * 0.5f, sz), new Vector3(0.07f, h, 0.07f), wood);
            }
            Util.Prim(PrimitiveType.Cube, dust, new Vector3(0, h * 0.6f, -0.5f * s.y * 0.95f), new Vector3(s.x * 0.95f, 0.05f, 0.05f), wood);
            Util.Prim(PrimitiveType.Cube, dust, new Vector3(0, h * 0.6f, 0.5f * s.y * 0.95f), new Vector3(s.x * 0.95f, 0.05f, 0.05f), wood);
            Util.Prim(PrimitiveType.Cube, dust, new Vector3(0.3f * s.x, 0.12f, -0.6f * s.y), new Vector3(0.35f, 0.24f, 0.25f), new Color(0.62f, 0.6f, 0.56f));
            model.localScale = new Vector3(1f, 0.02f, 1f);
        }

        /// <summary>A thin cyan outline round the footprint, lying on the ground (construction and upgrades).</summary>
        Transform BuildFrame(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var s = GridMap.Size(Def, Data.rotation);
            var glow = Materials.Emissive(new Color(0.35f, 0.85f, 1f), 2.2f);
            float hx = s.x * 0.5f - 0.05f, hz = s.y * 0.5f - 0.05f, t = 0.07f;
            Vector3[] pos = { new Vector3(0, 0.04f, -hz), new Vector3(0, 0.04f, hz), new Vector3(-hx, 0.04f, 0), new Vector3(hx, 0.04f, 0) };
            Vector3[] size = { new Vector3(hx * 2f, 0.03f, t), new Vector3(hx * 2f, 0.03f, t), new Vector3(t, 0.03f, hz * 2f), new Vector3(t, 0.03f, hz * 2f) };
            for (int i = 0; i < 4; i++)
            {
                var bar = Util.Prim(PrimitiveType.Cube, root, pos[i], size[i], new Color(0.35f, 0.85f, 1f));
                var r = bar.GetComponent<Renderer>();
                r.sharedMaterial = glow;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        public static Building Spawn(BuildingDef def, BuildingData data, Transform parent, bool ghost)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Building>();
            b.Def = def;
            b.Data = data;
            b.IsGhost = ghost;
            if (data.hp <= 0 && !data.ruined) data.hp = b.MaxHp;
            b.model = BuildingVisuals.Create(def, go.transform).transform;
            b.sprite = b.model.GetComponent<SpriteBuilding>();
            b.renderers = System.Array.FindAll(go.GetComponentsInChildren<Renderer>(), r => r.name != SpriteBuilding.QuadName);
            b.turret = b.model.Find("Turret");
            b.ApplyTransform();
            if (!ghost)
            {
                All.Add(b);
                if (def.lightRadius > 0f) b.lightSource = LightSource.Attach(go, b.LightRadius, def.height + 0.3f);
            }
            b.produceTimer = UnityEngine.Random.Range(0f, def.produceInterval);
            b.RefreshUpgradeVisual();
            if (!ghost) b.RefreshDamageVisual();
            return b;
        }

        void OnDestroy() { All.Remove(this); }

        public void ApplyTransform()
        {
            transform.position = Center;
            model.localRotation = Quaternion.Euler(0, Data.rotation * 90f, 0);
        }

        SpriteBuilding sprite;

        public void SetTint(Color? tint)
        {
            if (sprite != null) sprite.SetTint(tint);
            if (originalMaterials == null)
            {
                originalMaterials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++) originalMaterials[i] = renderers[i].sharedMaterials;
            }
            if (tint.HasValue)
            {
                if (tintMaterial == null) tintMaterial = Materials.Unique(tint.Value);
                Materials.SetColor(tintMaterial, tint.Value);
                // plus a see-through outline wherever other buildings hide it
                if (xrayMaterial == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/XRay");
                    if (sh != null) xrayMaterial = new Material(sh);
                }
                if (xrayMaterial != null) xrayMaterial.SetColor("_Color", new Color(tint.Value.r, tint.Value.g, tint.Value.b, 0.6f));
                for (int i = 0; i < renderers.Length; i++)
                {
                    int n = originalMaterials[i].Length;
                    var mats = new Material[n + (xrayMaterial != null ? 1 : 0)];
                    for (int m = 0; m < n; m++) mats[m] = tintMaterial;
                    if (xrayMaterial != null) mats[n] = xrayMaterial;
                    renderers[i].sharedMaterials = mats;
                }
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = originalMaterials[i];
            }
        }

        public void SetRaised(bool raised)
        {
            model.localPosition = raised ? new Vector3(0, 0.45f, 0) : Vector3.zero;
        }

        // ---------- Upgrades (real-time, keep running while the app is closed) ----------

        public void StartUpgrade()
        {
            Data.upgradeEndUtcTicks = DateTime.UtcNow.AddSeconds(UpgradeDuration).Ticks;
            RefreshUpgradeVisual();
        }

        public void FinishUpgradeNow()
        {
            if (!Data.IsUpgrading) return;
            Data.upgradeEndUtcTicks = 0;
            Data.level++;
            Data.hp = MaxHp;
            if (lightSource != null) lightSource.radius = LightRadius;
            RefreshUpgradeVisual();
            model.localScale = Vector3.one * (1f + 0.06f * (Data.level - 1));
            GameManager.Instance.OnBuildingUpgraded(this);
        }

        void RefreshUpgradeVisual()
        {
            bool show = Data.IsUpgrading && !IsGhost;
            if (show && upgradeScaffold == null && sprite != null) upgradeScaffold = BuildFrame("Upgrading");
            if (show && upgradeScaffold == null)
            {
                upgradeScaffold = new GameObject("Scaffold").transform;
                upgradeScaffold.SetParent(transform, false);
                var s = GridMap.Size(Def, Data.rotation);
                var wood = new Color(0.7f, 0.55f, 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0 ? -0.5f : 0.5f) * s.x, sz = (i < 2 ? -0.5f : 0.5f) * s.y;
                    Util.Prim(PrimitiveType.Cube, upgradeScaffold, new Vector3(sx, Def.height * 0.6f, sz), new Vector3(0.08f, Def.height * 1.2f, 0.08f), wood);
                }
            }
            if (upgradeScaffold != null) upgradeScaffold.gameObject.SetActive(show);
            if (model != null) model.localScale = Vector3.one * (1f + 0.06f * (Data.level - 1));
        }

        // ---------- Ruins & damage ----------

        static readonly Color Charred = new Color(0.2f, 0.17f, 0.15f);

        /// <summary>0 = fine, 1 = damaged (light smoke), 2 = ruined (collapsed, heavy smoke and embers).</summary>
        public void RefreshDamageVisual()
        {
            if (IsGhost || model == null) return;
            int level = Ruined ? 2 : (Data.hp < MaxHp * 0.6f ? 1 : 0);
            if (level == damageFxLevel) return;
            damageFxLevel = level;
            if (damageFx != null) Destroy(damageFx.gameObject);
            damageFx = null;
            var s = GridMap.Size(Def, Data.rotation);
            if (level == 2)
            {
                // collapsed: sunk, tilted, burnt
                model.localScale = new Vector3(1f, 0.38f, 1f);
                model.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-7f, 7f), Data.rotation * 90f, UnityEngine.Random.Range(-7f, 7f));
                SetTint(Charred);
                damageFx = new GameObject("Ruins").transform;
                damageFx.SetParent(transform, false);
                if (Def.id != "wall" && Def.id != "canal")
                {
                    DamageEffects.Rubble(damageFx, s);
                    DamageEffects.Smoke(damageFx, new Vector3(0, 0.3f, 0), Mathf.Clamp(s.x * s.y / 4f, 0.6f, 2f));
                    DamageEffects.Embers(damageFx, new Vector3(0, 0.2f, 0));
                }
                else DamageEffects.Smoke(damageFx, new Vector3(0, 0.2f, 0), 0.4f);
            }
            else
            {
                model.localScale = Vector3.one * (1f + 0.06f * (Data.level - 1));
                model.localRotation = Quaternion.Euler(0, Data.rotation * 90f, 0);
                SetTint(null);
                if (level == 1 && Def.id != "canal")
                {
                    damageFx = new GameObject("Damage").transform;
                    damageFx.SetParent(transform, false);
                    DamageEffects.Smoke(damageFx, new Vector3(s.x * 0.2f, Def.height * 0.8f, 0), 0.45f);
                }
            }
        }

        public void SetRuined(bool ruined)
        {
            Data.ruined = ruined;
            if (lightSource != null) lightSource.enabled = !ruined;
            damageFxLevel = -1;
            RefreshDamageVisual();
        }

        // ---------- Combat ----------

        public void TakeDamage(int amount)
        {
            if (IsGhost || Ruined || Def.indestructible) return;
            Data.hp -= amount;
            hitFlash = 0.15f;
            if (Data.hp <= 0) GameManager.Instance.DestroyBuilding(this);
            else RefreshDamageVisual();
        }

        void Update()
        {
            if (IsGhost) return;
            if (UnderConstruction)
            {
                buildTime += Time.deltaTime;
                float k = Mathf.Clamp01(buildTime / buildDuration);
                model.localScale = new Vector3(1f, Mathf.Max(0.02f, Mathf.SmoothStep(0f, 1f, k)), 1f) * (1f + 0.06f * (Data.level - 1));
                if (Mathf.Repeat(buildTime, 0.6f) < Time.deltaTime) AudioManager.PlayAt("hammer", transform.position, 0.35f);
                if (!UnderConstruction)
                {
                    if (dust != null) Destroy(dust.gameObject);
                    AudioManager.Play("coin", 0.5f);
                    if (Def.id == "well" || Def.id == "canal") GameManager.Instance.RecalculateWater();   // a rebuilt well fills its canals again
                }
                return;
            }
            if (Data.IsUpgrading && UpgradeSecondsLeft <= 0f) FinishUpgradeNow();

            if (hitFlash > 0f)
            {
                hitFlash -= Time.deltaTime;
                model.localPosition = hitFlash > 0f ? UnityEngine.Random.insideUnitSphere * 0.05f : Vector3.zero;
            }

            var gm = GameManager.Instance;
            if (gm == null) return;

            // Production pauses while upgrading and at night.
            if (Def.produces != ResourceType.None && !Data.IsUpgrading && !gm.Cycle.IsNight)
            {
                float eff = WorkerEfficiency * gm.FoodEfficiency;
                if (Def.produces == ResourceType.Coins) eff = Mathf.Clamp01(gm.PopulationIn(this) / (float)Mathf.Max(1, Housing));
                if (eff > 0f)
                {
                    produceTimer += Time.deltaTime * eff;
                    if (produceTimer >= Def.produceInterval)
                    {
                        produceTimer = 0f;
                        gm.Produce(this);
                    }
                }
            }

            // Weapons defend day and night (raids can come any time). A guard is not required to fire.
            if (Def.attackRange > 0f && !Ruined)
            {
                attackTimer -= Time.deltaTime;
                var aim = Enemy.NearestVisible(Center, AttackRange, Def.hitsAir);
                if (aim != null && turret != null)
                {
                    var d = aim.transform.position - turret.position; d.y = 0;
                    if (d.sqrMagnitude > 0.01f)
                        turret.rotation = Quaternion.Slerp(turret.rotation, Quaternion.LookRotation(-d), Time.deltaTime * 6f);
                }
                if (attackTimer <= 0f && aim != null)
                {
                    attackTimer = Def.attackInterval / (AssignedWorkers > 0 ? 1.25f : 1f);
                    var from = turret != null ? turret.position + Vector3.up * 0.3f : Center + Vector3.up * Def.height;
                    Projectile.Fire(from, aim, AttackDamage * Counter(Def.projectile, aim.kind), Def.projectile, Def.splashRadius);
                    if (Def.projectile == 2) AudioManager.PlayAt("cannon", from, 0.9f);
                    else if (Def.projectile == 1) AudioManager.PlayAt("ballista", from, 0.8f);
                    else AudioManager.PlayAt(UnityEngine.Random.value < 0.5f ? "bow" : "bow2", from, 0.7f);
                }
            }
        }
    }
}
