/*
 * Emberhold online backend: ONE Unity Cloud Code (JavaScript) script.
 *
 * Deploy name: "emberhold"  (the C# client calls CallEndpointAsync("emberhold", { action, payload })).
 * Every call carries:  action  = which operation (string)
 *                      payload = JSON text with that operation's arguments (string, may be empty)
 * Every call returns JSON text: { ok: true, ... } or { ok: false, error: "<code>" }.
 *
 * Storage (Unity Cloud Save):
 *   Player PROTECTED data (only this server can write it, so the client cannot cheat it):
 *     "city"    : { snap, savedAt, rev }              last accepted city (GameState subset)
 *     "pvp"     : { trophies, shieldUntil, lockUntil, lockBy, lastSeen, ledger[], log[], raid, suspect }
 *     "social"  : { friends[], incoming[], outgoing[] }
 *     "replays" : [ { raidId, at, attackerId, base, data } ]   newest first, small ring
 *   Player PUBLIC data:
 *     "profile" : { name, hq, trophies, code }
 *   Custom (game-wide) items:
 *     "fc_<first char of code>" : { <friendCode>: playerId }     friend code lookup
 *
 * The rules tables below copy Assets/Emberhold/Scripts/Data/BuildingCatalog.cs and UnitDef.cs.
 * When balance changes there, change RULES here too (and RaidRules.cs in the client mock).
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

// ---------------------------------------------------------------- rules

const RESOURCES = ["steel", "energy", "coins", "food", "water"];

/** defId -> { hp, maxLevel, caps (per HQ level 1..5), produces, amount, interval, raid: counts for destruction } */
const DEFS = {
  castle:       { hp: 1500, maxLevel: 5, caps: [1, 1, 1, 1, 1] },
  house:        { hp: 150, maxLevel: 3, caps: [2, 3, 4, 5, 6], produces: "coins", amount: 2, interval: 15 },
  apartment:    { hp: 300, maxLevel: 3, caps: [0, 0, 1, 2, 3], produces: "coins", amount: 5, interval: 15 },
  farm:         { hp: 80, maxLevel: 3, caps: [2, 3, 4, 4, 5], produces: "food", amount: 4, interval: 10 },
  well:         { hp: 120, maxLevel: 3, caps: [1, 2, 2, 3, 3], produces: "water", amount: 3, interval: 12 },
  canal:        { hp: 9999, maxLevel: 1, caps: [20, 30, 40, 50, 60], noRaid: true },
  solar:        { hp: 100, maxLevel: 3, caps: [1, 2, 3, 4, 5], produces: "energy", amount: 3, interval: 8 },
  mine:         { hp: 200, maxLevel: 3, caps: [1, 1, 2, 2, 3], produces: "steel", amount: 4, interval: 9 },
  powerplant:   { hp: 250, maxLevel: 3, caps: [0, 1, 1, 2, 2], produces: "energy", amount: 6, interval: 8 },
  factory:      { hp: 350, maxLevel: 3, caps: [0, 0, 1, 1, 2], produces: "steel", amount: 7, interval: 10 },
  wall:         { hp: 300, maxLevel: 1, caps: [20, 40, 60, 80, 100], noRaid: true },
  gate:         { hp: 700, maxLevel: 5, caps: [1, 1, 2, 2, 3] },
  wallgun:      { hp: 200, maxLevel: 3, caps: [0, 2, 4, 6, 8] },
  watchtower:   { hp: 250, maxLevel: 3, caps: [1, 2, 3, 4, 5] },
  ballista:     { hp: 220, maxLevel: 3, caps: [0, 1, 1, 2, 3] },
  cannon:       { hp: 400, maxLevel: 3, caps: [0, 1, 1, 2, 2] },
  dronepad:     { hp: 300, maxLevel: 3, caps: [0, 0, 0, 1, 2] },
  trap:         { hp: 9999, maxLevel: 1, caps: [0, 0, 2, 4, 6], noRaid: true },
  pen:          { hp: 300, maxLevel: 3, caps: [0, 0, 1, 1, 1] },
  barracks:     { hp: 400, maxLevel: 5, caps: [0, 1, 1, 1, 1] },
  camp:         { hp: 300, maxLevel: 5, caps: [0, 1, 1, 2, 2] },
  command:      { hp: 500, maxLevel: 1, caps: [0, 1, 1, 1, 1] },
  robotfactory: { hp: 450, maxLevel: 5, caps: [0, 0, 1, 1, 1] },
};

