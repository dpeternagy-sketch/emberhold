# Emberhold online: cloud save, friends, friend raids

Everything online for milestone M1-3: guest accounts, cloud save of the city, server time for offline
production, friends by code, a friends leaderboard, async raids on an offline friend's base, shields,
revenge and replays. The server judges every raid, so the client can never send "give me 5000 steel".

## Why Unity Gaming Services (UGS) and not Firebase

| | UGS | Firebase |
|---|---|---|
| Server code (needed so loot cannot be cheated) | Cloud Code, **free**: 1M calls + 20 compute hours a month | Cloud Functions need the paid **Blaze** plan with a card |
| Account | the Unity account Peter already uses in the Editor | a new Google project |
| SDK in Unity | small official packages, no native iOS/Android plugins | large native SDK (CocoaPods / Gradle setup) |
| Free storage | Cloud Save: 5 GiB, 1M reads and 1M writes a month | Firestore: 1 GiB, 50k reads / 20k writes a **day** |
| Later | Leaderboards, Remote Config, Friends, Lobby, IAP, Ads in the same dashboard | |

Source for the UGS numbers: https://unity.com/products/gaming-services/pricing (checked 2026-11-22).
One raid costs about 12 Cloud Save reads/writes, one city save about 6. With saves every 60 seconds
while playing, the free tier covers roughly 1,500 to 2,500 daily players before any cost.

## The pieces

```
Server/cloudcode/emberhold.js     the whole backend: ONE Cloud Code script, deployed as "emberhold"
Server/test/run-tests.js          13 Node tests with a fake Cloud Save   ->  node Server/test/run-tests.js
Server/test/csharp/run.sh         36 C# checks of the client layer without Unity (needs mono)

Assets/Emberhold/Scripts/Online/
  IOnlineService.cs     the interface + OnlineServices.Current + ServerClock + OnlineErrors (toast texts)
  OnlineModels.cs       reply / request classes (JsonUtility)
  MockOnlineService.cs  works with NO server: fake friends Nour / Karim / Lina, request from Yara, code SAMI777
  UgsOnlineService.cs   the real one; compiled only with the scripting define EMBERHOLD_UGS
  CloudSaveSync.cs      uploads the city every 60 s, applies raid losses / loot / ruins / clamps, restore advice
  RaidRules.cs          loot, stars, shield, trophy rules (copy of the server's, for the mock and the UI)
  ReplayRecorder.cs     compact replay text: drops, Commander strikes, buildings falling
```

No existing file is changed. Without the define, the game uses the mock and needs no packages.

## Data on the server (Cloud Save)

Protected player data can only be written by the server, so the client cannot edit trophies, shields or loot.

| Where | Key | What |
|---|---|---|
| player, protected | `city` | last accepted city (resources, army, buildings) + the full GameState for restore |
| player, protected | `pvp` | trophies, shield end, raid lock, last seen, ledger (losses/loot not yet applied), raid log (20), the raid in progress |
| player, protected | `social` | friends (max 50), incoming requests, outgoing requests |
| player, protected | `replays` | last 6 raids against this player (base + replay text) |
| player, public | `profile` | name, HQ level, trophies, friend code |
| custom item `fc_<letter>` | `<code>` | friend code -> player id |

## Rules the server enforces

- **City save:** one HQ, max 400 buildings, unique ids. Each resource can grow at most
  (city production rate x 2.5 x time since last save) + 300 (+ coins for dino kills); more is lowered and
  the client is told (`clamped`). Spending is always fine. Over-cap buildings are counted as "suspect", not blocked.
- **Who can be raided:** only friends (or the player you take revenge on), not online in the last 3 minutes,
  no shield, nobody else attacking. Attacking removes your own shield.
- **Loot:** 20% of each of the defender's resources is at stake, capped by the attacker's HQ level
  (150 / 300 / 600 / 1200 / 2000). Loot = at stake x destruction %, +10% if the HQ falls.
  The defender loses exactly what the attacker gets.
- **Battle report check:** only real buildings of that base count (walls, canals, traps do not);
  troops are capped by the attacker's saved army; the destroyed HP must fit what that army can deal in
  that time (x2.5 slack); under 8 seconds or over 3 min 45 s from start gives nothing.
