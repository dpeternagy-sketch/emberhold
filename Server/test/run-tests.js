/*
 * Local tests for Server/cloudcode/emberhold.js with an in-memory fake of the Cloud Save SDK.
 * Run:  node Server/test/run-tests.js      (Node 18+, no packages needed)
 */
const Module = require("module");
const path = require("path");
const assert = require("assert");

// ---------------- fake @unity-services/cloud-save-1.4 (same call shapes, keeps writeLocks)
const db = { protected: {}, public: {}, custom: {} };
let lockSeq = 1;
function table(kind, owner) { return (db[kind][owner] = db[kind][owner] || {}); }
function get(kind, owner, keys) {
  const t = table(kind, owner);
  return { data: { results: keys.filter((k) => k in t).map((k) => ({ key: k, value: JSON.parse(JSON.stringify(t[k].value)), writeLock: t[k].writeLock })) } };
}
function set(kind, owner, body) {
  const t = table(kind, owner);
  if (body.writeLock !== undefined && t[body.key] && t[body.key].writeLock !== body.writeLock) {
    const e = new Error("conflict"); e.response = { status: 409 }; throw e;
  }
  if (body.writeLock !== undefined && !t[body.key]) { const e = new Error("conflict"); e.response = { status: 409 }; throw e; }
  t[body.key] = { value: JSON.parse(JSON.stringify(body.value)), writeLock: "w" + lockSeq++ };
  return { data: { writeLock: t[body.key].writeLock } };
}
class DataApi {
  async getProtectedItems(p, id, keys) { return get("protected", id, keys); }
  async setProtectedItem(p, id, body) { return set("protected", id, body); }
  async getPublicItems(p, id, keys) { return get("public", id, keys); }
  async setPublicItem(p, id, body) { return set("public", id, body); }
  async getCustomItems(p, id, keys) { return get("custom", id, keys); }
  async setCustomItem(p, id, body) { return set("custom", id, body); }
}
const realLoad = Module._load;
Module._load = function (req, parent, isMain) {
  if (req === "@unity-services/cloud-save-1.4") return { DataApi };
  return realLoad.apply(this, arguments);
};
const script = require(path.join(__dirname, "../cloudcode/emberhold.js"));

// ---------------- helpers
let clock = Date.UTC(2026, 10, 22, 12, 0, 0);
Date.now = () => clock;
const advance = (sec) => { clock += sec * 1000; };

async function call(playerId, action, payload) {
  const out = await script({ params: { action, payload: payload === undefined ? "" : JSON.stringify(payload) },
                             context: { playerId, projectId: "proj", serviceToken: "svc" },
                             logger: { error: (m) => console.error("   [log]", m) } });
  return JSON.parse(out);
}

function city(opts = {}) {
  const b = [
    { uid: "hq", defId: "castle", x: 0, z: 0, level: opts.hq || 2 },
    { uid: "h1", defId: "house", x: 5, z: 0, level: 1 },
    { uid: "h2", defId: "house", x: 7, z: 0, level: 1 },
    { uid: "f1", defId: "farm", x: 9, z: 0, level: 1 },
    { uid: "m1", defId: "mine", x: 0, z: 6, level: 1 },
    { uid: "s1", defId: "solar", x: 3, z: 6, level: 1 },
    { uid: "g1", defId: "watchtower", x: 6, z: 6, level: 1 },
    { uid: "w1", defId: "wall", x: 1, z: 9, level: 1 },
    { uid: "w2", defId: "wall", x: 2, z: 9, level: 1 },
  ];
  return Object.assign({ version: 3, steel: 1000, energy: 500, coins: 800, food: 600, water: 400, population: 8,
                         riflemen: 20, heavies: 2, drones: 0, heroLevel: 1, buildings: b }, opts.extra || {});
}

const tests = [];
const test = (name, fn) => tests.push({ name, fn });

// ---------------- tests
let codeA, codeB;

test("hello makes a profile with a 7-char friend code", async () => {
  const a = await call("A", "hello", { name: "Peter" });
  assert.ok(a.ok); assert.strictEqual(a.code.length, 7); assert.strictEqual(a.name, "Peter"); assert.strictEqual(a.hasCloudCity, false);
  codeA = a.code;
  const b = await call("B", "hello", { name: "Omar" }); codeB = b.code;
  assert.notStrictEqual(codeA, codeB);
  const again = await call("A", "hello", {});
  assert.strictEqual(again.code, codeA, "code is stable");
});

