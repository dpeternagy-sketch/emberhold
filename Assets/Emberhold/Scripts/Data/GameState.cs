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
        public int version = 1;
        public int food = 40, wood = 80, stone = 30, gold = 50, weapons = 2;
        public int population = 4;
        public int night = 0;                 // nights survived
        public float dayTimeLeft = 150f;
        public long lastSavedUtcTicks;
        public List<BuildingData> buildings = new List<BuildingData>();

        public int Get(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Food: return food;
                case ResourceType.Wood: return wood;
                case ResourceType.Stone: return stone;
                case ResourceType.Gold: return gold;
                case ResourceType.Weapons: return weapons;
            }
            return 0;
        }

        public void Add(ResourceType t, int amount)
        {
            switch (t)
            {
                case ResourceType.Food: food = Math.Max(0, food + amount); break;
                case ResourceType.Wood: wood = Math.Max(0, wood + amount); break;
                case ResourceType.Stone: stone = Math.Max(0, stone + amount); break;
                case ResourceType.Gold: gold = Math.Max(0, gold + amount); break;
                case ResourceType.Weapons: weapons = Math.Max(0, weapons + amount); break;
            }
        }

        public bool CanAfford(Cost c)
        {
            return wood >= c.wood && stone >= c.stone && gold >= c.gold && weapons >= c.weapons;
        }

        public bool Pay(Cost c)
        {
            if (!CanAfford(c)) return false;
            wood -= c.wood; stone -= c.stone; gold -= c.gold; weapons -= c.weapons;
            return true;
        }

        public void Refund(Cost c, float fraction)
        {
            wood += (int)(c.wood * fraction); stone += (int)(c.stone * fraction);
            gold += (int)(c.gold * fraction); weapons += (int)(c.weapons * fraction);
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

        public bool IsUpgrading { get { return upgradeEndUtcTicks > 0; } }
    }
}
