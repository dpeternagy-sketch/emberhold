using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Army units (balance-milestone2.md scaled to our economy: costs about /10, HP about /3).
    /// Counter rule: Gun beats Rifleman, Laser beats Heavy, Missile (anti-air) beats Drone.
    /// </summary>
    public class UnitDef
    {
        public string id, displayName, model, counterHint;
        public int space;
        public Cost cost;
        public float trainSeconds;
        public int hp, damage;
        public float interval = 1f, range, speed, height;
        public bool flies, hitsAir;
        public int barracksLevel = 1;   // level of the training building needed

        public static readonly Dictionary<string, UnitDef> All = new Dictionary<string, UnitDef>();
        public static readonly List<UnitDef> Ordered = new List<UnitDef>();

        static UnitDef()
        {
            Add(new UnitDef {
                id = "rifleman", displayName = "Rifleman", model = "Models/Space/astronautA", space = 1,
                cost = new Cost(coins: 1, food: 3), trainSeconds = 15f, hp = 40, damage = 5, interval = 1f,
                range = 3f, speed = 1.2f, height = 1.1f, hitsAir = true, counterHint = "Weak to Gun"
            });
            Add(new UnitDef {
                id = "heavy", displayName = "Heavy Suit", model = "Models/Space/astronautB", space = 5,
                cost = new Cost(steel: 6, food: 12), trainSeconds = 60f, hp = 300, damage = 12, interval = 1.2f,
                range = 1.3f, speed = 0.7f, height = 1.55f, barracksLevel = 2, counterHint = "Weak to Laser"
            });
            Add(new UnitDef {
                id = "drone", displayName = "Attack Drone", model = "Models/Space/craft_speederA", space = 3,
                cost = new Cost(steel: 6, energy: 8), trainSeconds = 40f, hp = 70, damage = 8, interval = 1f,
                range = 2.5f, speed = 1.8f, height = 0.6f, flies = true, hitsAir = true, counterHint = "Weak to Missile"
            });
        }

        static void Add(UnitDef d) { All[d.id] = d; Ordered.Add(d); }
        public static UnitDef Get(string id) { UnitDef d; return id != null && All.TryGetValue(id, out d) ? d : null; }
    }
}