test("first save accepted, loadCity gives the full GameState back", async () => {
  const s = await call("A", "saveCity", { city: city() });
  assert.ok(s.ok, JSON.stringify(s)); assert.strictEqual(s.clamped, null); assert.strictEqual(s.rev, 1);
  const l = await call("A", "loadCity");
  const back = JSON.parse(l.city);
  assert.strictEqual(back.steel, 1000); assert.strictEqual(back.buildings.length, 9);
  assert.ok((await call("B", "saveCity", { city: city({ hq: 3 }) })).ok);
});

test("a city without an HQ is refused", async () => {
  const bad = city(); bad.buildings = bad.buildings.filter((b) => b.defId !== "castle");
  const r = await call("A", "saveCity", { city: bad });
  assert.strictEqual(r.error, "invalid_city");
});

test("resources above what the city can produce are clamped", async () => {
  advance(60);
  const r = await call("A", "saveCity", { city: city({ extra: { steel: 999999 } }) });
  assert.ok(r.ok); assert.ok(r.clamped && r.clamped.steel < 2000, JSON.stringify(r.clamped));
  assert.ok(!r.clamped.coins, "normal numbers are not touched");
});

test("friends: add by code, accept, both see each other + leaderboard", async () => {
  assert.strictEqual((await call("A", "addFriend", { code: "XXXXXXX" })).error, "code_not_found");
  assert.strictEqual((await call("A", "addFriend", { code: codeA })).error, "own_code");
  const add = await call("A", "addFriend", { code: codeB.toLowerCase() });
  assert.ok(add.ok); assert.strictEqual(add.friends, false);
  const fb = await call("B", "getFriends");
  assert.strictEqual(fb.incoming.length, 1); assert.strictEqual(fb.incoming[0].name, "Peter");
  assert.ok((await call("B", "respondFriend", { playerId: "A", accept: true })).accepted);
  const fa = await call("A", "getFriends");
  assert.strictEqual(fa.friends.length, 1); assert.strictEqual(fa.friends[0].name, "Omar"); assert.strictEqual(fa.outgoing.length, 0);
  assert.strictEqual(fa.leaderboard.length, 2);
  assert.strictEqual((await call("A", "addFriend", { code: codeB })).error, "already_friends");
});

test("cannot raid a friend who is online, or a stranger", async () => {
  assert.strictEqual((await call("A", "startRaid", { defenderId: "B" })).error, "target_online");
  await call("C", "hello", { name: "Stranger" }); await call("C", "saveCity", { city: city() });
  advance(600);
  assert.strictEqual((await call("A", "startRaid", { defenderId: "C" })).error, "not_friends");
});

let raid1;
test("raid an offline friend: server computes stars and loot, defender gets shield", async () => {
  advance(10 * 60);   // B is offline now
  const start = await call("A", "startRaid", { defenderId: "B" });
  assert.ok(start.ok, JSON.stringify(start));
  const base = JSON.parse(start.base);
  assert.strictEqual(base.buildings.length, 9);
  assert.strictEqual(start.atStake.steel, 200, "20% of 1000, under the HQ 2 cap of 300");
  assert.strictEqual((await call("D", "startRaid", { defenderId: "B" })).error, "not_friends");
  advance(90);
  // destroy all 7 raidable buildings (walls do not count); a fake uid and the walls are ignored
  const fin = await call("A", "finishRaid", { raidId: start.raidId, destroyed: ["hq", "h1", "h2", "f1", "m1", "s1", "g1", "w1", "nope"],
                                             durationSec: 85, troopsUsed: { rifleman: 20, heavy: 2 }, heroUsed: true, replay: "seed;deploy..." });
  assert.ok(fin.ok, JSON.stringify(fin));
  assert.strictEqual(fin.destructionPct, 100); assert.strictEqual(fin.stars, 3); assert.strictEqual(fin.rejected, false);
  assert.strictEqual(fin.loot.steel, 200); assert.strictEqual(fin.shieldHours, 16);
  raid1 = start.raidId;
  // B is now shielded
  assert.strictEqual((await call("A", "startRaid", { defenderId: "B" })).error, "target_shielded");
});

