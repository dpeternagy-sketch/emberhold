using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>Five resources only: mobile players like short chains.</summary>
    public enum ResourceType { None, Food, Water, Energy, Steel, Coins }

    public enum BuildingCategory { Core, Economy, Defense, Army }

    [System.Serializable]
    public struct Cost
    {
        public int steel, energy, coins, food;

        public Cost(int steel = 0, int energy = 0, int coins = 0, int food = 0)
        {
            this.steel = steel; this.energy = energy; this.coins = coins; this.food = food;
        }

        public Cost Scaled(float factor)
        {
            return new Cost(Mathf.CeilToInt(steel * factor), Mathf.CeilToInt(energy * factor), Mathf.CeilToInt(coins * factor), Mathf.CeilToInt(food * factor));
        }

        public bool IsFree { get { return steel + energy + coins + food == 0; } }

        public override string ToString()
        {
            var parts = new List<string>();
            if (steel > 0) parts.Add(steel + " Steel");
            if (energy > 0) parts.Add(energy + " Energy");
            if (coins > 0) parts.Add(coins + " Coins");
            if (food > 0) parts.Add(food + " Food");
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
        public float upgradeSeconds = 60f;     // real-time seconds for level 1 -> 2 (x level^2: 1, 4, 9, 16 min)

        // Economy
        public int housing;                    // villagers this building can house
        public int workers;                    // villagers needed for full output
        public ResourceType produces = ResourceType.None;
        public int produceAmount;
        public float produceInterval = 10f;
        public int consumeEnergy;              // per production cycle
        /// <summary>Must touch the foot of the mountain (mines).</summary>
        public bool nearMountain;
        /// <summary>One line for the build card / info panel, e.g. "Wood -> Planks".</summary>
        public string recipe;

        // Night
        public float lightRadius;              // reveals enemies in fog
        public float attackRange;
        public int attackDamage;
        public float attackInterval = 1f;
        public float splashRadius;             // cannon: hits every enemy this close to the target
        public int projectile;                 // 0 arrow, 1 ballista bolt, 2 cannon ball

        /// <summary>Castle level needed before this can be built. Locked cards show grey with the requirement.</summary>
        public int unlockLevel = 1;

        /// <summary>Optional prefab at Resources/Models/{id}. If missing, a placeholder is generated.</summary>
        public string PrefabPath { get { return "Models/" + id; } }

        public bool Movable { get { return category != BuildingCategory.Core; } }

        /// <summary>Enemies walk through it (slowly) instead of attacking it, e.g. canals.</summary>
        public bool passable;
        /// <summary>Catches a dinosaur that walks over it (if a pen has room).</summary>
        public bool trap;
        /// <summary>Can never be damaged (walls). Enemies walk around it, so a closed ring needs a gate.</summary>
        public bool indestructible;
        /// <summary>Only anti-air weapons (the missile launcher) can hit flyers; gun and laser are ground only.</summary>
        public bool hitsAir;
        /// <summary>Army camp: housing space for trained troops at level 1 (+ campPerLevel each level).</summary>
        public int campSpace, campPerLevel;
        /// <summary>Barracks / factory: unit ids it can train.</summary>
        public string[] trains;
        /// <summary>Command Center: home of the Commander hero.</summary>
        public bool commandCenter;
        /// <summary>How many tamed dinosaurs this pen keeps.</summary>
        public int penCapacity;

        /// <summary>Label of the remove button ("Remove", or "Fill in" for canals).</summary>
        public string removeLabel = "Remove";
    }

    public static class BuildingCatalog
    {
        static readonly Dictionary<string, BuildingDef> defs = new Dictionary<string, BuildingDef>();
        static readonly List<BuildingDef> ordered = new List<BuildingDef>();

        public const string Castle = "castle";

        /// <summary>Build menu order: food and water first, then the wood, stone and iron chains, then defenses.</summary>
        static readonly string[] MenuOrder = { "house", "farm", "well", "canal", "solar", "mine",
                                               "powerplant", "factory", "wall", "gate", "watchtower", "ballista", "cannon", "dronepad", "trap", "pen",
                                               "barracks", "camp", "command", "robotfactory" };

        static BuildingCatalog()
        {
            Add(new BuildingDef {
                id = Castle, displayName = "HQ", category = BuildingCategory.Core,
                width = 4, depth = 4, height = 4f, color = new Color(0.62f, 0.6f, 0.56f),
                maxHp = 1500, maxLevel = 5, upgradeSeconds = 180f, housing = 4, lightRadius = 7f,
                attackRange = 7f, attackDamage = 6, attackInterval = 1.4f
            });
            Add(new BuildingDef {
                id = "house", displayName = "House", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 1.6f, color = new Color(0.78f, 0.62f, 0.45f),
                cost = new Cost(steel: 20), maxHp = 150, housing = 4,
                produces = ResourceType.Coins, produceAmount = 2, produceInterval = 15f, recipe = "Homes for 4. They pay coins"
            });
            Add(new BuildingDef {
                id = "farm", displayName = "Greenhouse", category = BuildingCategory.Economy,
                width = 3, depth = 3, height = 0.3f, color = new Color(0.82f, 0.72f, 0.3f),
                cost = new Cost(steel: 15), maxHp = 80, workers = 2,
                produces = ResourceType.Food, produceAmount = 4, produceInterval = 10f, recipe = "Grows food (+75% next to water)"
            });
            Add(new BuildingDef {
                id = "well", displayName = "Water Pump", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 1.2f, color = new Color(0.55f, 0.6f, 0.68f),
                cost = new Cost(steel: 10, coins: 5), maxHp = 120, workers = 1,
                produces = ResourceType.Water, produceAmount = 3, produceInterval = 12f, recipe = "Pumps water, fills canals"
            });
            Add(new BuildingDef {
                id = "canal", displayName = "Canal", category = BuildingCategory.Economy,
                width = 1, depth = 1, height = 0.05f, color = new Color(0.25f, 0.45f, 0.6f),
                cost = new Cost(coins: 2), maxHp = 9999, maxLevel = 1, passable = true, removeLabel = "Fill in",
                recipe = "Carries water to greenhouses"
            });
            Add(new BuildingDef {
                id = "solar", displayName = "Solar Panels", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 0.8f, color = new Color(0.2f, 0.3f, 0.5f),
                cost = new Cost(steel: 15), maxHp = 100,
                produces = ResourceType.Energy, produceAmount = 3, produceInterval = 8f, recipe = "Makes energy by day"
            });
            Add(new BuildingDef {
                id = "mine", displayName = "Steel Works", category = BuildingCategory.Economy,
                width = 3, depth = 3, height = 1.6f, color = new Color(0.4f, 0.37f, 0.35f),
                cost = new Cost(steel: 25, coins: 10), maxHp = 200, workers = 2, nearMountain = true,
                produces = ResourceType.Steel, produceAmount = 4, produceInterval = 9f,
                consumeEnergy = 1, recipe = "Energy -> Steel (build at the mountain)"
            });
            Add(new BuildingDef {
                id = "trap", displayName = "Dino Trap", category = BuildingCategory.Defense,
                width = 1, depth = 1, height = 0.2f, color = new Color(0.4f, 0.4f, 0.42f),
                cost = new Cost(steel: 15, energy: 5), maxHp = 9999, maxLevel = 1, passable = true, trap = true, unlockLevel = 3,
                recipe = "Catches a raptor or triceratops"
            });
            Add(new BuildingDef {
                id = "pen", displayName = "Dino Pen", category = BuildingCategory.Defense,
                width = 3, depth = 3, height = 1.2f, color = new Color(0.4f, 0.5f, 0.35f),
                cost = new Cost(steel: 40, coins: 20), maxHp = 300, maxLevel = 3, penCapacity = 2, unlockLevel = 3,
                recipe = "Keeps 2 tamed dinos. They guard you"
            });
            Add(new BuildingDef {
                id = "wall", displayName = "Barrier", category = BuildingCategory.Defense,
                width = 1, depth = 1, height = 1.5f, color = new Color(0.6f, 0.58f, 0.55f),
                cost = new Cost(steel: 3), maxHp = 300, upgradeSeconds = 20f, maxLevel = 1, indestructible = true,
                recipe = "Can't be broken. Leave a Gate"
            });
            Add(new BuildingDef {
                id = "gate", displayName = "Gate", category = BuildingCategory.Defense,
                width = 1, depth = 1, height = 1.9f, color = new Color(0.9f, 0.92f, 0.95f),
                cost = new Cost(steel: 15, coins: 5), maxHp = 700, maxLevel = 5, upgradeSeconds = 40f,
                recipe = "The way in. Dinos must break it"
            });
            Add(new BuildingDef {
                id = "watchtower", displayName = "Gun Turret", category = BuildingCategory.Defense,
                width = 2, depth = 2, height = 3.5f, color = new Color(0.5f, 0.38f, 0.25f),
                cost = new Cost(steel: 30, coins: 10), maxHp = 250,
                lightRadius = 8f, attackRange = 9f, attackDamage = 12, attackInterval = 1f,
                recipe = "Rapid fire. Strong vs raptors and soldiers"
            });
            Add(new BuildingDef {
                id = "ballista", displayName = "Laser Turret", category = BuildingCategory.Defense,
                width = 2, depth = 2, height = 1.3f, color = new Color(0.5f, 0.38f, 0.25f),
                cost = new Cost(steel: 45, energy: 20, coins: 20), maxHp = 220, unlockLevel = 2,
                attackRange = 12f, attackDamage = 40, attackInterval = 2.6f, projectile = 1,
                recipe = "Beam. Strong vs triceratops and heavies"
            });
            Add(new BuildingDef {
                id = "cannon", displayName = "Missile Launcher", category = BuildingCategory.Defense,
                width = 2, depth = 2, height = 2.1f, color = new Color(0.55f, 0.53f, 0.5f),
                cost = new Cost(steel: 70, energy: 30, coins: 40), maxHp = 400, unlockLevel = 2,
                attackRange = 9f, attackDamage = 28, attackInterval = 3.2f, splashRadius = 2.2f, projectile = 2, hitsAir = true,
                recipe = "Anti-air. Strong vs pterosaurs and drones"
            });
            Add(new BuildingDef {
                id = "dronepad", displayName = "Drone Pad", category = BuildingCategory.Defense,
                width = 2, depth = 2, height = 0.6f, color = new Color(0.5f, 0.55f, 0.6f),
                cost = new Cost(steel: 50, energy: 30), maxHp = 300, maxLevel = 3, unlockLevel = 4, hitsAir = true,
                recipe = "2 patrol drones. Hit air and ground"
            });
            Add(new BuildingDef {
                id = "powerplant", displayName = "Power Plant", category = BuildingCategory.Economy,
                width = 2, depth = 2, height = 2f, color = new Color(0.55f, 0.6f, 0.65f),
                cost = new Cost(steel: 35, coins: 15), maxHp = 250, workers = 2, unlockLevel = 2,
                produces = ResourceType.Energy, produceAmount = 6, produceInterval = 8f, recipe = "Energy day and night"
            });
            Add(new BuildingDef {
                id = "factory", displayName = "Parts Factory", category = BuildingCategory.Economy,
                width = 3, depth = 3, height = 2.4f, color = new Color(0.6f, 0.5f, 0.4f),
                cost = new Cost(steel: 40, energy: 20), maxHp = 350, workers = 3, unlockLevel = 3,
                produces = ResourceType.Steel, produceAmount = 7, produceInterval = 10f, consumeEnergy = 2,
                recipe = "Energy -> Steel, anywhere"
            });
            AddArmy();
        }

        static void AddArmy()
        {
            Add(new BuildingDef {
                id = "barracks", displayName = "Barracks", category = BuildingCategory.Army,
                width = 3, depth = 3, height = 2.2f, color = new Color(0.35f, 0.42f, 0.5f),
                cost = new Cost(steel: 30, energy: 15), maxHp = 400, maxLevel = 5, unlockLevel = 2, upgradeSeconds = 90f,
                trains = new[] { "rifleman", "heavy" }, recipe = "Trains soldiers in battle suits"
            });
            Add(new BuildingDef {
                id = "camp", displayName = "Army Camp", category = BuildingCategory.Army,
                width = 3, depth = 3, height = 0.6f, color = new Color(0.45f, 0.5f, 0.4f),
                cost = new Cost(steel: 20, food: 10), maxHp = 300, maxLevel = 5, unlockLevel = 2, upgradeSeconds = 60f,
                campSpace = 20, campPerLevel = 10, recipe = "Your army lives here and defends"
            });
            Add(new BuildingDef {
                id = "command", displayName = "Command Center", category = BuildingCategory.Army,
                width = 2, depth = 2, height = 2.4f, color = new Color(0.9f, 0.75f, 0.3f),
                cost = new Cost(steel: 40, coins: 20), maxHp = 500, maxLevel = 1, unlockLevel = 2,
                commandCenter = true, recipe = "Home of your Commander hero"
            });
            Add(new BuildingDef {
                id = "robotfactory", displayName = "Robot Factory", category = BuildingCategory.Army,
                width = 2, depth = 2, height = 2.4f, color = new Color(0.4f, 0.45f, 0.55f),
                cost = new Cost(steel: 50, energy: 40), maxHp = 450, maxLevel = 5, unlockLevel = 3, upgradeSeconds = 120f,
                trains = new[] { "drone" }, recipe = "Builds attack drones (they fly)"
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
            var list = ordered.FindAll(d => d.category == c);
            list.Sort((a, b) => System.Array.IndexOf(MenuOrder, a.id).CompareTo(System.Array.IndexOf(MenuOrder, b.id)));
            foreach (var d in list) yield return d;
        }
    }
}
