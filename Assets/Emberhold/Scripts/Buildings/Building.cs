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

        public int MaxHp { get { return Mathf.RoundToInt(Def.maxHp * (1f + 0.5f * (Data.level - 1))); } }
        public float LightRadius { get { return Def.lightRadius * (1f + 0.2f * (Data.level - 1)); } }
        public float AttackRange { get { return Def.attackRange * (1f + 0.1f * (Data.level - 1)); } }
        public int AttackDamage { get { return Mathf.RoundToInt(Def.attackDamage * (1f + 0.4f * (Data.level - 1))); } }
        public int Housing { get { return Def.housing * Data.level; } }
        public Vector3 Center { get { return GridMap.FootprintCenter(Data.x, Data.z, Def, Data.rotation); } }

        public float WorkerEfficiency
        {
            get { return Def.workers <= 0 ? 1f : Mathf.Clamp01((float)AssignedWorkers / Def.workers); }
        }

        public IEnumerable<Vector2Int> Cells
        {
            get { return GridMap.Footprint(Data.x, Data.z, Def.width, Def.depth, Data.rotation); }
        }

        public Cost UpgradeCost
        {
            get
            {
                var baseCost = Def.cost;
                if (Def.id == BuildingCatalog.Castle) baseCost = new Cost(wood: 60, stone: 40, gold: 40);
                var c = baseCost.Scaled(1.2f * Data.level);
                c.gold += 10 * Data.level;
                return c;
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
        Transform upgradeScaffold;
        LightSource lightSource;
        float produceTimer;
        float attackTimer;
        Renderer[] renderers;
        Material[][] originalMaterials;
        Material tintMaterial;
        float hitFlash;

        public static Building Spawn(BuildingDef def, BuildingData data, Transform parent, bool ghost)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Building>();
            b.Def = def;
            b.Data = data;
            b.IsGhost = ghost;
            if (data.hp <= 0) data.hp = b.MaxHp;
            b.model = BuildingVisuals.Create(def, go.transform).transform;
            b.renderers = go.GetComponentsInChildren<Renderer>();
            b.ApplyTransform();
            if (!ghost)
            {
                All.Add(b);
                if (def.lightRadius > 0f) b.lightSource = LightSource.Attach(go, b.LightRadius, def.height + 0.3f);
            }
            b.produceTimer = UnityEngine.Random.Range(0f, def.produceInterval);
            b.RefreshUpgradeVisual();
            return b;
        }

        void OnDestroy() { All.Remove(this); }

        public void ApplyTransform()
        {
            transform.position = Center;
            model.localRotation = Quaternion.Euler(0, Data.rotation * 90f, 0);
        }

        public void SetTint(Color? tint)
        {
            if (originalMaterials == null)
            {
                originalMaterials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++) originalMaterials[i] = renderers[i].sharedMaterials;
            }
            if (tint.HasValue)
            {
                if (tintMaterial == null) tintMaterial = Materials.Unique(tint.Value);
                Materials.SetColor(tintMaterial, tint.Value);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var mats = new Material[originalMaterials[i].Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = tintMaterial;
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
            model.localPosition = raised ? new Vector3(0, 0.3f, 0) : Vector3.zero;
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

        // ---------- Combat ----------

        public void TakeDamage(int amount)
        {
            if (IsGhost) return;
            Data.hp -= amount;
            hitFlash = 0.15f;
            if (Data.hp <= 0) GameManager.Instance.DestroyBuilding(this);
        }

        void Update()
        {
            if (IsGhost) return;
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
                if (Def.produces == ResourceType.Gold) eff = Mathf.Clamp01(gm.PopulationIn(this) / (float)Mathf.Max(1, Housing));
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

            if (Def.attackRange > 0f && gm.Cycle.IsNight && (Def.workers == 0 || AssignedWorkers > 0))
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    var target = Enemy.NearestVisible(Center, AttackRange);
                    if (target != null)
                    {
                        attackTimer = Def.attackInterval;
                        Projectile.Fire(Center + Vector3.up * Def.height, target, AttackDamage);
                        AudioManager.Play("shoot", 0.5f);
                    }
                }
            }
        }
    }
}
