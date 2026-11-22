using System;
using System.Collections.Generic;

namespace Emberhold.Online
{
    // Plain [Serializable] classes so JsonUtility can read the server's JSON.
    // Field names match the server (Server/cloudcode/emberhold.js) exactly; do not rename them.

    [Serializable]
    public class Resources5
    {
        public int steel, energy, coins, food, water;

        public int Get(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Steel: return steel;
                case ResourceType.Energy: return energy;
                case ResourceType.Coins: return coins;
                case ResourceType.Food: return food;
                case ResourceType.Water: return water;
            }
            return 0;
        }

        public bool IsZero { get { return steel == 0 && energy == 0 && coins == 0 && food == 0 && water == 0; } }

        public override string ToString()
        {
            var p = new List<string>();
            if (steel != 0) p.Add(steel + " steel");
            if (energy != 0) p.Add(energy + " energy");
            if (coins != 0) p.Add(coins + " coins");
            if (food != 0) p.Add(food + " food");
            if (water != 0) p.Add(water + " water");
            return p.Count == 0 ? "nothing" : string.Join(", ", p.ToArray());
        }
    }

    [Serializable]
    public class TroopCounts
    {
        public int rifleman, heavy, drone;
    }

    /// <summary>Base of every reply. ok=false: see error (codes in OnlineErrors).</summary>
    [Serializable]
    public class OnlineReply
    {
        public bool ok;
        public string error;
        /// <summary>Server time, Unix milliseconds. ServerClock uses it.</summary>
        public long serverNow;
    }

    [Serializable]
    public class HelloReply : OnlineReply
    {
        public string playerId, name, code;
        public bool hasCloudCity;
        public long cloudSavedAt;
        public int cloudRev;
    }

    [Serializable]
    public class NameReply : OnlineReply { public string name; }

    [Serializable]
    public class LoadCityReply : OnlineReply
    {
        /// <summary>Full GameState JSON, or null/empty when there is no cloud city.</summary>
        public string city;
        public long savedAt;
        public int rev;
    }

    [Serializable]
    public class LedgerInfo
    {
        /// <summary>raid_loss: attacker name. raid_gain: empty.</summary>
        public string by;
        /// <summary>raid_gain: defender name.</summary>
        public string from;
        public int stars, pct;
        /// <summary>raid_loss: uids of your buildings that are now ruins.</summary>
        public List<string> ruined = new List<string>();
    }

    /// <summary>Something that changed your city on the server (raid loss or loot gain).</summary>
    [Serializable]
    public class LedgerEntry
    {
        public string id, kind;      // kind: "raid_loss" | "raid_gain"
        public Resources5 res;       // add these (losses are negative)
        public long at;
        public LedgerInfo info;
        public bool IsLoss { get { return kind == "raid_loss"; } }
    }

    [Serializable]
    public class SaveCityReply : OnlineReply
    {
        public int rev;
        /// <summary>Apply each one to the local GameState now (CloudSaveSync does it for you).</summary>
        public List<LedgerEntry> deltas = new List<LedgerEntry>();
        /// <summary>True when the server lowered resources that were too high; then set them to `clamped`.</summary>
        public bool clampedAny;
        public Resources5 clamped;
        public int trophies;
        public long shieldUntil;
        public string code;
    }

    [Serializable]
    public class FriendRow
    {
        public string playerId, name;
        public int hq, trophies, rank;
        public long shieldUntil;
        public bool online, canRaid, me;
    }

    [Serializable]
    public class FriendRequest
    {
        public string playerId, name;
        public long at;
    }

    [Serializable]
    public class FriendsReply : OnlineReply
    {
        public string code;
        public List<FriendRow> friends = new List<FriendRow>();
        /// <summary>Friends + you, sorted by trophies (rank 1 first). This is the friends leaderboard.</summary>
        public List<FriendRow> leaderboard = new List<FriendRow>();
        public List<FriendRequest> incoming = new List<FriendRequest>();
        public List<string> outgoing = new List<string>();
    }

    [Serializable]
    public class AddFriendReply : OnlineReply
    {
        /// <summary>True when they had already asked you, so you are friends right away.</summary>
        public bool friends;
    }

    [Serializable]
    public class RespondFriendReply : OnlineReply { public bool accepted; }

    /// <summary>The defender's city as the attacker sees it.</summary>
    [Serializable]
    public class RaidBase
    {
        public List<BuildingData> buildings = new List<BuildingData>();
        public int tamedRaptors, tamedTrikes, heroLevel, riflemen, heavies, drones;
    }

    [Serializable]
    public class StartRaidReply : OnlineReply
    {
        public string raidId, defenderId, defenderName;
        /// <summary>RaidBase as JSON text; use Base.</summary>
        public string @base;
        /// <summary>Most loot this raid can give (all at 100% destruction).</summary>
        public Resources5 atStake;
        /// <summary>Your troops the server knows you have (by unit id).</summary>
        public TroopCounts army;
        /// <summary>Use for every random choice in the battle, so the replay matches.</summary>
        public int seed;
        /// <summary>Finish before this (Unix ms) or the raid gives nothing.</summary>
        public long expiresAt;

        [NonSerialized] RaidBase parsed;
        public RaidBase Base
        {
            get
            {
                if (parsed == null && !string.IsNullOrEmpty(@base)) parsed = Json.From<RaidBase>(@base);
                return parsed;
            }
        }
    }

    /// <summary>What the attacker's phone reports at the end of a battle. No loot here: the server decides it.</summary>
    [Serializable]
    public class RaidReport
    {
        public string raidId;
        /// <summary>uids of the defender's buildings that were destroyed.</summary>
        public List<string> destroyed = new List<string>();
        public float durationSec;
        public TroopCounts troopsUsed = new TroopCounts();
        public bool heroUsed;
        /// <summary>ReplayRecorder.ToText(); kept under 16,000 characters.</summary>
        public string replay;
    }

    [Serializable]
    public class FinishRaidReply : OnlineReply
    {
        public string raidId;
        public int stars, destructionPct, trophies, shieldHours;
        public bool hqDown;
        /// <summary>True when the server refused part of the report (too fast, too strong, too late).</summary>
        public bool rejected;
        /// <summary>Arrives in your city at the next save (as a raid_gain delta).</summary>
        public Resources5 loot;
        /// <summary>Remove these from your army now.</summary>
        public TroopCounts troopsLost;
    }

    [Serializable]
    public class RaidLogEntry
    {
        public string raidId, role, otherId, otherName;   // role: "attacker" | "defender"
        public long at;
        public int stars, pct, trophies;
        public Resources5 loot;
        public bool hqDown, revenge, revenged, canRevenge;
    }

    [Serializable]
    public class RaidLogReply : OnlineReply
    {
        public List<RaidLogEntry> entries = new List<RaidLogEntry>();
        public int trophies;
        public long shieldUntil;
    }

    [Serializable]
    public class ReplayReply : OnlineReply
    {
        public string raidId, attackerId, defenderId, data;
        public int seed;
        public long at;
        /// <summary>The defender's buildings at raid time, as a JSON array of BuildingData; use Buildings.</summary>
        public string @base;

        [NonSerialized] List<BuildingData> parsed;
        public List<BuildingData> Buildings
        {
            get
            {
                if (parsed == null && !string.IsNullOrEmpty(@base)) parsed = Json.From<BuildingList>("{\"items\":" + @base + "}").items;
                return parsed;
            }
        }
    }

    [Serializable]
    public class BuildingList { public List<BuildingData> items = new List<BuildingData>(); }

    // ---- request payloads (sent as JSON text)

    [Serializable] class NameArgs { public string name; }
    [Serializable] class CityArgs { public string city; }
    [Serializable] class CodeArgs { public string code; }
    [Serializable] class PlayerArgs { public string playerId; public bool accept; }
    [Serializable] class StartRaidArgs { public string defenderId; public string revengeOf; }
    [Serializable] class RaidIdArgs { public string raidId; }

    internal static class Args
    {
        public static string Name(string n) { return Json.To(new NameArgs { name = n }); }
        public static string City(GameState s) { return Json.To(new CityArgs { city = Json.To(s) }); }
        public static string Code(string c) { return Json.To(new CodeArgs { code = c }); }
        public static string Player(string id, bool accept) { return Json.To(new PlayerArgs { playerId = id, accept = accept }); }
        public static string StartRaid(string id, string revengeOf) { return Json.To(new StartRaidArgs { defenderId = id, revengeOf = revengeOf }); }
        public static string RaidId(string id) { return Json.To(new RaidIdArgs { raidId = id }); }
    }

    /// <summary>JsonUtility in one place (tests swap nothing; Unity provides it).</summary>
    public static class Json
    {
        public static string To(object o) { return UnityEngine.JsonUtility.ToJson(o); }
        public static T From<T>(string s) { return UnityEngine.JsonUtility.FromJson<T>(s); }
    }
}
