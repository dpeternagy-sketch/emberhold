using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    public enum ResourceType { None, Food, Wood, Stone, Gold, Weapons }

    public enum BuildingCategory { Core, Economy, Defense }

    [System.Serializable]
    public struct Cost
    {
        public int wood, stone, gold, weapons;

        public Cost(int wood = 0, int stone = 0, int gold = 0, int weapons = 0)
        {
            this.wood = wood; this.stone = stone; this.gold = gold; this.weapons = weapons;
        }

        public Cost Scaled(float factor)
        {
            return new Cost(Mathf.CeilToInt(wood * factor), Mathf.CeilToInt(stone * factor),
                            Mathf.CeilToInt(gold * factor), Mathf.CeilToInt(weapons * factor));
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (wood > 0) parts.Add(wood + " Wood");
            if (stone > 0) parts.Add(stone + " Stone");
            if (gold > 0) parts.Add(gold + " Gold");
            if (weapons > 0) parts.Add(weapons + " Weapons");
            return parts.Count == 0 ? "Free" : string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Static description of a building type. Pure data, no scene references,
    /// so it can later be moved to ScriptableObjects or Remote Config for live balancing.
    /// </summary>
    public class BuildingDef
    {
        public string id;
        public string displayName;
        public BuildingCategory category;
        public int width = 1, depth = 1;
        public float height = 1f;
        public Color color = Color.gray;
        public Cost cost;
        public int maxHp = 100;
        public int maxLevel = 3;
        public float upgradeSeconds = 30f;     // real-time seconds for level 1 -> 2 (scales per level)

        // Economy
        public int housing;                    // villagers this building can house
        public int workers;                    // villagers needed for full output
        public ResourceType produces = ResourceType.None;
        public int produceAmount;
        public float produceInterval = 10f;
        public int consumeWood, consumeStone;  // per production cycle (workshop)
        public float harvestRadius;            // lumberjack: how far it looks for trees

        // Night
        public float lightRadius;              // reveals enemies in fog
        public float attackRange;
        public int attackDamage;
        public float attackInterval = 1f;

        /// <summary>Optional prefab at Resources/Models/{id}. If missing, a placeholder is generated.</summary>
        public string PrefabPath { get { return "Models/" + id; } }

        public bool Movable { get { return category != BuildingCategory.Core; } }
    }

    public static class BuildingCatalog
    {
        static readonly Dictionary<string, BuildingDef> defs = new Dictionary<string, BuildingDef>();
        static readonly List<BuildingDef> ordered = new List<BuildingDef>();

        public const string Castle = "castle";

        static BuildingCatalog()
        {
            Add(new BuildingDef {
                id = Castle, displayName = "Castle", category = BuildingCategory.Core,
                width = 4, depth = 4, height = 4f, color = new Color(0.62f, 0.6f, 0.56f),
                maxHp = 1500, maxLevel = 5, upgradeSeconds = 60f, housing = 4, lightRadius = 7f,
                attackRange = 7f, attackDamage = 6, attackInterval = 1.4f
            });
            Add(new BuildingDef {
                id = "house", displayName = "House", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 1.6f, color = new Color(0.78f, 0.62f, 0.45f),
                cost = new Cost(wood: 20), maxHp = 150, housing = 4,
                produces = ResourceType.Gold, produceAmount = 2, produceInterval = 15f
            });
            Add(new BuildingDef {
                id = "farm", displayName = "Farm", category = BuildingCategory.Economy,
                width = 3, depth = 3, height = 0.3f, color = new Color(0.82f, 0.72f, 0.3f),
                cost = new Cost(wood: 15), maxHp = 80, workers = 2,
                produces = ResourceType.Food, produceAmount = 4, produceInterval = 10f
            });
            Add(new BuildingDef {
                id = "lumberjack", displayName = "Lumberjack", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 1.4f, color = new Color(0.45f, 0.32f, 0.2f),
                cost = new Cost(wood: 10, stone: 5), maxHp = 120, workers = 2,
                produces = ResourceType.Wood, produceAmount = 3, produceInterval = 7f, harvestRadius = 10f
            });
            Add(new BuildingDef {
                id = "quarry", displayName = "Quarry", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 1.0f, color = new Color(0.5f, 0.5f, 0.52f),
                cost = new Cost(wood: 25), maxHp = 160, workers = 2,
                produces = ResourceType.Stone, produceAmount = 2, produceInterval = 9f
            });
            Add(new BuildingDef {
                id = "workshop", displayName = "Workshop", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 1.8f, color = new Color(0.55f, 0.27f, 0.2f),
                cost = new Cost(wood: 30, stone: 20), maxHp = 180, workers = 2,
                produces = ResourceType.Weapons, produceAmount = 1, produceInterval = 20f,
                consumeWood = 4, consumeStone = 2
            });
            Add(new BuildingDef {
                id = "wall", displayName = "Wall", category = BuildingCategory.Defense,
                width = 1, depth = 1, height = 1.5f, color = new Color(0.6f, 0.58f, 0.55f),
                cost = new Cost(stone: 3), maxHp = 300, upgradeSeconds = 15f
            });
            Add(new BuildingDef {
                id = "torch", displayName = "Torch", category = BuildingCategory.Defense,
                width = 1, depth = 1, height = 1.2f, color = new Color(0.35f, 0.25f, 0.15f),
                cost = new Cost(wood: 3), maxHp = 40, lightRadius = 5f, maxLevel = 2, upgradeSeconds = 10f
            });
            Add(new BuildingDef {
                id = "watchtower", displayName = "Watchtower", category = BuildingCategory.Defense,
                width = 2, depth = 2, height = 3.5f, color = new Color(0.5f, 0.38f, 0.25f),
                cost = new Cost(wood: 20, stone: 10, weapons: 2), maxHp = 250, workers = 1,
                lightRadius = 8f, attackRange = 9f, attackDamage = 12, attackInterval = 1f
            });
        }

        static void Add(BuildingDef d) { defs[d.id] = d; ordered.Add(d); }

        public static BuildingDef Get(string id)
        {
            BuildingDef d;
            return defs.TryGetValue(id, out d) ? d : null;
        }

        public static IEnumerable<BuildingDef> InCategory(BuildingCategory c)
        {
            foreach (var d in ordered) if (d.category == c) yield return d;
        }
    }
}
