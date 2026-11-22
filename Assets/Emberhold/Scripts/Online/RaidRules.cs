using System;
using System.Collections.Generic;

namespace Emberhold.Online
{
    /// <summary>
    /// The raid rules in C#. The SERVER (Server/cloudcode/emberhold.js, object R) is the real judge;
    /// this copy is for the mock and for showing numbers in the UI (e.g. "Loot up to ..."). Keep them equal.
    /// </summary>
    public static class RaidRules
    {
        public const float LootShare = 0.2f;
        public static readonly int[] LootCapByHq = { 150, 300, 600, 1200, 2000 };
        public const float HqBonusShare = 0.1f;
        public const int BattleSeconds = 180;
        public const int RaidMaxSeconds = 180 + 45;
        public const int RaidMinSeconds = 8;
        public const int OnlineWindowSeconds = 180;
        public const int RevengeWindowHours = 48;
        public const int TrophiesPerStar = 10, TrophiesLoss = 5, DefenderLossPerStar = 6;
        public const float HeroDpsPerLevel = 40f, HpSlackFactor = 2.5f, HpSlackFlat = 1500f;

        static readonly HashSet<string> NotRaidable = new HashSet<string> { "wall", "canal", "trap" };

        /// <summary>Counts toward destruction % (walls, canals and traps do not).</summary>
        public static bool Raidable(BuildingData b) { return b != null && !b.ruined && !NotRaidable.Contains(b.defId); }

        public static int BuildingHp(BuildingData b)
        {
            var d = BuildingCatalog.Get(b.defId);
            int hp = d != null ? d.maxHp : 300;
            return (int)Math.Round(hp * (1f + 0.5f * (Math.Max(1, b.level) - 1)));
        }

        public static int Stars(int pct, bool hqDown) { return (pct >= 50 ? 1 : 0) + (hqDown ? 1 : 0) + (pct >= 100 ? 1 : 0); }

        public static int ShieldHours(int pct) { return pct >= 90 ? 16 : pct >= 40 ? 12 : 0; }

        public static int AttackerTrophies(int stars) { return stars > 0 ? stars * TrophiesPerStar : -TrophiesLoss; }
        public static int DefenderTrophies(int stars) { return stars > 0 ? -stars * DefenderLossPerStar : TrophiesLoss; }

        public static int AtStake(int defenderAmount, int attackerHq)
        {
            int cap = LootCapByHq[Math.Max(1, Math.Min(5, attackerHq)) - 1];
            return Math.Min((int)Math.Floor(defenderAmount * LootShare), cap);
        }

        public static int Loot(int atStake, int pct, bool hqDown)
        {
            double share = atStake * pct / 100.0;
            if (hqDown) share += atStake * HqBonusShare;
            return Math.Max(0, Math.Min((int)Math.Floor(share), atStake));
        }

        public static float UnitDps(string unitId)
        {
            var u = UnitDef.Get(unitId);
            return u == null ? 0f : u.damage / Math.Max(0.1f, u.interval);
        }

        /// <summary>HP the army could destroy in that time (with slack). More than this = cheat or bug.</summary>
        public static float HpBudget(TroopCounts used, bool hero, int heroLevel, float seconds)
        {
            float dps = used.rifleman * UnitDps("rifleman") + used.heavy * UnitDps("heavy") + used.drone * UnitDps("drone");
            if (hero) dps += HeroDpsPerLevel * Math.Max(1, heroLevel);
            return dps * seconds * HpSlackFactor + (dps > 0 ? HpSlackFlat : 0);
        }

        public static int HqLevel(IList<BuildingData> buildings)
        {
            foreach (var b in buildings) if (b.defId == BuildingCatalog.Castle) return Math.Max(1, Math.Min(5, b.level));
            return 1;
        }
    }
}