/** Attack units: damage per second per unit, and which GameState field holds the count. */
const UNITS = {
  rifleman: { dps: 5 / 1.0, field: "riflemen" },
  heavy:    { dps: 12 / 1.2, field: "heavies" },
  drone:    { dps: 8 / 1.0, field: "drones" },
};

const R = {
  maxBuildings: 400,
  // saving / anti-cheat on resources
  gainBoost: 2.5,            // allowed production above the city's normal rate (water bonus x1.75 is inside DEFS math, ads x2, slack)
  gainSlack: 300,            // flat allowance per resource per save (quests, dino kill rewards, rounding)
  coinsPerMinuteSlack: 20,   // dino kill rewards
  maxElapsedSec: 12 * 3600,  // longer gaps are paid at most this much
  firstSaveCap: 3000,        // the first cloud save of an existing local city is accepted up to this
  // raids
  onlineWindowMs: 3 * 60 * 1000,        // defender seen this recently = "online", cannot be raided
  raidMaxMs: (180 + 45) * 1000,         // 3 minute battle + loading / slack
  raidMinSec: 8,
  lootShare: 0.2,                       // share of each defender resource that is at stake
  lootCapByHq: [150, 300, 600, 1200, 2000],   // max loot per resource per raid, by ATTACKER HQ level
  hqBonusShare: 0.1,                    // extra share of the at-stake loot when the HQ falls
  shieldHoursAt: [[90, 16], [40, 12]],  // destruction% >= x -> shield hours
  revengeWindowMs: 48 * 3600 * 1000,
  trophiesPerStar: 10, trophiesLoss: 5, defenderLossPerStar: 6,
  heroDpsByLevel: 40,                   // Commander allowance (strike damage spread over a battle)
  hpSlackFactor: 2.5, hpSlackFlat: 1500,
  // social
  maxFriends: 50, maxIncoming: 30,
  logSize: 20, ledgerKeep: 30, replayKeep: 6, replayMaxChars: 16000,
};

// ---------------------------------------------------------------- entry point

module.exports = async ({ params, context, logger }) => {
  const api = makeStore(context);
  try {
    const payload = params.payload ? JSON.parse(params.payload) : {};
    const fn = ACTIONS[params.action];
    if (!fn) return JSON.stringify({ ok: false, error: "unknown_action" });
    const result = await fn(api, context.playerId, payload, Date.now());
    return JSON.stringify(Object.assign({ ok: true, serverNow: Date.now() }, result));
  } catch (e) {
    if (e && e.gameError) return JSON.stringify({ ok: false, error: e.gameError, serverNow: Date.now() });
    if (logger) logger.error("emberhold " + params.action + " failed: " + (e && e.message));
    return JSON.stringify({ ok: false, error: "server_error", serverNow: Date.now() });
  }
};

module.exports.params = {
  action: { type: "String", required: true },
  payload: { type: "String", required: false },
};

function fail(code) { const e = new Error(code); e.gameError = code; throw e; }

// ---------------------------------------------------------------- storage adapter

/** Thin wrapper over the Cloud Save SDK, so tests (and any SDK change) only touch this. */
function makeStore(context) {
  const admin = new DataApi({ accessToken: context.serviceToken });
  const pid = context.projectId;
  const first = (res) => (res && res.data && res.data.results && res.data.results[0]) || null;
  return {
    async getProtected(playerId, key) {
      const it = first(await admin.getProtectedItems(pid, playerId, [key]));
      return it ? { value: it.value, writeLock: it.writeLock } : { value: null, writeLock: undefined };
    },
    async setProtected(playerId, key, value, writeLock) {
      const body = { key, value };
      if (writeLock) body.writeLock = writeLock;
      await admin.setProtectedItem(pid, playerId, body);
    },
    async getPublic(playerId, key) {
      const it = first(await admin.getPublicItems(pid, playerId, [key]));
      return it ? it.value : null;
    },
    async setPublic(playerId, key, value) { await admin.setPublicItem(pid, playerId, { key, value }); },
    async getCustom(id, key) {
      const it = first(await admin.getCustomItems(pid, id, [key]));
      return it ? it.value : null;
    },
    async setCustom(id, key, value) { await admin.setCustomItem(pid, id, { key, value }); },
  };
}

function isConflict(e) {
  const s = e && (e.status || (e.response && e.response.status));
  return s === 409 || s === 412;
}

