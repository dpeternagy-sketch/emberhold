// Tests for the client online layer (mock service, sync, rules, replay) without Unity.
// Run: Server/test/csharp/run.sh
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Emberhold;
using Emberhold.Online;

static class OnlineTests
{
    static int failed, passed;
    static void Check(bool ok, string what) { if (ok) passed++; else { failed++; Console.WriteLine("  FAIL " + what); } }

    static GameState City()
    {
        var s = new GameState { steel = 500, coins = 300, food = 200, riflemen = 20, heavies = 2, heroLevel = 1 };
        s.buildings.Add(new BuildingData { uid = "hq", defId = "castle", level = 2 });
        s.buildings.Add(new BuildingData { uid = "h1", defId = "house", x = 5 });
        s.buildings.Add(new BuildingData { uid = "h2", defId = "house", x = 7 });
        s.buildings.Add(new BuildingData { uid = "f1", defId = "farm", x = 9 });
        s.buildings.Add(new BuildingData { uid = "g1", defId = "watchtower", z = 6 });
        s.buildings.Add(new BuildingData { uid = "w1", defId = "wall", z = 9 });
        return s;
    }

    static async Task Run()
    {
        var mock = new MockOnlineService { LatencyMs = 0 };
        var sync = new CloudSaveSync(mock);
        var state = City();

        var hello = await sync.ConnectAsync("Peter");
        Check(hello.ok && hello.code == "MOCK123" && !hello.hasCloudCity, "sign in");
        Check(sync.Advice == CloudSaveSync.RestoreAdvice.None, "no restore on first run");

        var fr = await mock.GetFriendsAsync();
        Check(fr.friends.Count == 3 && fr.incoming.Count == 1, "3 fake friends + 1 request");
        Check(fr.leaderboard[0].rank == 1 && fr.leaderboard[0].trophies >= fr.leaderboard[1].trophies, "leaderboard sorted");
        Check(fr.friends.Find(f => f.name == "Karim").online && !fr.friends.Find(f => f.name == "Karim").canRaid, "online friend not raidable");

        Check((await mock.StartRaidAsync("bot-nour")).error == "save_first", "must save before raiding");
        var save = await sync.PushAsync(state);
        Check(save != null && save.ok && save.deltas.Count == 0, "first save");

        Check((await mock.StartRaidAsync("bot-karim")).error == "target_online", "online refused");
        Check((await mock.StartRaidAsync("bot-lina")).error == "target_shielded", "shield refused");
        Check((await mock.AddFriendAsync("nope")).error == "bad_code", "bad code");
        Check((await mock.AddFriendAsync("ABCDEFG")).error == "code_not_found", "unknown code");
        Check((await mock.AddFriendAsync("sami777")).friends, "add Sami by code");
        Check((await mock.RespondFriendAsync("bot-yara", true)).accepted, "accept Yara");

        var start = await mock.StartRaidAsync("bot-nour");
        Check(start.ok && mock.CurrentRaidBuildings.Count == 6, "raid starts with a base copy");
        Check(start.atStake.steel == RaidRules.AtStake(800, 2), "at stake = 20% capped by HQ");
        Check((await mock.StartRaidAsync("bot-yara")).error == "raid_in_progress", "one raid at a time");

        // the mock checks elapsed real time; pretend the battle took long enough
        var report = new RaidReport { raidId = start.raidId, durationSec = 60, troopsUsed = new TroopCounts { rifleman = 99 }, heroUsed = true };
        foreach (var b in mock.CurrentRaidBuildings) report.destroyed.Add(b.uid);
        var tooFast = await mock.FinishRaidAsync(report);
        Check(tooFast.ok && tooFast.destructionPct == 0, "raid under 8 seconds counts nothing");
        Check(tooFast.troopsLost.rifleman == 20, "troops capped by army");

        // RaidRules on their own
        Check(RaidRules.Stars(100, true) == 3 && RaidRules.Stars(50, false) == 1 && RaidRules.Stars(49, false) == 0, "stars");
        Check(RaidRules.Loot(200, 100, true) == 200 && RaidRules.Loot(200, 50, false) == 100 && RaidRules.Loot(200, 50, true) == 120, "loot");
        Check(RaidRules.ShieldHours(95) == 16 && RaidRules.ShieldHours(40) == 12 && RaidRules.ShieldHours(39) == 0, "shield");
        Check(!RaidRules.Raidable(new BuildingData { defId = "wall" }) && RaidRules.Raidable(new BuildingData { defId = "house" }), "walls not raidable");
        Check(RaidRules.HpBudget(new TroopCounts { rifleman = 1 }, false, 1, 10) < RaidRules.BuildingHp(new BuildingData { defId = "castle", level = 2 }) + 1500, "one rifleman cannot flatten an HQ in 10 s");

        // being raided while away -> applied at next save, building ruined, revenge + replay
        var ruinedUids = new List<string>();
        sync.BuildingRuined += uid => ruinedUids.Add(uid);
        LedgerEntry raided = null; sync.Raided += d => raided = d;
        mock.SimulateRaidOnMe(2);
        int steelBefore = state.steel;
        var s2 = await sync.PushAsync(state);
        Check(s2.deltas.Count == 2, "loss + loot deltas arrive (" + s2.deltas.Count + ")");
        Check(raided != null && raided.info.by == "Nour" && ruinedUids.Count == 2, "raid event + 2 ruins");
        Check(state.steel == steelBefore - 60, "steel lost applied: " + state.steel);
        Check((await sync.PushAsync(state)).deltas.Count == 0, "deltas only once");

        var log = await mock.GetRaidLogAsync();
        var def = log.entries.Find(e => e.role == "defender");
        Check(def != null && def.canRevenge, "revenge available");
        var rep = await mock.GetReplayAsync(def.raidId);
        var parsed = ReplayRecorder.Parse(rep.data);
        Check(parsed != null && parsed.Drops.Count == 8 && parsed.Falls.Count == 2, "replay parses");
        var rv = await mock.StartRaidAsync("bot-nour", def.raidId);
        Check(rv.ok, "revenge starts: " + rv.error);
        await mock.FinishRaidAsync(new RaidReport { raidId = rv.raidId });
        Check((await mock.StartRaidAsync("bot-nour", def.raidId)).error == "revenge_used", "revenge only once");

        // clamp applied by the sync
        var st = City(); st.steel = 99999;
        sync.Apply(st, new SaveCityReply { ok = true, clampedAny = true, clamped = new Resources5 { steel = 1200 } });
        Check(st.steel == 1200 && st.coins == 300, "clamp lowers only the clamped resource");

        // replay round trip
        var r = new ReplayRecorder(123);
        r.RecordDrop(1.25f, "heavy", 10.5f, -3.25f); r.RecordStrike(40f, 2, 3); r.RecordFall(55.5f, "abc");
        var back = ReplayRecorder.Parse(r.ToText());
        Check(back.Seed == 123 && back.Drops[0].unitId == "heavy" && Math.Abs(back.Drops[0].z + 3.25f) < 0.01f && back.Falls[0].uid == "abc", "replay round trip: " + r.ToText());
        Check(ReplayRecorder.Parse("junk") == null, "bad replay is null");

        mock.SimulateOffline = true;
        Check((await mock.GetFriendsAsync()).error == "network", "offline -> network error");
        Check(OnlineErrors.Message("target_shielded").Contains("shield"), "error text");
    }

    static int Main()
    {
        Run().GetAwaiter().GetResult();
        Console.WriteLine(failed == 0 ? "all " + passed + " C# checks passed" : failed + " failed, " + passed + " passed");
        return failed == 0 ? 0 : 1;
    }
}
