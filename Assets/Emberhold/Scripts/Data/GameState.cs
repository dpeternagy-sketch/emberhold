using System;
using System.Collections.Generic;

namespace Emberhold
{
    /// <summary>
    /// Everything that must survive between sessions. Plain serializable data,
    /// so it can be saved locally now and synced to a cloud save (e.g. Unity Cloud Save) later.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 3;   // 3 = near-future city with 5 resources
        public int version = CurrentVersion;
        public int food = 40, water = 30, energy = 20, steel = 80, coins = 60;
        public int population = 4;
        /// <summary>Dinosaurs caught in traps and kept in pens (they guard the city).</summary>
        public int tamedRaptors, tamedTrikes;
        public int extraBuilders;          // bonus builder slots (VIP / shop later)
        public int night = 0;                 // nights survived
        public float dayTimeLeft = 150f;
        public long lastSavedUtcTicks;
        public List<BuildingData> buildings = new List<BuildingData>();

        public int Get(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Food: return food;
                case ResourceType.Water: return water;
                case ResourceType.Energy: return energy;
                case ResourceType.Steel: return steel;
                case ResourceType.Coins: return coins;
            }
            return 0;
        }

        public void Add(ResourceType t, int amount)
        {
            switch (t)
            {
                case ResourceType.Food: food = Math.Max(0, food + amount); break;
                case ResourceType.Water: water = Math.Max(0, water + amount); break;
                case ResourceType.Energy: energy = Math.Max(0, energy + amount); break;
                case ResourceType.Steel: steel = Math.Max(0, steel + amount); break;
                case ResourceType.Coins: coins = Math.Max(0, coins + amount); break;
            }
        }

        public bool CanAfford(Cost c)
        {
            return steel >= c.steel && energy >= c.energy && coins >= c.coins;
        }

        public bool Pay(Cost c)
        {
            if (!CanAfford(c)) return false;
            steel -= c.steel; energy -= c.energy; coins -= c.coins;
            return true;
        }

        /// <summary>Names of the resources you are short of, e.g. "steel, energy".</summary>
        public string Missing(Cost c)
        {
            var list = new List<string>();
            if (steel < c.steel) list.Add("steel");
            if (energy < c.energy) list.Add("energy");
            if (coins < c.coins) list.Add("coins");
            return list.Count == 0 ? "resources" : string.Join(", ", list.ToArray());
        }

        public void Refund(Cost c, float fraction)
        {
            steel += (int)(c.steel * fraction); energy += (int)(c.energy * fraction); coins += (int)(c.coins * fraction);
        }
    }

    [Serializable]
    public class BuildingData
    {
        public string uid;
        public string defId;
        public int x, z;              // grid origin (lower-left cell)
        public int rotation;          // 0..3, quarter turns
        public int level = 1;
        public int hp;
        public long upgradeEndUtcTicks;  // 0 = not upgrading
        public bool ruined;              // destroyed in a raid: stays as a smoking ruin until rebuilt

        public bool IsUpgrading { get { return upgradeEndUtcTicks > 0; } }
    }
}
