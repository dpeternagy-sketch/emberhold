using System;
using System.Threading.Tasks;

namespace Emberhold.Online
{
    /// <summary>
    /// Everything online in Emberhold. Two implementations:
    ///  - MockOnlineService: no server, no packages; fake friends with bases to raid. Default.
    ///  - UgsOnlineService:  Unity Gaming Services (Authentication + Cloud Code + Cloud Save).
    ///    Compiled only when the scripting define EMBERHOLD_UGS is set (see Server/README.md).
    /// Pick one with OnlineServices.Current.
    ///
    /// Every call returns a reply with ok/error; calls never throw for game or network errors
    /// (error = "network" when offline). Calls must be made from the Unity main thread.
    /// </summary>
    public interface IOnlineService
    {
        /// <summary>True after SignInAsync succeeded.</summary>
        bool SignedIn { get; }
        string PlayerId { get; }

        /// <summary>Guest sign-in (no login screen), then "hello": makes the profile and friend code.</summary>
        Task<HelloReply> SignInAsync(string displayName);

        Task<NameReply> SetNameAsync(string displayName);

        /// <summary>The full cloud city (new phone / reinstall).</summary>
        Task<LoadCityReply> LoadCityAsync();

        /// <summary>Upload the city. The reply carries raid losses / loot to apply (deltas) and any clamp.</summary>
        Task<SaveCityReply> SaveCityAsync(GameState state);

        // ---- friends + friends leaderboard
        Task<FriendsReply> GetFriendsAsync();
        Task<AddFriendReply> AddFriendAsync(string friendCode);
        Task<RespondFriendReply> RespondFriendAsync(string playerId, bool accept);
        Task<OnlineReply> RemoveFriendAsync(string playerId);

        // ---- raids
        /// <summary>Raid an offline friend; revengeOfRaidId = a raid log entry where you were the defender.</summary>
        Task<StartRaidReply> StartRaidAsync(string defenderId, string revengeOfRaidId = null);
        Task<FinishRaidReply> FinishRaidAsync(RaidReport report);
        Task<RaidLogReply> GetRaidLogAsync();
        Task<ReplayReply> GetReplayAsync(string raidId);
    }

    /// <summary>Error codes from the server, with short English text for a toast.</summary>
    public static class OnlineErrors
    {
        public static string Message(string code)
        {
            switch (code)
            {
                case "network": return "No connection. Try again later.";
                case "not_signed_in": return "Connecting...";
                case "code_not_found": return "No player with that code.";
                case "bad_code": return "Friend codes have 7 letters.";
                case "own_code": return "That is your own code.";
                case "already_friends": return "You are already friends.";
                case "too_many_friends": return "Friend list is full.";
                case "no_request": return "That request is gone.";
                case "bad_name": return "Pick a name with 2 to 16 letters.";
                case "not_friends": return "You can only raid friends.";
                case "target_online": return "They are playing right now. Try later.";
                case "target_shielded": return "They are protected by a shield.";
                case "target_busy": return "Someone is attacking them right now.";
                case "target_has_no_city": return "They have no city yet.";
                case "raid_in_progress": return "Finish your current raid first.";
                case "save_first": return "Saving your city... try again.";
                case "revenge_used": return "Revenge already taken.";
                case "revenge_expired": return "Too late for revenge.";
                case "bad_revenge": return "Revenge is not possible.";
                case "no_raid": return "That raid has ended.";
                case "no_replay": return "Replay not available.";
                case "invalid_city": return "Could not save your city.";
            }
            return "Something went wrong. Try again.";
        }
    }

    /// <summary>
    /// Server time, so offline production and timers cannot be cheated by changing the phone clock.
    /// Every reply updates it. Until the first reply it is the phone's clock.
    /// Use ServerClock.UtcNow where the game uses DateTime.UtcNow for offline production / upgrade / training timers.
    /// </summary>
    public static class ServerClock
    {
        static long offsetMs;
        public static bool Synced { get; private set; }

        public static void Observe(long serverNowUnixMs)
        {
            if (serverNowUnixMs <= 0) return;
            offsetMs = serverNowUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Synced = true;
        }

        public static DateTime UtcNow { get { return DateTime.UtcNow.AddMilliseconds(offsetMs); } }
        public static long NowUnixMs { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + offsetMs; } }

        public static DateTime FromUnixMs(long ms) { return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime; }
        public static TimeSpan Until(long unixMs) { return TimeSpan.FromMilliseconds(Math.Max(0, unixMs - NowUnixMs)); }
    }

    /// <summary>Picks the service. Set Current yourself in tests; otherwise UGS when compiled in, else the mock.</summary>
    public static class OnlineServices
    {
        static IOnlineService current;

        public static IOnlineService Current
        {
            get
            {
                if (current == null)
                {
#if EMBERHOLD_UGS
                    current = new UgsOnlineService();
#else
                    current = new MockOnlineService();
#endif
                }
                return current;
            }
            set { current = value; }
        }
    }
}