/** Read-modify-write one protected key with optimistic locking; retries on a write conflict. */
async function mutate(api, playerId, key, fn, fallback) {
  for (let attempt = 0; attempt < 5; attempt++) {
    const cur = await api.getProtected(playerId, key);
    const value = cur.value != null ? cur.value : clone(fallback);
    const out = fn(value);               // fn edits value in place; may throw fail()
    try {
      await api.setProtected(playerId, key, value, cur.writeLock);
      return out;
    } catch (e) {
      if (!isConflict(e) || attempt === 4) throw e;
    }
  }
}

const clone = (o) => JSON.parse(JSON.stringify(o));
const PVP0 = { trophies: 0, shieldUntil: 0, lockUntil: 0, lockBy: null, lastSeen: 0, ledger: [], log: [], raid: null, suspect: 0 };
const SOCIAL0 = { friends: [], incoming: [], outgoing: [] };

function newId(prefix) {
  let s = "";
  for (let i = 0; i < 12; i++) s += "abcdefghijklmnopqrstuvwxyz0123456789"[Math.floor(Math.random() * 36)];
  return prefix + s;
}

// ---------------------------------------------------------------- city helpers

function hqOf(snap) {
  const c = (snap.buildings || []).find((b) => b.defId === "castle");
  return Math.max(1, Math.min(5, c ? c.level | 0 : 1));
}

function resOf(snap) {
  const o = {};
  for (const r of RESOURCES) o[r] = Math.max(0, Math.floor(Number(snap[r]) || 0));
  return o;
}

/** Highest normal production per second of a city, per resource. */
function ratePerSec(snap) {
  const rate = { steel: 0, energy: 0, coins: 0, food: 0, water: 0 };
  for (const b of snap.buildings || []) {
    const d = DEFS[b.defId];
    if (!d || !d.produces || b.ruined) continue;
    const lvl = Math.max(1, Math.min(d.maxLevel, b.level | 0));
    rate[d.produces] += (d.amount * (1 + 0.5 * (lvl - 1)) * 1.75) / d.interval;
  }
  return rate;
}

function buildingHp(b) {
  const d = DEFS[b.defId];
  const lvl = Math.max(1, b.level | 0);
  return Math.round((d ? d.hp : 300) * (1 + 0.5 * (lvl - 1)));
}

function raidable(b) {
  const d = DEFS[b.defId];
  return !b.ruined && !(d && d.noRaid);
}

/** Structure checks. Returns a list of soft problems; throws on a city that cannot be real. */
function checkStructure(snap) {
  const list = snap.buildings;
  if (!Array.isArray(list) || list.length === 0 || list.length > R.maxBuildings) fail("invalid_city");
  if (list.filter((b) => b.defId === "castle").length !== 1) fail("invalid_city");
  const hq = hqOf(snap), counts = {}, problems = [];
  const uids = new Set();
  for (const b of list) {
    if (!b || typeof b.uid !== "string" || typeof b.defId !== "string") fail("invalid_city");
    if (uids.has(b.uid)) fail("invalid_city");
    uids.add(b.uid);
    counts[b.defId] = (counts[b.defId] || 0) + 1;
    const d = DEFS[b.defId];
    if (d && (b.level | 0) > d.maxLevel) problems.push("level:" + b.defId);
  }
  for (const id in counts) {
    const d = DEFS[id];
    if (d && counts[id] > d.caps[hq - 1]) problems.push("cap:" + id);
  }
  return problems;
}

/** Only the fields the server and raiders need: keeps stored data small and private fields out. */
function trimCity(s) {
  const out = resOf(s);
  for (const u in UNITS) out[UNITS[u].field] = Math.max(0, s[UNITS[u].field] | 0);
  out.tamedRaptors = Math.max(0, s.tamedRaptors | 0);
  out.tamedTrikes = Math.max(0, s.tamedTrikes | 0);
  out.heroLevel = Math.max(1, s.heroLevel | 0);
  out.population = Math.max(0, s.population | 0);
  out.buildings = (s.buildings || []).map((b) => ({
    uid: String(b.uid), defId: String(b.defId), x: b.x | 0, z: b.z | 0, rotation: b.rotation | 0,
    level: Math.max(1, b.level | 0), hp: b.hp | 0, ruined: !!b.ruined,
  }));
  out.full = s;   // the whole GameState, for restore on a new phone (only the owner gets it back)
  return out;
}

// ---------------------------------------------------------------- profile & friend codes

const CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O, 1/I