test("loot reaches both players at their next save, with the raid info", async () => {
  advance(30);
  const a = await call("A", "saveCity", { city: city() });
  const gain = a.deltas.find((d) => d.kind === "raid_gain");
  assert.ok(gain && gain.res.steel === 200, JSON.stringify(a.deltas));
  assert.strictEqual(a.clamped, null, "the loot does not trigger the clamp");
  const b = await call("B", "saveCity", { city: city({ hq: 3 }) });
  const loss = b.deltas.find((d) => d.kind === "raid_loss");
  assert.ok(loss && loss.res.steel === -200 && loss.info.by === "Peter");
  assert.ok(loss.info.ruined.includes("h1") && !loss.info.ruined.includes("hq"));
  const again = await call("B", "saveCity", { city: city({ hq: 3 }) });
  assert.strictEqual(again.deltas.length, 0, "deltas are given only once");
});

test("cheat: claiming destruction the army could not do is cut down", async () => {
  // C befriends A; A raids C with 1 rifleman and claims everything in 10 seconds
  await call("C", "addFriend", { code: codeA }); await call("A", "respondFriend", { playerId: "C", accept: true });
  const ac = city(); ac.riflemen = 1; ac.heavies = 0;
  await call("A", "saveCity", { city: ac });
  advance(600);
  const s = await call("A", "startRaid", { defenderId: "C" });
  assert.ok(s.ok, JSON.stringify(s));
  advance(12);
  const f = await call("A", "finishRaid", { raidId: s.raidId, destroyed: ["hq", "h1", "h2", "f1", "m1", "s1", "g1"], durationSec: 10, troopsUsed: { rifleman: 50 } });
  assert.ok(f.rejected); assert.ok(f.destructionPct < 50, "pct " + f.destructionPct); assert.strictEqual(f.troopsLost.rifleman, 1);
});

test("cheat: a raid finished too late gives nothing; finishing twice fails", async () => {
  await call("B", "saveCity", { city: city({ hq: 3 }) });
  // remove B's shield for the test by waiting it out
  advance(17 * 3600);
  await call("A", "saveCity", { city: city() }); advance(600);
  const s = await call("A", "startRaid", { defenderId: "B" });
  assert.ok(s.ok, JSON.stringify(s));
  advance(400);
  const f = await call("A", "finishRaid", { raidId: s.raidId, destroyed: ["h1"], durationSec: 60, troopsUsed: { rifleman: 20 } });
  assert.strictEqual(f.destructionPct, 0); assert.strictEqual(f.loot.steel, 0);
  assert.strictEqual((await call("A", "finishRaid", { raidId: s.raidId, destroyed: [] })).error, "no_raid");
});

test("raid log, revenge once, and replay", async () => {
  const log = await call("B", "getRaidLog");
  const first = log.entries.find((e) => e.raidId === raid1);
  assert.ok(first && first.role === "defender" && first.canRevenge);
  const rep = await call("B", "getReplay", { raidId: raid1 });
  assert.strictEqual(rep.data, "seed;deploy..."); assert.strictEqual(JSON.parse(rep.base).length, 9);
  assert.ok((await call("A", "getReplay", { raidId: raid1 })).ok, "attacker can watch too");
  // revenge: A must be offline and unshielded
  advance(600);
  const r = await call("B", "startRaid", { defenderId: "A", revengeOf: raid1 });
  assert.ok(r.ok, JSON.stringify(r));
  advance(30);
  await call("B", "finishRaid", { raidId: r.raidId, destroyed: ["h1"], durationSec: 25, troopsUsed: { rifleman: 20 } });
  const log2 = await call("B", "getRaidLog");
  assert.strictEqual(log2.entries.find((e) => e.raidId === raid1).canRevenge, false);
  advance(600);
  assert.strictEqual((await call("B", "startRaid", { defenderId: "A", revengeOf: raid1 })).error, "revenge_used");
});

test("remove friend works both ways", async () => {
  await call("A", "removeFriend", { playerId: "C" });
  assert.ok(!(await call("C", "getFriends")).friends.some((f) => f.playerId === "A"));
});

test("unknown action and bad JSON are handled", async () => {
  assert.strictEqual((await call("A", "nope")).error, "unknown_action");
  const raw = JSON.parse(await script({ params: { action: "hello", payload: "{bad" }, context: { playerId: "A" }, logger: { error() {} } }));
  assert.strictEqual(raw.error, "server_error");
});

(async () => {
  let failed = 0;
  for (const t of tests) {
    try { await t.fn(); console.log("  ok   " + t.name); }
    catch (e) { failed++; console.log("  FAIL " + t.name + "\n       " + (e && e.message)); }
  }
  console.log(failed ? `\n${failed} of ${tests.length} failed` : `\nall ${tests.length} passed`);
  process.exit(failed ? 1 : 0);
})();
