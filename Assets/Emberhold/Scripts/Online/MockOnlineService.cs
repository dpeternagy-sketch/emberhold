using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Emberhold.Online
{
    /// <summary>
    /// Offline stand-in for the server, so all online screens can be built and tested with no account,
    /// no packages and no internet. Same replies and error codes as the real server.
    ///
    /// Fake friends (their bases are copies of YOUR last saved city with other levels, so positions are valid):
    ///   Nour  (HQ 2, offline)        - can be raided
    ///   Karim (HQ 3, online now)     - "target_online"
    ///   Lina  (HQ 1, shielded 6 h)   - "target_shielded"
    /// Incoming request from "Yara". Friend code "SAMI777" adds Sami (accepts at once); any other code = not found.
    /// Debug: SimulateRaidOnMe() makes Nour raid you; the loss arrives at your next SaveCityAsync, with a replay and revenge.
    /// Nothing is stored on disk: the mock starts fresh every Play.
    /// </summary>
    public class MockOnlineService : IOnlineService
    {
        /// <summary>Fake network delay, so loading spinners can be seen. 0 in tests.</summary>
        public int LatencyMs = 250;
        /// <summary>Set true to test the "No connection" paths.</summary>
        public bool SimulateOffline;

        class Bot
        {
            public string id, name;
            public int hq, trophies;
            public bool online;
            public long shieldUntil;
            public Resources5 res;
            public bool friend;
        }

        class Pending
        {
            public StartRaidReply reply;
            public List<BuildingData> baseBuildings;
            public TroopCounts army;
            public int heroLevel;
            public long startedAt;
            public string revengeOf;
        }

        class StoredReplay { public string raidId, attackerId, defenderId, data; public int seed; public long at; public List<BuildingData> buildings; }

        readonly Dictionary<string, Bot> bots = new Dictionary<string, Bot>();
        readonly List<FriendRequest> incoming = new List<FriendRequest>();
        readonly List<LedgerEntry> ledger = new List<LedgerEntry>();
        readonly List<RaidLogEntry> log = new List<RaidLogEntry>();
        readonly List<StoredReplay> replays = new List<StoredReplay>();
        readonly Random rng = new Random(7);
        GameState lastSaved;
        string lastSavedJson;
        Pending raid;
        string name = "Commander";
        int trophies;
        long shieldUntil;
        int rev;

        public bool SignedIn { get; private set; }
        public string PlayerId { get { return SignedIn ? "mock-player" : null; } }
        public string Code { get { return "MOCK123"; } }

        public MockOnlineService()
        {
            long now = Now;
            AddBot("bot-nour", "Nour", 2, 120, false, 0, true);
            AddBot("bot-karim", "Karim", 3, 210, true, 0, true);
            AddBot("bot-lina", "Lina", 1, 40, false, now + 6L * 3600 * 1000, true);
            AddBot("bot-yara", "Yara", 2, 90, false, 0, false);
            AddBot("bot-sami", "Sami", 1, 15, false, 0, false);
            incoming.Add(new FriendRequest { playerId = "bot-yara", name = "Yara", at = now - 3600 * 1000 });
        }

        void AddBot(string id, string n, int hq, int tr, bool online, long shield, bool friend)
        {
            bots[id] = new Bot { id = id, name = n, hq = hq, trophies = tr, online = online, shieldUntil = shield, friend = friend,
                                 res = new Resources5 { steel = 400 * hq, energy = 200 * hq, coins = 300 * hq, food = 250 * hq, water = 150 * hq } };
        }

        static long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } }

        async Task<T> Reply<T>(T r) where T : OnlineReply
        {
            if (LatencyMs > 0) await Task.Delay(LatencyMs);
            if (SimulateOffline) { r.ok = false; r.error = "network"; }
            r.serverNow = Now;
            ServerClock.Observe(r.serverNow);
            return r;
        }

        static T Fail<T>(T r, string code) where T : OnlineReply { r.ok = false; r.error = code; return r; }

        // ------------------------------------------------ account / save

        public Task<HelloReply> SignInAsync(string displayName)
        {
            SignedIn = !SimulateOffline;
            if (!string.IsNullOrEmpty(displayName)) name = displayName;
            return Reply(new HelloReply { ok = true, playerId = "mock-player", name = name, code = Code, hasCloudCity = lastSaved != null, cloudRev = rev });
        }

        public Task<NameReply> SetNameAsync(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Trim().Length < 2 || n.Length > 16) return Reply(Fail(new NameReply(), "bad_name"));
            name = n.Trim();
            return Reply(new NameReply { ok = true, name = name });
        }

        public Task<LoadCityReply> LoadCityAsync()
        {
            return Reply(new LoadCityReply { ok = true, city = lastSavedJson, rev = rev });
        }

        public Task<SaveCityReply> SaveCityAsync(GameState state)
        {
            if (state == null || state.buildings == null || state.buildings.Count == 0) return Reply(Fail(new SaveCityReply(), "invalid_city"));
            lastSaved = CopyArmyAndBuildings(state);
            try { lastSavedJson = Json.To(state); } catch (Exception) { lastSavedJson = null; }   // no JsonUtility outside Unity (tests)
            var r = new SaveCityReply { ok = true, rev = ++rev, trophies = trophies, shieldUntil = shieldUntil, code = Code };
            foreach (var l in ledger) r.deltas.Add(l);
            ledger.Clear();
            return Reply(r);
        }

        static GameState CopyArmyAndBuildings(GameState s)
        {
            var c = new GameState { riflemen = s.riflemen, heavies = s.heavies, drones = s.drones, heroLevel = s.heroLevel,
                                    steel = s.steel, energy = s.energy, coins = s.coins, food = s.food, water = s.water };
            c.buildings = new List<BuildingData>();
            foreach (var b in s.buildings) c.buildings.Add(CopyBuilding(b, b.uid));
            return c;
        }

        static BuildingData CopyBuilding(BuildingData b, string uid)
        {
            return new BuildingData { uid = uid, defId = b.defId, x = b.x, z = b.z, rotation = b.rotation, level = b.level, hp = b.hp, ruined = false };
        }

        // ------------------------------------------------ friends

        public Task<FriendsReply> GetFriendsAsync()
        {
            long now = Now;
            var r = new FriendsReply { ok = true, code = Code };
            foreach (var b in bots.Values)
            {
                if (!b.friend) continue;
                r.friends.Add(new FriendRow { playerId = b.id, name = b.name, hq = b.hq, trophies = b.trophies, shieldUntil = b.shieldUntil,
                                              online = b.online, canRaid = !b.online && b.shieldUntil <= now && (raid == null || raid.reply.defenderId != b.id) });
            }
            r.leaderboard.AddRange(r.friends);
            r.leaderboard.Add(new FriendRow { playerId = "mock-player", name = name, hq = lastSaved != null ? RaidRules.HqLevel(lastSaved.buildings) : 1, trophies = trophies, me = true });
            r.leaderboard.Sort((a, b) => b.trophies.CompareTo(a.trophies));
            for (int i = 0; i < r.leaderboard.Count; i++) r.leaderboard[i].rank = i + 1;
            r.incoming.AddRange(incoming);
            return Reply(r);
        }

        public Task<AddFriendReply> AddFriendAsync(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != 7) return Reply(Fail(new AddFriendReply(), "bad_code"));
            if (code == Code) return Reply(Fail(new AddFriendReply(), "own_code"));
            if (code != "SAMI777") return Reply(Fail(new AddFriendReply(), "code_not_found"));
            if (bots["bot-sami"].friend) return Reply(Fail(new AddFriendReply(), "already_friends"));
            bots["bot-sami"].friend = true;
            return Reply(new AddFriendReply { ok = true, friends = true });
        }

        public Task<RespondFriendReply> RespondFriendAsync(string playerId, bool accept)
        {
            int i = incoming.FindIndex(x => x.playerId == playerId);
            if (i < 0) return Reply(Fail(new RespondFriendReply(), "no_request"));
            incoming.RemoveAt(i);
            if (accept && bots.ContainsKey(playerId)) bots[playerId].friend = true;
            return Reply(new RespondFriendReply { ok = true, accepted = accept });
        }

        public Task<OnlineReply> RemoveFriendAsync(string playerId)
        {
            Bot b;
            if (bots.TryGetValue(playerId, out b)) b.friend = false;
            return Reply(new OnlineReply { ok = true });
        }

        // ------------------------------------------------ raids

        public Task<StartRaidReply> StartRaidAsync(string defenderId, string revengeOfRaidId = null)
        {
            long now = Now;
            Bot bot;
            if (!bots.TryGetValue(defenderId ?? "", out bot)) return Reply(Fail(new StartRaidReply(), "bad_target"));
            if (revengeOfRaidId != null)
            {
                var l = log.Find(x => x.raidId == revengeOfRaidId && x.role == "defender");
                if (l == null || l.otherId != defenderId) return Reply(Fail(new StartRaidReply(), "bad_revenge"));
                if (l.revenged) return Reply(Fail(new StartRaidReply(), "revenge_used"));
                if (now - l.at > RaidRules.RevengeWindowHours * 3600L * 1000) return Reply(Fail(new StartRaidReply(), "revenge_expired"));
            }
            else if (!bot.friend) return Reply(Fail(new StartRaidReply(), "not_friends"));
            if (raid != null && now < raid.reply.expiresAt) return Reply(Fail(new StartRaidReply(), "raid_in_progress"));
            if (lastSaved == null) return Reply(Fail(new StartRaidReply(), "save_first"));
            if (bot.online) return Reply(Fail(new StartRaidReply(), "target_online"));
            if (bot.shieldUntil > now) return Reply(Fail(new StartRaidReply(), "target_shielded"));

            // the bot's base: a copy of your city, levels nudged to the bot's HQ
            var rb = new RaidBase { heroLevel = 1, riflemen = 5 * bot.hq };
            foreach (var b in lastSaved.buildings)
            {
                var c = CopyBuilding(b, "bot_" + b.uid);
                var def = BuildingCatalog.Get(b.defId);
                int max = def != null ? def.maxLevel : 1;
                c.level = b.defId == BuildingCatalog.Castle ? bot.hq : Math.Max(1, Math.Min(max, Math.Min(bot.hq, b.level + rng.Next(-1, 2))));
                c.hp = 0;   // 0 = full HP
                rb.buildings.Add(c);
            }
            int myHq = RaidRules.HqLevel(lastSaved.buildings);
            var stake = new Resources5 {
                steel = RaidRules.AtStake(bot.res.steel, myHq), energy = RaidRules.AtStake(bot.res.energy, myHq), coins = RaidRules.AtStake(bot.res.coins, myHq),
                food = RaidRules.AtStake(bot.res.food, myHq), water = RaidRules.AtStake(bot.res.water, myHq) };
            var army = new TroopCounts { rifleman = lastSaved.riflemen, heavy = lastSaved.heavies, drone = lastSaved.drones };
            string json = null;
            try { json = Json.To(rb); } catch (Exception) { }
            var reply = new StartRaidReply { ok = true, raidId = "mock_" + now, defenderId = bot.id, defenderName = bot.name, @base = json,
                                             atStake = stake, army = army, seed = rng.Next(), expiresAt = now + RaidRules.RaidMaxSeconds * 1000L };
            raid = new Pending { reply = reply, baseBuildings = rb.buildings, army = army, heroLevel = lastSaved.heroLevel, startedAt = now, revengeOf = revengeOfRaidId };
            shieldUntil = 0;
            return Reply(reply);
        }

        /// <summary>The base of the raid that is running (the mock gives it without JSON too).</summary>
        public List<BuildingData> CurrentRaidBuildings { get { return raid != null ? raid.baseBuildings : null; } }

        public Task<FinishRaidReply> FinishRaidAsync(RaidReport report)
        {
            long now = Now;
            if (raid == null || report == null || raid.reply.raidId != report.raidId) return Reply(Fail(new FinishRaidReply(), "no_raid"));
            var p = raid; raid = null;
            var bot = bots[p.reply.defenderId];
            float elapsed = (now - p.startedAt) / 1000f;
            bool late = now > p.reply.expiresAt;

            var used = new TroopCounts {
                rifleman = Math.Max(0, Math.Min(p.army.rifleman, report.troopsUsed != null ? report.troopsUsed.rifleman : 0)),
                heavy = Math.Max(0, Math.Min(p.army.heavy, report.troopsUsed != null ? report.troopsUsed.heavy : 0)),
                drone = Math.Max(0, Math.Min(p.army.drone, report.troopsUsed != null ? report.troopsUsed.drone : 0)) };
            float secs = Math.Min(Math.Min(Math.Max(0, report.durationSec), elapsed), RaidRules.BattleSeconds);
            float budget = RaidRules.HpBudget(used, report.heroUsed, p.heroLevel, secs);

            var byUid = new Dictionary<string, BuildingData>();
            int targets = 0;
            foreach (var b in p.baseBuildings) { byUid[b.uid] = b; if (RaidRules.Raidable(b)) targets++; }
            var destroyed = new List<BuildingData>();
            var seen = new HashSet<string>();
            float hp = 0; bool rejected = false;
            if (report.destroyed != null)
                foreach (var uid in report.destroyed)
                {
                    BuildingData b;
                    if (!byUid.TryGetValue(uid, out b) || !RaidRules.Raidable(b) || !seen.Add(uid)) continue;
                    hp += RaidRules.BuildingHp(b);
                    if (hp > budget) { rejected = true; break; }
                    destroyed.Add(b);
                }
            if (late || elapsed < RaidRules.RaidMinSeconds) { destroyed.Clear(); rejected = rejected || late; }

            int pct = targets > 0 ? (int)Math.Round(destroyed.Count * 100.0 / targets) : 0;
            bool hqDown = destroyed.Exists(b => b.defId == BuildingCatalog.Castle);
            int stars = RaidRules.Stars(pct, hqDown);
            var s = p.reply.atStake;
            var loot = new Resources5 {
                steel = RaidRules.Loot(s.steel, pct, hqDown), energy = RaidRules.Loot(s.energy, pct, hqDown), coins = RaidRules.Loot(s.coins, pct, hqDown),
                food = RaidRules.Loot(s.food, pct, hqDown), water = RaidRules.Loot(s.water, pct, hqDown) };
            bot.res.steel -= loot.steel; bot.res.energy -= loot.energy; bot.res.coins -= loot.coins; bot.res.food -= loot.food; bot.res.water -= loot.water;
            int shield = RaidRules.ShieldHours(pct);
            if (shield > 0) bot.shieldUntil = now + shield * 3600L * 1000;
            int tr = RaidRules.AttackerTrophies(stars);
            trophies = Math.Max(0, trophies + tr);
            bot.trophies = Math.Max(0, bot.trophies + RaidRules.DefenderTrophies(stars));
            if (lastSaved != null) { lastSaved.riflemen -= used.rifleman; lastSaved.heavies -= used.heavy; lastSaved.drones -= used.drone; }

            ledger.Add(new LedgerEntry { id = p.reply.raidId, kind = "raid_gain", res = loot, at = now, info = new LedgerInfo { from = bot.name, stars = stars, pct = pct } });
            log.Insert(0, new RaidLogEntry { raidId = p.reply.raidId, role = "attacker", otherId = bot.id, otherName = bot.name, at = now, stars = stars, pct = pct,
                                             trophies = tr, loot = loot, hqDown = hqDown, revenge = p.revengeOf != null });
            if (p.revengeOf != null) { var l = log.Find(x => x.raidId == p.revengeOf); if (l != null) l.revenged = true; }
            if (!string.IsNullOrEmpty(report.replay))
                replays.Insert(0, new StoredReplay { raidId = p.reply.raidId, attackerId = "mock-player", defenderId = bot.id, data = report.replay, seed = p.reply.seed, at = now, buildings = p.baseBuildings });

            return Reply(new FinishRaidReply { ok = true, raidId = p.reply.raidId, stars = stars, destructionPct = pct, hqDown = hqDown, loot = loot,
                                               trophies = tr, shieldHours = shield, rejected = rejected, troopsLost = used });
        }

        public Task<RaidLogReply> GetRaidLogAsync()
        {
            long now = Now;
            var r = new RaidLogReply { ok = true, trophies = trophies, shieldUntil = shieldUntil };
            foreach (var l in log)
            {
                l.canRevenge = l.role == "defender" && !l.revenged && now - l.at < RaidRules.RevengeWindowHours * 3600L * 1000;
                r.entries.Add(l);
            }
            return Reply(r);
        }

        public Task<ReplayReply> GetReplayAsync(string raidId)
        {
            var rp = replays.Find(x => x.raidId == raidId);
            if (rp == null) return Reply(Fail(new ReplayReply(), "no_replay"));
            string json = null;
            try { json = Json.To(new BuildingList { items = rp.buildings }); json = json.Substring("{\"items\":".Length, json.Length - "{\"items\":".Length - 1); } catch (Exception) { }
            return Reply(new ReplayReply { ok = true, raidId = rp.raidId, attackerId = rp.attackerId, defenderId = rp.defenderId, data = rp.data, seed = rp.seed, at = rp.at, @base = json });
        }

        // ------------------------------------------------ debug

        /// <summary>
        /// Pretend Nour raided you while you were away: you lose some resources, `ruinCount` buildings
        /// become ruins, you get a 12 h shield, a replay and a revenge chance. Shows at your next SaveCityAsync.
        /// </summary>
        public void SimulateRaidOnMe(int ruinCount = 2)
        {
            if (lastSaved == null) return;
            long now = Now;
            var bot = bots["bot-nour"];
            var ruined = new List<string>();
            foreach (var b in lastSaved.buildings)
            {
                if (ruined.Count >= ruinCount) break;
                if (RaidRules.Raidable(b) && b.defId != BuildingCatalog.Castle) ruined.Add(b.uid);
            }
            int pct = 45, stars = 0;
            var loss = new Resources5 { steel = -Math.Min(lastSaved.steel, 60), coins = -Math.Min(lastSaved.coins, 40), food = -Math.Min(lastSaved.food, 30) };
            string id = "mock_def_" + now;
            ledger.Add(new LedgerEntry { id = id, kind = "raid_loss", res = loss, at = now, info = new LedgerInfo { by = bot.name, stars = stars, pct = pct, ruined = ruined } });
            int tr = RaidRules.DefenderTrophies(stars);
            trophies = Math.Max(0, trophies + tr);
            shieldUntil = now + RaidRules.ShieldHours(pct) * 3600L * 1000;
            var lootShown = new Resources5 { steel = -loss.steel, coins = -loss.coins, food = -loss.food };
            log.Insert(0, new RaidLogEntry { raidId = id, role = "defender", otherId = bot.id, otherName = bot.name, at = now, stars = stars, pct = pct, trophies = tr, loot = lootShown });
            var rec = new ReplayRecorder(42);
            var castle = lastSaved.buildings.Find(b => b.defId == BuildingCatalog.Castle);
            float cx = castle != null ? castle.x : 0, cz = castle != null ? castle.z : 0;
            for (int i = 0; i < 8; i++) rec.RecordDrop(1f + i * 0.5f, "rifleman", cx + 12, cz - 6 + i);
            for (int i = 0; i < ruined.Count; i++) rec.RecordFall(30f + i * 20f, ruined[i]);
            replays.Insert(0, new StoredReplay { raidId = id, attackerId = bot.id, defenderId = "mock-player", data = rec.ToText(), seed = 42, at = now, buildings = lastSaved.buildings });
            bot.shieldUntil = 0; bot.online = false;
        }

        /// <summary>Make a fake friend online/offline or shielded, to test the friends screen.</summary>
        public void SetBot(string botId, bool online, int shieldHours)
        {
            Bot b;
            if (!bots.TryGetValue(botId, out b)) return;
            b.online = online;
            b.shieldUntil = shieldHours > 0 ? Now + shieldHours * 3600L * 1000 : 0;
        }
    }
}