async function ensureProfile(api, playerId, wantedName) {
  let profile = await api.getPublic(playerId, "profile");
  let changed = false;
  if (!profile) { profile = { name: "Commander", hq: 1, trophies: 0, code: null }; changed = true; }
  if (!profile.code) {
    for (let i = 0; i < 8 && !profile.code; i++) {
      let code = "";
      for (let k = 0; k < 7; k++) code += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
      const taken = await api.getCustom("fc_" + code[0], code);
      if (!taken) { await api.setCustom("fc_" + code[0], code, playerId); profile.code = code; }
    }
    changed = true;
  }
  if (wantedName) {
    const n = cleanName(wantedName);
    if (n && n !== profile.name) { profile.name = n; changed = true; }
  }
  if (changed) await api.setPublic(playerId, "profile", profile);
  return profile;
}

function cleanName(s) {
  const n = String(s).replace(/[^\p{L}\p{N} _.-]/gu, "").trim().slice(0, 16);
  return n.length >= 2 ? n : null;
}

// ---------------------------------------------------------------- actions

const ACTIONS = {
  /** First call after sign-in: makes the profile and friend code, says if a cloud city exists. */
  async hello(api, me, p) {
    const profile = await ensureProfile(api, me, p.name);
    const city = await api.getProtected(me, "city");
    return { playerId: me, name: profile.name, code: profile.code, hasCloudCity: !!city.value,
             cloudSavedAt: city.value ? city.value.savedAt : 0, cloudRev: city.value ? city.value.rev : 0 };
  },

  async setName(api, me, p) {
    if (!cleanName(p.name || "")) fail("bad_name");
    const profile = await ensureProfile(api, me, p.name);
    return { name: profile.name };
  },

  /** New phone / reinstall: the full saved GameState. */
  async loadCity(api, me) {
    const city = (await api.getProtected(me, "city")).value;
    if (!city) return { city: null };
    return { city: JSON.stringify(city.snap.full || {}), savedAt: city.savedAt, rev: city.rev };
  },

  /**
   * Save the city (every minute and when the app pauses). Also the sync point:
   *  - raid losses/gains that happened since the last save are applied on top of the client's numbers
   *    and returned in `deltas`, so the client adds them too and shows "You were raided by ...".
   *  - resources above what the city could have produced are clamped and returned in `clamped`.
   */
  async saveCity(api, me, p, now) {
    let snap = p.city;
    if (typeof snap === "string") snap = JSON.parse(snap);
    if (!snap || typeof snap !== "object") fail("invalid_city");
    const problems = checkStructure(snap);
    const incoming = trimCity(snap);
    const hq = hqOf(incoming);

    let deltas, clamped = null, rev = 0, pvpOut = null;

    // 1) the not-yet-applied ledger entries (raid losses/gains since the last save)
    const pvpNow = (await api.getProtected(me, "pvp")).value || clone(PVP0);
    deltas = pvpNow.ledger.filter((l) => !l.applied).map((l) => clone(l));

    // 2) clamp resources against the last accepted city and store the new one
    await mutate(api, me, "city", (city) => {
      const prev = city.snap;
      const finalRes = resOf(incoming);
      for (const d of deltas) for (const r of RESOURCES) finalRes[r] = Math.max(0, finalRes[r] + ((d.res && d.res[r]) || 0));
      const base = prev ? resOf(prev) : { steel: R.firstSaveCap, energy: R.firstSaveCap, coins: R.firstSaveCap, food: R.firstSaveCap, water: R.firstSaveCap };
      // `prev` already contains every ledger delta (raids edit the stored city directly)
      const elapsed = prev ? Math.min(R.maxElapsedSec, Math.max(0, (now - city.savedAt) / 1000)) : 0;
      const rate = prev ? ratePerSec(prev) : ratePerSec(incoming);
      for (const r of RESOURCES) {
        let cap = base[r] + rate[r] * elapsed * R.gainBoost + R.gainSlack;
        if (r === "coins") cap += (elapsed / 60) * R.coinsPerMinuteSlack;
        cap = Math.floor(cap);
        if (finalRes[r] > cap) { clamped = clamped || {}; clamped[r] = cap; finalRes[r] = cap; }
      }
      Object.assign(incoming, finalRes);
      Object.assign(incoming.full, finalRes);
      city.snap = incoming;
      city.savedAt = now;
      city.rev = (city.rev || 0) + 1;
      rev = city.rev;
    }, { snap: null, savedAt: 0, rev: 0 });

    // 3) only now (city stored) mark those entries applied, and note we were online
    const appliedIds = new Set(deltas.map((d) => d.id + d.kind));
    pvpOut = await mutate(api, me, "pvp", (pvp) => {
      for (const l of pvp.ledger) if (appliedIds.has(l.id + l.kind)) l.applied = true;
      pvp.ledger = pvp.ledger.slice(-R.ledgerKeep);
      pvp.lastSeen = now;
      if (problems.length) pvp.suspect = (pvp.suspect || 0) + 1;
      if (pvp.raid && pvp.raid.expiresAt < now) pvp.raid = null;
      return { trophies: pvp.trophies, shieldUntil: pvp.shieldUntil };
    }, PVP0);

    // 4) public profile (friends list shows HQ level and trophies)
    const profile = await ensureProfile(api, me);
    if (profile.hq !== hq || profile.trophies !== pvpOut.trophies) {
      profile.hq = hq; profile.trophies = pvpOut.trophies;
      await api.setPublic(me, "profile", profile);
    }

    return { rev, deltas, clamped, clampedAny: !!clamped, trophies: pvpOut.trophies, shieldUntil: pvpOut.shieldUntil, code: profile.code };
  },

  // ------------------------------------------------ friends

  /** Friends with name, HQ, trophies, shield and online state; also the friends leaderboard (sorted). */
  async getFriends(api, me, p, now) {
    const social = (await api.getProtected(me, "social")).value || clone(SOCIAL0);
    const profile = await ensureProfile(api, me);
    const myPvp = (await api.getProtected(me, "pvp")).value || clone(PVP0);
    const rows = await Promise.all(social.friends.map(async (id) => {
      const [pr, pvp] = await Promise.all([api.getPublic(id, "profile"), api.getProtected(id, "pvp")]);
      const v = pvp.value || PVP0;
      const online = now - (v.lastSeen || 0) < R.onlineWindowMs;
      const shielded = (v.shieldUntil || 0) > now;
      const busy = (v.lockUntil || 0) > now;
      return { playerId: id, name: pr ? pr.name : "?", hq: pr ? pr.hq : 1, trophies: v.trophies || 0,
               shieldUntil: v.shieldUntil || 0, online, canRaid: !online && !shielded && !busy };
    }));
    const board = rows.concat([{ playerId: me, name: profile.name, hq: profile.hq, trophies: myPvp.trophies || 0, me: true }])
      .sort((a, b) => b.trophies - a.trophies);
    board.forEach((r, i) => (r.rank = i + 1));
    return { code: profile.code, friends: rows, leaderboard: board, incoming: social.incoming, outgoing: social.outgoing };
  },

  async addFriend(api, me, p, now) {
    const code = String(p.code || "").toUpperCase().replace(/[^A-Z0-9]/g, "");
    if (code.length !== 7) fail("bad_code");
    const other = await api.getCustom("fc_" + code[0], code);
    if (!other) fail("code_not_found");
    if (other === me) fail("own_code");
    const myProfile = await ensureProfile(api, me);
    let autoAccepted = false;
    await mutate(api, me, "social", (s) => {
      if (s.friends.includes(other)) fail("already_friends");
      if (s.friends.length >= R.maxFriends) fail("too_many_friends");
      if (s.incoming.some((i) => i.playerId === other)) {          // they already asked me: become friends
        s.incoming = s.incoming.filter((i) => i.playerId !== other);
        s.friends.push(other); autoAccepted = true;
      } else if (!s.outgoing.includes(other)) s.outgoing.push(other);
    }, SOCIAL0);
    await mutate(api, other, "social", (s) => {
      if (autoAccepted) {
        s.outgoing = s.outgoing.filter((x) => x !== me);
        if (!s.friends.includes(me)) s.friends.push(me);
      } else if (!s.incoming.some((i) => i.playerId === me) && !s.friends.includes(me)) {
        s.incoming.push({ playerId: me, name: myProfile.name, at: now });
        s.incoming = s.incoming.slice(-R.maxIncoming);
      }
    }, SOCIAL0);
    return { friends: autoAccepted };
  },

  async respondFriend(api, me, p) {
    const other = String(p.playerId || "");
    let accepted = false;
    await mutate(api, me, "social", (s) => {
      if (!s.incoming.some((i) => i.playerId === other)) fail("no_request");
      s.incoming = s.incoming.filter((i) => i.playerId !== other);
      if (p.accept) {
        if (s.friends.length >= R.maxFriends) fail("too_many_friends");
        if (!s.friends.includes(other)) s.friends.push(other);
        accepted = true;
      }
    }, SOCIAL0);
    await mutate(api, other, "social", (s) => {
      s.outgoing = s.outgoing.filter((x) => x !== me);
      if (accepted && !s.friends.includes(me)) s.friends.push(me);
    }, SOCIAL0);
    return { accepted };
  },

  async removeFriend(api, me, p) {
    const other = String(p.playerId || "");
    await mutate(api, me, "social", (s) => { s.friends = s.friends.filter((x) => x !== other); s.outgoing = s.outgoing.filter((x) => x !== other); }, SOCIAL0);
    await mutate(api, other, "social", (s) => { s.friends = s.friends.filter((x) => x !== me); s.incoming = s.incoming.filter((i) => i.playerId !== me); }, SOCIAL0);
    return {};
  },

  // ------------------------------------------------ raids

  /**
   * Start a raid on an offline friend (or revenge on whoever raided you).
   * Locks the defender for the battle, removes the attacker's own shield, and returns the defender's
   * base plus the most loot this raid can give. The battle then runs on the attacker's phone.
   */
  async startRaid(api, me, p, now) {
    const target = String(p.defenderId || "");
    if (!target || target === me) fail("bad_target");
    const social = (await api.getProtected(me, "social")).value || SOCIAL0;
    const myPvp = (await api.getProtected(me, "pvp")).value || PVP0;
    let revenge = null;
    if (p.revengeOf) {
      revenge = (myPvp.log || []).find((l) => l.raidId === p.revengeOf && l.role === "defender");
      if (!revenge || revenge.otherId !== target) fail("bad_revenge");
      if (revenge.revenged) fail("revenge_used");
      if (now - revenge.at > R.revengeWindowMs) fail("revenge_expired");
    } else if (!social.friends.includes(target)) fail("not_friends");
    if (myPvp.raid && myPvp.raid.expiresAt > now) fail("raid_in_progress");

    const myCity = (await api.getProtected(me, "city")).value;
    if (!myCity) fail("save_first");
    const army = {};
    for (const u in UNITS) army[u] = myCity.snap[UNITS[u].field] | 0;

    const theirCity = (await api.getProtected(target, "city")).value;
    if (!theirCity) fail("target_has_no_city");

    const raidId = newId("r_");
    // lock the defender (fails if online, shielded or already under attack)
    await mutate(api, target, "pvp", (pvp) => {
      if (now - (pvp.lastSeen || 0) < R.onlineWindowMs) fail("target_online");
      if ((pvp.shieldUntil || 0) > now) fail("target_shielded");
      if ((pvp.lockUntil || 0) > now && pvp.lockBy !== raidId) fail("target_busy");
      pvp.lockUntil = now + R.raidMaxMs; pvp.lockBy = raidId;
    }, PVP0);

    const snap = theirCity.snap;
    const myHq = hqOf(myCity.snap);
    const atStake = {};
    for (const r of RESOURCES) atStake[r] = Math.min(Math.floor(snap[r] * R.lootShare), R.lootCapByHq[myHq - 1]);

    const base = {
      buildings: snap.buildings, tamedRaptors: snap.tamedRaptors, tamedTrikes: snap.tamedTrikes,
      heroLevel: snap.heroLevel, riflemen: snap.riflemen, heavies: snap.heavies, drones: snap.drones,
    };
    const seed = Math.floor(Math.random() * 2147483647);
    await mutate(api, me, "pvp", (pvp) => {
      pvp.shieldUntil = 0;        // attacking ends your own shield
      pvp.raid = { raidId, defenderId: target, startedAt: now, expiresAt: now + R.raidMaxMs, atStake, army,
                   heroLevel: myCity.snap.heroLevel || 1, revengeOf: p.revengeOf || null, seed,
                   base: base.buildings.map((b) => [b.uid, b.defId, b.level, b.ruined ? 1 : 0, b.x, b.z, b.rotation]) };
    }, PVP0);

    const profile = await api.getPublic(target, "profile");
    return { raidId, defenderId: target, defenderName: profile ? profile.name : "?", base: JSON.stringify(base),
             atStake, army, seed, expiresAt: now + R.raidMaxMs };
  },

  /**
   * End a raid. The client sends WHAT happened (buildings destroyed, troops used, time, replay);
   * the server decides destruction %, stars, loot, trophies and shield. The client never sends loot.
   */
  async finishRaid(api, me, p, now) {
    const raidId = String(p.raidId || "");
    let raid = null;
    await mutate(api, me, "pvp", (pvp) => {
      if (!pvp.raid || pvp.raid.raidId !== raidId) fail("no_raid");
      raid = pvp.raid; pvp.raid = null;
    }, PVP0);

    const elapsedSec = (now - raid.startedAt) / 1000;
    const late = now > raid.expiresAt;
    const baseRows = raid.base.map((t) => ({ uid: t[0], defId: t[1], level: t[2], ruined: !!t[3], x: t[4], z: t[5], rotation: t[6] }));
    const byUid = new Map(baseRows.map((b) => [b.uid, b]));
    const targets = baseRows.filter(raidable);

    // troops: cannot use more than you own
    const used = {};
    let dps = 0;
    for (const u in UNITS) {
      used[u] = Math.max(0, Math.min(raid.army[u] | 0, (p.troopsUsed && p.troopsUsed[u]) | 0));
      dps += used[u] * UNITS[u].dps;
    }
    if (p.heroUsed) dps += R.heroDpsByLevel * (raid.heroLevel || 1);

    // destroyed: real raidable buildings of that base, once each, and only as much HP as the army could deal
    const seen = new Set();
    let destroyed = [];
    for (const uid of Array.isArray(p.destroyed) ? p.destroyed : []) {
      const b = byUid.get(uid);
      if (b && raidable(b) && !seen.has(uid)) { seen.add(uid); destroyed.push(b); }
    }
    const battleSec = Math.min(Math.max(0, Number(p.durationSec) || 0), elapsedSec, 180);
    const hpBudget = dps * battleSec * R.hpSlackFactor + (dps > 0 ? R.hpSlackFlat : 0);
    let hpSum = 0, rejected = false;
    destroyed = destroyed.filter((b) => { hpSum += buildingHp(b); if (hpSum > hpBudget) { rejected = true; return false; } return true; });
    if (late || elapsedSec < R.raidMinSec) { destroyed = []; rejected = rejected || late; }

    const pct = targets.length ? Math.round((destroyed.length / targets.length) * 100) : 0;
    const hqDown = destroyed.some((b) => b.defId === "castle");
    const stars = (pct >= 50 ? 1 : 0) + (hqDown ? 1 : 0) + (pct >= 100 ? 1 : 0);

    // loot from the defender's CURRENT numbers (they may have spent since the raid started)
    const destroyedUids = new Set(destroyed.map((b) => b.uid));
    const loot = {};
    await mutate(api, raid.defenderId, "city", (city) => {
      for (const r of RESOURCES) {
        let share = (raid.atStake[r] * pct) / 100;
        if (hqDown) share += raid.atStake[r] * R.hqBonusShare;
        loot[r] = Math.max(0, Math.min(Math.floor(share), raid.atStake[r], city.snap ? city.snap[r] : 0));
        if (city.snap) { city.snap[r] -= loot[r]; if (city.snap.full) city.snap.full[r] = city.snap[r]; }
      }
      // destroyed buildings become ruins in the defender's city (they repair them)
      if (city.snap) {
        for (const b of city.snap.buildings) if (destroyedUids.has(b.uid) && b.defId !== "castle") b.ruined = true;
        if (city.snap.full && Array.isArray(city.snap.full.buildings))
          for (const b of city.snap.full.buildings) if (destroyedUids.has(b.uid) && b.defId !== "castle") b.ruined = true;
      }
    }, { snap: null, savedAt: 0, rev: 0 });

    let shieldHours = 0;
    for (const [atPct, h] of R.shieldHoursAt) if (pct >= atPct) { shieldHours = h; break; }
    const won = stars > 0;
    const attackerTrophies = won ? stars * R.trophiesPerStar : -R.trophiesLoss;
    const defenderTrophies = won ? -stars * R.defenderLossPerStar : R.trophiesLoss;

    const [attProfile, defProfile] = await Promise.all([api.getPublic(me, "profile"), api.getPublic(raid.defenderId, "profile")]);
    const entry = { raidId, at: now, stars, pct, loot, hqDown, revenge: !!raid.revengeOf };

    // defender: losses in the ledger, shield, unlock, log entry, replay
    await mutate(api, raid.defenderId, "pvp", (pvp) => {
      const neg = {}; for (const r of RESOURCES) neg[r] = -loot[r];
      pvp.ledger.push({ id: raidId, kind: "raid_loss", res: neg, at: now, applied: false,
                        info: { by: attProfile ? attProfile.name : "?", stars, pct, ruined: destroyed.filter((b) => b.defId !== "castle").map((b) => b.uid) } });
      pvp.ledger = pvp.ledger.slice(-R.ledgerKeep);
      if (shieldHours > 0) pvp.shieldUntil = Math.max(pvp.shieldUntil || 0, now + shieldHours * 3600 * 1000);
      if (pvp.lockBy === raidId) { pvp.lockUntil = 0; pvp.lockBy = null; }
      pvp.trophies = Math.max(0, (pvp.trophies || 0) + defenderTrophies);
      pvp.log.unshift(Object.assign({ role: "defender", otherId: me, otherName: attProfile ? attProfile.name : "?", trophies: defenderTrophies, revenged: false }, entry));
      pvp.log = pvp.log.slice(0, R.logSize);
    }, PVP0);

    // attacker: loot in the ledger (applied at next save), trophies, log; mark revenge used
    await mutate(api, me, "pvp", (pvp) => {
      pvp.ledger.push({ id: raidId, kind: "raid_gain", res: loot, at: now, applied: false, info: { from: defProfile ? defProfile.name : "?", stars, pct } });
      pvp.ledger = pvp.ledger.slice(-R.ledgerKeep);
      pvp.trophies = Math.max(0, (pvp.trophies || 0) + attackerTrophies);
      pvp.log.unshift(Object.assign({ role: "attacker", otherId: raid.defenderId, otherName: defProfile ? defProfile.name : "?", trophies: attackerTrophies }, entry));
      pvp.log = pvp.log.slice(0, R.logSize);
      if (raid.revengeOf) { const l = pvp.log.find((x) => x.raidId === raid.revengeOf); if (l) l.revenged = true; }
    }, PVP0);

    // attacker's stored city: + loot (so the next save is not clamped), - troops used (no reuse)
    await mutate(api, me, "city", (city) => {
      if (!city.snap) return;
      for (const r of RESOURCES) { city.snap[r] += loot[r]; if (city.snap.full) city.snap.full[r] = city.snap[r]; }
      for (const u in UNITS) {
        const f = UNITS[u].field;
        city.snap[f] = Math.max(0, (city.snap[f] | 0) - used[u]);
        if (city.snap.full) city.snap.full[f] = city.snap[f];
      }
    }, { snap: null, savedAt: 0, rev: 0 });

    // replay for the defender (and the attacker can open it too)
    const data = typeof p.replay === "string" && p.replay.length <= R.replayMaxChars ? p.replay : "";
    if (data) {
      await mutate(api, raid.defenderId, "replays", (list) => {
        list.unshift({ raidId, at: now, attackerId: me, defenderId: raid.defenderId, seed: raid.seed, base: raid.base, data });
        list.length = Math.min(list.length, R.replayKeep);
      }, []);
    }

    return { raidId, stars, destructionPct: pct, hqDown, loot, trophies: attackerTrophies, shieldHours, rejected,
             troopsLost: used };
  },

  /** Raids you made and raids against you (newest first), with revenge availability. */
  async getRaidLog(api, me, p, now) {
    const pvp = (await api.getProtected(me, "pvp")).value || PVP0;
    const entries = (pvp.log || []).map((l) => Object.assign({}, l, {
      canRevenge: l.role === "defender" && !l.revenged && now - l.at < R.revengeWindowMs,
    }));
    return { entries, trophies: pvp.trophies || 0, shieldUntil: pvp.shieldUntil || 0 };
  },

  async getReplay(api, me, p) {
    const raidId = String(p.raidId || "");
    const pvp = (await api.getProtected(me, "pvp")).value || PVP0;
    const l = (pvp.log || []).find((x) => x.raidId === raidId);
    if (!l) fail("no_replay");
    const defenderId = l.role === "defender" ? me : l.otherId;
    const list = (await api.getProtected(defenderId, "replays")).value || [];
    const r = list.find((x) => x.raidId === raidId);
    if (!r) fail("no_replay");
    return { raidId, seed: r.seed, data: r.data, at: r.at, attackerId: r.attackerId, defenderId: r.defenderId,
             base: JSON.stringify(r.base.map((t) => ({ uid: t[0], defId: t[1], level: t[2], ruined: !!t[3], x: t[4], z: t[5], rotation: t[6] }))) };
  },
};

// exposed for the local Node tests only (Cloud Code ignores extra properties)
module.exports._test = { ACTIONS, R, DEFS, ratePerSec, checkStructure };