- **Stars:** 50% destruction, HQ destroyed, 100%. **Shield:** 12 h at 40%+, 16 h at 90%+.
  **Trophies:** attacker +10 per star (or -5 for 0 stars), defender -6 per star (or +5).
- **Revenge:** once per raid against you, within 48 hours. **Replay:** up to 16,000 characters.
- Troops used are removed from the attacker's saved army; destroyed buildings become ruins in the defender's city.

## How it fits the game (for the Unity developer)

1. **Start** (after the local save is loaded): `sync = new CloudSaveSync(OnlineServices.Current); await sync.ConnectAsync(name);`
   If `sync.Advice == CloudIsNewer`, ask "Load your city from the cloud?" -> `await sync.LoadCloudCityAsync()`, else `sync.KeepLocal()`.
2. **Every frame:** `sync.Tick(State, Time.unscaledDeltaTime)`. In `OnApplicationPause(true)`: `_ = sync.PushAsync(State)`.
3. **Events:** `Raided` (toast "Nour raided you: -60 steel", show the raid log button), `BuildingRuined(uid)`
   (find the Building with that uid, `SetRuined(true)`), `LootArrived` (toast), `Clamped` (just log).
4. **Timers:** use `ServerClock.UtcNow` instead of `DateTime.UtcNow` for offline production, upgrades and training.
5. **Friends screen:** `GetFriendsAsync()` -> `friends` (Raid button when `canRaid`), `incoming` (Accept / Decline),
   `code` (show "Your code: K7M2QPA", Share button), `leaderboard` (already sorted, `me` = you).
6. **Raid:** `StartRaidAsync(friendId)` -> build the battle from `reply.Base` (same `BuildingData` as the city),
   seed every random choice with `reply.seed`, let the player drop only the troops in `reply.army`,
   record with `ReplayRecorder`. At the end (or when they leave): `FinishRaidAsync(new RaidReport { raidId, destroyed, durationSec, troopsUsed, heroUsed, replay })`.
   Show `stars`, `destructionPct`, `loot` (arrives at the next save), remove `troopsLost` from the army, then `sync.PushSoon()`.
7. **Raid log:** `GetRaidLogAsync()` -> entries with `canRevenge` (Revenge = `StartRaidAsync(otherId, raidId)`)
   and Replay (`GetReplayAsync(raidId)` -> `Buildings` + `ReplayRecorder.Parse(data)`).
8. **Show errors** with `OnlineErrors.Message(reply.error)`.

Testing with the mock: `((MockOnlineService)OnlineServices.Current).SimulateRaidOnMe()` makes Nour raid you;
the loss, ruins, revenge and replay appear at the next save. `SimulateOffline = true` tests "No connection".

## Turning on the real server

Done by the Unity developer in the Editor on Peter's Mac, signed in with Peter's Unity account:

1. **Edit > Project Settings > Services**: link the project to a Unity Cloud project (create one named Emberhold).
2. **Package Manager**: add `com.unity.services.authentication`, `com.unity.services.cloudcode`, `com.unity.services.deployment`.
3. In the Unity Cloud dashboard (cloud.unity.com) for that project: open **Cloud Code** and **Cloud Save**
   once so they are switched on, and turn on **Anonymous** sign-in under **Player Authentication > Identity providers**.
4. Copy `Server/cloudcode/emberhold.js` into the Unity project (for example `Assets/Emberhold/CloudCode/emberhold.js`),
   open **Window > Deployment**, and deploy it. Its name must stay `emberhold`.
5. **Project Settings > Player > Scripting Define Symbols**: add `EMBERHOLD_UGS`.

What only Peter can do: nothing extra if the Unity Cloud dashboard accepts his account as it is. If the
dashboard asks to accept the Unity Gaming Services terms (one click) or for billing details, that has to be him.

**Later (before launch):** guest accounts live on one phone. Linking Apple / Google sign-in
(`AuthenticationService.Instance.LinkWithAppleAsync` / `LinkWithGoogleAsync`) keeps the city across phones
and reinstalls; it needs Apple and Google developer accounts, which come with the phone release (M6).

## Changing balance

The rules tables at the top of `emberhold.js` copy `BuildingCatalog.cs` and `UnitDef.cs`.
When building HP, production, caps or unit damage change there, change `DEFS` / `UNITS` in the script too,
run `node Server/test/run-tests.js`, and deploy again. Raid numbers live in `R` and in `RaidRules.cs`.
