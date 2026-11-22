using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Owns the game state and the rules of the city: placing buildings, workers, food,
    /// production, nights and settlers. Visual/UI code talks to the game through this class.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public GridMap Grid { get; private set; }
        public WorldBuilder World { get; private set; }
        public DayNightCycle Cycle { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public Building Castle { get; private set; }
        public RaidDirector Raids { get; set; }

        /// <summary>Castle grid origin: at the back of the valley, against the mountain.</summary>
        public const int CastleX = -2, CastleZ = GridMap.MountainZ - 8;
        public static readonly Vector3 CastleCenter = new Vector3(CastleX + 2f, 0f, CastleZ + 2f);

        public int CastleLevel { get { return Castle != null ? Castle.Data.level : 1; } }
        public int BuildRadius { get { return 9 + 3 * CastleLevel; } }

        /// <summary>Builders: how many upgrades can run at the same time. 1 at the start, a 2nd at HQ level 3
        /// (State.extraBuilders is for a later VIP/shop bonus: it only saves time, never unlocks power).</summary>
        public const int SecondBuilderHQLevel = 3;
        public int BuilderSlots { get { return 1 + (CastleLevel >= SecondBuilderHQLevel ? 1 : 0) + Mathf.Max(0, State != null ? State.extraBuilders : 0); } }
        public int BuildersBusy
        {
            get { int n = 0; foreach (var b in Building.All) if (b != null && !b.IsGhost && b.Data.IsUpgrading) n++; return n; }
        }
        public bool BuilderFree { get { return BuildersBusy < BuilderSlots; } }

        /// <summary>The upgrade that will finish first (the one "Free a builder" speeds up).</summary>
        public Building SoonestUpgrade()
        {
            Building best = null;
            foreach (var b in Building.All)
                if (b != null && !b.IsGhost && b.Data.IsUpgrading && (best == null || b.UpgradeSecondsLeft < best.UpgradeSecondsLeft)) best = b;
            return best;
        }
        public int HousingCapacity { get; private set; }
        public float FoodEfficiency { get { return State.food > 0 && State.water > 0 ? 1f : 0.5f; } }
        public int PendingSettlers { get; private set; }

        public event Action<string> OnToast;
        public event Action<int> OnSettlersArrived;   // number asking to join
        public event Action OnBuildingsChanged;

        ISaveBackend save;
        Transform buildingsRoot, peopleRoot;
        float workerTimer, foodTimer, saveTimer;
        bool started;

        void Awake() { Instance = this; }

        public void Init(WorldBuilder world, DayNightCycle cycle, EnemySpawner spawner, ISaveBackend saveBackend)
        {
            World = world; Cycle = cycle; Spawner = spawner; save = saveBackend;
            Grid = new GridMap();
            buildingsRoot = new GameObject("Buildings").transform;
            peopleRoot = new GameObject("People").transform;

            World.Build(Grid, 1234);

            State = save.Load();
            if (State != null && State.version < GameState.CurrentVersion) State = null; // old layout: start fresh
            bool fresh = State == null;
            if (fresh) State = new GameState();

            if (fresh)
            {
                var c = BuildingCatalog.Get(BuildingCatalog.Castle);
                ClearTreesUnder(CastleX, CastleZ, c, 0);
                PlaceInternal(c, CastleX, CastleZ, 0);
                // Starter house and farm in front of the castle so the first minute is not empty.
                var house = BuildingCatalog.Get("house");
                ClearTreesUnder(-6, 9, house, 0);
                PlaceInternal(house, -6, 9, 0);
                var farm = BuildingCatalog.Get("farm");
                ClearTreesUnder(3, 8, farm, 0);
                PlaceInternal(farm, 3, 8, 0);
                // A first archer tower beside the castle, so night 1 is never defenceless.
                var tower = BuildingCatalog.Get("watchtower");
                ClearTreesUnder(4, 12, tower, 0);
                if (CanPlace(tower, 4, 12, 0)) PlaceInternal(tower, 4, 12, 0);
            }
            else
            {
                var saved = new List<BuildingData>(State.buildings);
                State.buildings.Clear();
                foreach (var d in saved)
                {
                    var def = BuildingCatalog.Get(d.defId);
                    if (def == null) continue;
                    ClearTreesUnder(d.x, d.z, def, d.rotation);
                    RestoreInternal(def, d);
                }
            }
            // Older saves could have buildings on the river bank: move them to the nearest dry ground.
            foreach (var b in Building.All.ToArray())
            {
                if (b == Castle || b.Def.id == "canal") continue;
                bool wet = false;
                foreach (var c in b.Cells) if (GridMap.OnRiverBank(c)) wet = true;
                if (!wet) continue;
                for (int r = 1; r <= 12 && wet; r++)
                    for (int dx = -r; dx <= r && wet; dx++)
                        for (int dz = -r; dz <= r && wet; dz++)
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) == r && Move(b, b.Data.x + dx, b.Data.z + dz, b.Data.rotation)) wet = false;
            }

            // Never leave a city with no way to defend itself (fixes saves stuck after removing the only tower).
            bool hasWeapon = false;
            foreach (var b in Building.All) if (b != Castle && b.Def.attackRange > 0f && !b.Ruined) hasWeapon = true;
            if (!fresh && !hasWeapon && State.steel < 30)
            {
                State.steel = 30; State.coins = Mathf.Max(State.coins, 10);
                Toast("Command sent you steel: build a Gun Turret!");
            }

            if (Castle == null)
            {
                var c = BuildingCatalog.Get(BuildingCatalog.Castle);
                ClearTreesUnder(CastleX, CastleZ, c, 0);
                PlaceInternal(c, CastleX, CastleZ, 0);
            }

            Cycle.OnNightStarted += HandleNightStarted;
            Cycle.OnDawn += HandleDawn;
            World.ShowBuildBorder(BuildRadius);
            RecalculateCity();
            RefreshWallJoins();
            if (!fresh) ApplyOfflineProduction();
            SyncTamedDinos();
            started = true;
        }

        // ---------------- Placement ----------------

        public bool CanPlace(BuildingDef def, int x, int z, int rotation, Building ignore = null)
        {
            if (def.nearMountain && !TouchesMountain(def, x, z, rotation)) return false;
            if (!Grid.CanPlace(x, z, def, rotation, ignore, BuildRadius)) return false;
            if (def.indestructible && SealsCity(def, x, z, rotation, ignore)) return false;
            return true;
        }

        /// <summary>Walls can't be broken, so they may never close the city off completely: a Gate must stay.</summary>
        public bool SealsCity(BuildingDef def, int x, int z, int rotation, Building ignore = null)
        {
            var cells = new HashSet<Vector2Int>(GridMap.Footprint(x, z, def.width, def.depth, rotation));
            if (ignore != null && ignore.Def.indestructible)
            {
                // moving a wall: its old cells are open while it moves
                bool removed = false;
                foreach (var c in ignore.Cells) if (!cells.Contains(c)) { removed = true; break; }
                if (removed)
                {
                    Grid.Free(ignore.Cells, ignore);
                    bool sealsMoved = PathField.WouldSeal(Grid, Castle, cells, BuildRadius);
                    Grid.Occupy(ignore.Cells, ignore);
                    return sealsMoved;
                }
            }
            return PathField.WouldSeal(Grid, Castle, cells, BuildRadius);
        }

        /// <summary>Mines must sit against the mountain at the back of the valley.</summary>
        public static bool TouchesMountain(BuildingDef def, int x, int z, int rotation)
        {
            foreach (var c in GridMap.Footprint(x, z, def.width, def.depth, rotation))
                if (c.y >= GridMap.BuildBackZ - 2) return true;
            return false;
        }

        public Building PlaceNew(BuildingDef def, int x, int z, int rotation)
        {
            if (!CanPlace(def, x, z, rotation))
            {
                bool sealing = def.indestructible && Grid.CanPlace(x, z, def, rotation, null, BuildRadius) && SealsCity(def, x, z, rotation);
                Toast(sealing ? "Leave a way in: build a Gate there" : "Can't build there");
                return null;
            }
            if (!State.Pay(def.cost)) { Toast("Need more " + State.Missing(def.cost)); return null; }
            var b = PlaceInternal(def, x, z, rotation);
            b.StartConstruction(def.width * def.depth > 1 ? 4f : 1.5f);
            AudioManager.Play("step", 0.6f);
            RecalculateCity();
            SaveNow();
            return b;
        }

        public bool Move(Building b, int x, int z, int rotation)
        {
            if (!b.Def.Movable || !CanPlace(b.Def, x, z, rotation, b)) return false;
            Grid.Free(b.Cells, b);
            b.Data.x = x; b.Data.z = z; b.Data.rotation = rotation;
            Grid.Occupy(b.Cells, b);
            b.ApplyTransform();
            RecalculateWater();
            SaveNow();
            return true;
        }

        public void Demolish(Building b)
        {
            if (!b.Def.Movable) return;
            if (b.Ruined)
            {
                RemoveInternal(b);
                Toast("Ruins cleared");
                return;
            }
            // Full refund (build price + every upgrade paid, canals too): removing by mistake never costs anything.
            if (b.Def.id == "canal")
            {
                State.Refund(b.Def.cost, 1f);
                RemoveInternal(b);
                Toast("Canal filled in - coins refunded");
                return;
            }
            State.Refund(b.TotalPaid, 1f);
            RemoveInternal(b);
            Toast(b.Def.displayName + " removed - full cost refunded");
        }

        /// <summary>Raiders broke it: it stays as a smoking ruin (no homes, no work, no shooting) until rebuilt.</summary>
        public void DestroyBuilding(Building b)
        {
            PathField.MarkDirty();
            if (b == Castle)
            {
                // The castle never disappears: it falls, the raid ends and the city loses part of its stock.
                b.Data.hp = Mathf.Max(1, b.MaxHp / 4);
                b.RefreshDamageVisual();
                State.food /= 2; State.steel /= 2; State.energy /= 2; State.coins /= 2;
                Toast("The HQ has fallen! The dinosaurs wrecked half your stock.");
                AudioManager.Play("collapse");
                Spawner.EndWave();
                if (Raids != null) Raids.CancelAll();   // a daytime raid ends too
                Cycle.EndNight();                        // no-op by day
                return;
            }
            b.Data.hp = 0;
            b.SetRuined(true);
            AudioManager.PlayAt("collapse", b.Center, 1f);
            DamageEffects.Puff(b.Center + Vector3.up * 0.4f, 1.6f);
            // raiders loot a little from what the building held
            int loot = Mathf.Min(State.coins, 3 + b.Def.width * b.Def.depth);
            State.coins -= loot;
            Toast(b.Def.displayName + " was destroyed!" + (loot > 0 ? " You lost " + loot + " coins." : "") + " Tap it to rebuild.");
            RecalculateCity();
            SaveNow();
        }

        /// <summary>Cost to fix a building: a ruin costs 60% of the build price; damage costs in proportion.</summary>
        public Cost RepairCost(Building b)
        {
            if (b.Ruined) return b.Def.cost.Scaled(0.3f);
            float missing = 1f - b.Data.hp / (float)b.MaxHp;
            if (missing <= 0f) return new Cost();
            var c = b.Def.cost.Scaled(0.5f * missing);
            if (b == Castle) c = new Cost(steel: Mathf.CeilToInt(60 * missing), coins: Mathf.CeilToInt(20 * missing));
            return c;
        }

        public bool Repair(Building b)
        {
            if (!b.Ruined && !b.Damaged) return false;
            var cost = RepairCost(b);
            if (!State.Pay(cost)) { Toast("Need more " + State.Missing(cost)); return false; }
            bool wasRuin = b.Ruined;
            b.Data.hp = b.MaxHp;
            if (wasRuin)
            {
                b.SetRuined(false);
                b.StartConstruction(b.Def.width * b.Def.depth > 1 ? 5f : 1.5f);
                Toast(b.Def.displayName + " is being rebuilt");
            }
            else
            {
                b.RefreshDamageVisual();
                AudioManager.Play("hammer", 0.8f);
                Toast(b.Def.displayName + " repaired");
            }
            RecalculateCity();
            SaveNow();
            return true;
        }

        public bool TryUpgrade(Building b)
        {
            if (b.Data.IsUpgrading) return false;
            if (b.Data.level >= b.Def.maxLevel) { Toast("Max level"); return false; }
            if (b != Castle && b.Data.level >= CastleLevel + 1) { Toast("Upgrade the HQ first"); return false; }
            if (!BuilderFree) { Toast("All builders are busy"); return false; }
            if (!State.Pay(b.UpgradeCost)) { Toast("Not enough resources"); return false; }
            b.StartUpgrade();
            AudioManager.Play("coin", 0.6f);
            SaveNow();
            return true;
        }

        public void OnBuildingUpgraded(Building b)
        {
            Toast("Level up! " + b.Def.displayName + " is now level " + b.Data.level);
            DamageEffects.LevelUp(b.Center + Vector3.up * (b.Def.height * 0.8f), Mathf.Max(1f, b.Def.width * 0.6f));
            AudioManager.Play("coin", 0.8f);
            if (b == Castle) { World.ShowBuildBorder(BuildRadius); Toast("Your land grows!"); }
            if (b == Castle && b.Data.level == SecondBuilderHQLevel) Toast("A second builder joined your city!");
            RecalculateCity();
            SaveNow();
        }

        Building PlaceInternal(BuildingDef def, int x, int z, int rotation)
        {
            var data = new BuildingData { uid = Guid.NewGuid().ToString("N"), defId = def.id, x = x, z = z, rotation = rotation };
            return RestoreInternal(def, data);
        }

        Building RestoreInternal(BuildingDef def, BuildingData data)
        {
            if (string.IsNullOrEmpty(data.uid)) data.uid = Guid.NewGuid().ToString("N");
            var b = Building.Spawn(def, data, buildingsRoot, false);
            Grid.Occupy(b.Cells, b);
            State.buildings.Add(data);
            if (def.id == BuildingCatalog.Castle) Castle = b;
            return b;
        }

        void RemoveInternal(Building b)
        {
            Grid.Free(b.Cells, b);
            State.buildings.Remove(b.Data);
            foreach (var v in Villager.All) { if (v.home == b) v.home = null; if (v.work == b) v.SetJob(null); }
            Destroy(b.gameObject);
            Building.All.Remove(b);
            if (b.Def.id == "wall") RefreshWallJoins();
            RecalculateCity();
            SaveNow();
        }

        void ClearTreesUnder(int x, int z, BuildingDef def, int rotation)
        {
            foreach (var c in GridMap.Footprint(x, z, def.width, def.depth, rotation))
            {
                var t = Grid.At(c) as TreeNode;
                if (t != null) { Grid.Free(new[] { c }, t); Destroy(t.gameObject); }
            }
        }

        // ---------------- Economy & people ----------------

        public void Produce(Building b)
        {
            var def = b.Def;
            int amount = Mathf.RoundToInt(def.produceAmount * (1f + 0.5f * (b.Data.level - 1)));
            if (State.energy < def.consumeEnergy) { b.Starved = true; return; }
            b.Starved = false;
            State.energy -= def.consumeEnergy;
            // irrigated farms and wells next to water make more
            if ((def.id == "farm" || def.id == "well") && b.HasWater) amount = Mathf.RoundToInt(amount * 1.75f);
            State.Add(def.produces, amount);
        }

        public int PopulationIn(Building house)
        {
            int n = 0;
            foreach (var v in Villager.All) if (v.home == house) n++;
            return n;
        }

        /// <summary>The HQ always keeps this many people (it houses them), so scrap can always be gathered.</summary>
        const int MinPopulation = 2;

        /// <summary>Recount housing, assign homes and jobs, and match villagers to the population.</summary>
        public void RecalculateCity()
        {
            PathField.MarkDirty();
            HousingCapacity = 0;
            foreach (var b in Building.All) HousingCapacity += b.Housing;
            State.population = Mathf.Min(State.population, Mathf.Max(HousingCapacity, 1));
            State.population = Mathf.Max(State.population, MinPopulation);   // the HQ crew always stays

            while (Villager.All.Count < State.population)
                Villager.Spawn(peopleRoot, Castle != null ? Castle.Center + new Vector3(0, 0, -2.6f) : Vector3.zero);
            while (Villager.All.Count > State.population)
            {
                var v = Villager.All[Villager.All.Count - 1];
                Villager.All.RemoveAt(Villager.All.Count - 1);
                Destroy(v.gameObject);
            }

            // Homes
            int vi = 0;
            foreach (var b in Building.All)
                for (int i = 0; i < b.Housing && vi < Villager.All.Count; i++) Villager.All[vi++].home = b;

            // Jobs, in the order buildings were placed
            foreach (var b in Building.All) b.AssignedWorkers = 0;
            int wi = 0;
            // production first; guards for towers only when people are left over (towers shoot without them)
            var order = new List<Building>(Building.All);
            order.RemoveAll(x => x.Ruined || x.IsGhost);
            order.Sort((a, c) => (a.Def.attackRange > 0f ? 1 : 0).CompareTo(c.Def.attackRange > 0f ? 1 : 0));
            foreach (var b in order)
            {
                for (int i = 0; i < b.Def.workers && wi < Villager.All.Count; i++)
                {
                    Villager.All[wi++].SetJob(b);
                    b.AssignedWorkers++;
                }
            }
            for (; wi < Villager.All.Count; wi++) Villager.All[wi].SetJob(null);
            AssignFreeGatherers();
            SyncTamedDinos();

            RecalculateWater();
            if (OnBuildingsChanged != null) OnBuildingsChanged();
        }

        bool toldAboutGatherers;

        /// <summary>
        /// The economy must never get stuck: with no working Steel Drill, two townspeople collect
        /// scrap steel by hand at the foot of the mountain. Idle people go first; if everyone has a job,
        /// the last workers are borrowed. They go back to normal once a drill works.
        /// </summary>
        void AssignFreeGatherers()
        {
            bool hasDrill = false;
            foreach (var b in Building.All)
                if (!b.Ruined && !b.IsGhost && b.Def.id == "mine") hasDrill = true;
            foreach (var v in Villager.All) v.freeGather = null;
            int need = hasDrill ? 0 : 2;
            for (int pass = 0; pass < 2 && need > 0; pass++)
                for (int i = Villager.All.Count - 1; i >= 0 && need > 0; i--)
                {
                    var v = Villager.All[i];
                    if (v.freeGather.HasValue) continue;
                    if (pass == 0 && v.work != null) continue;           // idle people first
                    if (v.work != null) { v.work.AssignedWorkers = Mathf.Max(0, v.work.AssignedWorkers - 1); }
                    v.freeGather = ResourceType.Steel;
                    need--;
                    v.SetJob(null);
                }
            if (!hasDrill && !toldAboutGatherers && Villager.All.Count > 0)
            {
                toldAboutGatherers = true;
                Toast("Your people collect scrap steel. Build a Steel Drill to get more!");
            }
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        /// <summary>
        /// Water flows from the river (and wells) through connected canals.
        /// Farms and wells next to water get a bonus.
        /// </summary>
        /// <summary>Walls turn to follow their neighbours, and corners/junctions get a stone post, so lines join neatly.</summary>
        public void RefreshWallJoins()
        {
            foreach (var b in Building.All)
            {
                if (b.Def.id != "wall" && b.Def.id != "gate") continue;
                var c = new Vector2Int(b.Data.x, b.Data.z);
                bool l = IsWall(c + Vector2Int.left), r = IsWall(c + Vector2Int.right);
                bool u = IsWall(c + Vector2Int.up), d = IsWall(c + Vector2Int.down);
                bool alongX = l || r, alongZ = u || d;
                int rot = alongZ && !alongX ? 1 : 0;
                if (b.Data.rotation != rot) { b.Data.rotation = rot; b.ApplyTransform(); }
                if (b.Def.id == "wall") b.SetWallJoint(alongX && alongZ, rot == 0 ? (u || d) : false);
            }
        }

        bool IsWall(Vector2Int c)
        {
            var o = Grid.At(c) as Building;
            return o != null && (o.Def.id == "wall" || o.Def.id == "gate");
        }

        /// <summary>Would a canal on this cell get water? (next to the river, a well, or a canal that already has water)</summary>
        public bool CanalWouldHaveWater(int x, int z)
        {
            var c = new Vector2Int(x, z);
            foreach (var d in Dirs)
            {
                var o = Grid.At(c + d);
                if (o is RiverCell || River.Mask(c.x + d.x + 0.5f, c.y + d.y + 0.5f) > 0.05f) return true;
                var ob = o as Building;
                if (ob != null && (IsWorkingWell(ob) || (ob.Def.id == "canal" && !ob.Ruined && ob.HasWater))) return true;
            }
            return false;
        }

        /// <summary>A canal side is open (no bank) where it meets another canal, the river or a well.</summary>
        bool JoinsWater(Vector2Int c)
        {
            var o = Grid.At(c);
            if (o is RiverCell) return true;
            if (River.Mask(c.x + 0.5f, c.y + 0.5f) > 0.05f) return true;
            var b = o as Building;
            return b != null && !b.Ruined && (b.Def.id == "canal" || b.Def.id == "well");
        }

        /// <summary>Only a standing, finished well gives water. A ruined one dries up its canals.</summary>
        static bool IsWorkingWell(Building b) { return b != null && b.Def.id == "well" && !b.Ruined && !b.UnderConstruction; }

        public void RecalculateWater()
        {
            var wet = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            foreach (var b in Building.All)
            {
                if (b.Def.id != "canal" || b.Ruined) continue;
                bool source = false;
                foreach (var c in b.Cells)
                    foreach (var d in Dirs)
                    {
                        var o = Grid.At(c + d);
                        if (o is RiverCell || River.Mask(c.x + d.x + 0.5f, c.y + d.y + 0.5f) > 0.05f) source = true;
                        var ob = o as Building;
                        if (IsWorkingWell(ob)) source = true;
                    }
                if (source) foreach (var c in b.Cells) if (wet.Add(c)) queue.Enqueue(c);
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    var ob = Grid.At(n) as Building;
                    if (ob != null && ob.Def.id == "canal" && !ob.Ruined && wet.Add(n)) queue.Enqueue(n);
                }
            }
            foreach (var b in Building.All)
            {
                if (b.Def.id == "canal")
                {
                    bool w = false; foreach (var c in b.Cells) if (wet.Contains(c)) w = true;
                    b.SetWater(w);
                    var o = new Vector2Int(b.Data.x, b.Data.z);
                    b.SetCanalBanks(JoinsWater(o + Vector2Int.up), JoinsWater(o + Vector2Int.down), JoinsWater(o + Vector2Int.right), JoinsWater(o + Vector2Int.left));
                    continue;
                }
                if (b.Def.id != "farm" && b.Def.id != "well") continue;
                bool near = false;
                foreach (var c in b.Cells)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                        for (int dz = -1; dz <= 1 && !near; dz++)
                        {
                            var n = new Vector2Int(c.x + dx, c.y + dz);
                            if (wet.Contains(n) || Grid.At(n) is RiverCell) near = true;
                        }
                b.SetWater(near);
            }
        }

        public int FreeWorkers
        {
            get
            {
                int need = 0;
                foreach (var b in Building.All) need += b.Def.workers;
                return Mathf.Max(0, State.population - need);
            }
        }

        public void AnswerSettlers(bool accept)
        {
            if (accept && PendingSettlers > 0)
            {
                State.population += PendingSettlers;
                Toast(PendingSettlers + " settlers joined your city");
                RecalculateCity();
            }
            PendingSettlers = 0;
            SaveNow();
        }

        // ---------------- Night ----------------

        void HandleNightStarted()
        {
            Spawner.BeginWave(State.night + 1);
            Toast("Night " + (State.night + 1) + " - defend the city!");
        }

        void HandleDawn()
        {
            State.night++;
            int reward = 10 + State.night * 5;
            State.coins += reward;
            Toast("You survived night " + State.night + "!  +" + reward + " Gold");

            int room = HousingCapacity - State.population;
            if (room > 0 && State.food >= 5)
            {
                PendingSettlers = Mathf.Min(room, UnityEngine.Random.Range(1, 4));
                if (OnSettlersArrived != null) OnSettlersArrived(PendingSettlers);
            }
            SaveNow();
        }

        bool toldAboutPens;

        int PenCapacity()
        {
            int cap = 0;
            foreach (var b in Building.All)
                if (!b.IsGhost && !b.Ruined && b.Def.penCapacity > 0) cap += b.Def.penCapacity * b.Data.level;
            return cap;
        }

        /// <summary>A raptor or triceratops stepped on a trap: catch it if a pen has room.</summary>
        public bool TryCapture(Enemy e, Building trap)
        {
            if (trap.IsGhost || trap.Ruined || trap.UnderConstruction || Time.time < trap.trapReadyAt) return false;
            if (State.tamedRaptors + State.tamedTrikes >= PenCapacity())
            {
                if (!toldAboutPens) { toldAboutPens = true; Toast("Build a Dino Pen so your traps can keep what they catch!"); }
                return false;
            }
            trap.trapReadyAt = Time.time + 40f;
            if (e.kind == EnemyKind.Brute) State.tamedTrikes++; else State.tamedRaptors++;
            e.Captured();
            AudioManager.PlayAt("coin", trap.Center, 0.8f);
            Toast("You caught a " + (e.kind == EnemyKind.Brute ? "Triceratops" : "Velociraptor") + "! It now guards your city.");
            SyncTamedDinos();
            SaveNow();
            return true;
        }

        /// <summary>Shows each tamed dinosaur in a pen (the extra ones wait until there is room).</summary>
        public void SyncTamedDinos()
        {
            var wanted = new List<KeyValuePair<EnemyKind, Building>>();
            var kinds = new List<EnemyKind>();
            for (int i = 0; i < State.tamedTrikes; i++) kinds.Add(EnemyKind.Brute);
            for (int i = 0; i < State.tamedRaptors; i++) kinds.Add(EnemyKind.Raider);
            int k = 0;
            foreach (var b in Building.All)
            {
                if (b.IsGhost || b.Ruined || b.Def.penCapacity <= 0) continue;
                for (int i = 0; i < b.Def.penCapacity * b.Data.level && k < kinds.Count; i++)
                    wanted.Add(new KeyValuePair<EnemyKind, Building>(kinds[k++], b));
            }
            bool same = wanted.Count == TamedDino.All.Count;
            for (int i = 0; same && i < wanted.Count; i++)
                same = TamedDino.All[i].kind == wanted[i].Key && TamedDino.All[i].pen == wanted[i].Value;
            if (same) return;
            foreach (var d in TamedDino.All.ToArray()) { TamedDino.All.Remove(d); Destroy(d.gameObject); }
            foreach (var w in wanted) TamedDino.Spawn(w.Key, w.Value, peopleRoot);
        }

        public void OnEnemyKilled(Enemy e)
        {
            State.coins += e.reward;
        }

        /// <summary>Longest time away that still pays out, so players come back a few times a day.</summary>
        const double OfflineCapHours = 8;
        /// <summary>The city works at this share of its normal speed while the game is closed.</summary>
        const float OfflineRate = 0.6f;

        /// <summary>
        /// The city keeps producing while the game is closed (short-session design): every producer
        /// pays out for the time away, capped, and drills only run as long as there is energy.
        /// </summary>
        void ApplyOfflineProduction()
        {
            if (State.lastSavedUtcTicks <= 0) return;
            double away = (System.DateTime.UtcNow.Ticks - State.lastSavedUtcTicks) / (double)System.TimeSpan.TicksPerSecond;
            if (away < 60) return;
            float secs = (float)System.Math.Min(away, OfflineCapHours * 3600) * OfflineRate;
            var gained = new int[System.Enum.GetValues(typeof(ResourceType)).Length];
            // energy first, so drills can spend it
            for (int pass = 0; pass < 2; pass++)
                foreach (var b in Building.All)
                {
                    var d = b.Def;
                    if (b.IsGhost || b.Ruined || d.produces == ResourceType.None) continue;
                    if ((pass == 0) != (d.produces == ResourceType.Energy)) continue;
                    float eff = d.produces == ResourceType.Coins
                        ? Mathf.Clamp01(PopulationIn(b) / (float)Mathf.Max(1, b.Housing))
                        : b.WorkerEfficiency;
                    int cycles = Mathf.FloorToInt(secs * eff / d.produceInterval);
                    if (d.consumeEnergy > 0) cycles = Mathf.Min(cycles, State.energy / d.consumeEnergy);
                    if (cycles <= 0) continue;
                    State.energy -= cycles * d.consumeEnergy;
                    int amount = Mathf.RoundToInt(d.produceAmount * (1f + 0.5f * (b.Data.level - 1)));
                    if ((d.id == "farm" || d.id == "well") && b.HasWater) amount = Mathf.RoundToInt(amount * 1.75f);
                    State.Add(d.produces, cycles * amount);
                    gained[(int)d.produces] += cycles * amount;
                }
            var parts = new List<string>();
            foreach (ResourceType r in System.Enum.GetValues(typeof(ResourceType)))
                if (gained[(int)r] > 0) parts.Add("+" + gained[(int)r] + " " + r.ToString().ToLower());
            if (parts.Count > 0) Toast("While you were away: " + string.Join(", ", parts.ToArray()));
        }

        // ---------------- Loop ----------------

        void Update()
        {
            if (!started) return;

            if (Cycle.Phase == Phase.Night && Spawner.Finished)
            {
                Spawner.EndWave();
                Cycle.EndNight();
            }

            workerTimer += Time.deltaTime;
            if (workerTimer > 2f) { workerTimer = 0f; RecalculateWorkersOnly(); }

            foodTimer += Time.deltaTime;
            if (foodTimer > 30f)
            {
                foodTimer = 0f;
                int eat = Mathf.CeilToInt(State.population * 0.5f);
                if (State.food < eat) Toast("Your people are hungry! Build farms.");
                State.Add(ResourceType.Food, -eat);
                if (State.water < eat) Toast("Your people are thirsty! Dig a well.");
                State.Add(ResourceType.Water, -eat);
            }

            saveTimer += Time.deltaTime;
            if (saveTimer > 20f) { saveTimer = 0f; SaveNow(); }
        }

        void RecalculateWorkersOnly()
        {
            // cheap periodic check in case buildings changed during the night etc.
            if (Villager.All.Count != State.population) RecalculateCity();
        }

        public void SaveNow()
        {
            if (State == null) return;
            State.dayTimeLeft = Cycle != null ? Cycle.DayTimeLeft : DayNightCycle.DayLength;
            save.Save(State);
        }

        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() { SaveNow(); }

        readonly List<string> earlyToasts = new List<string>();

        public void Toast(string message)
        {
            if (OnToast != null)
            {
                foreach (var m in earlyToasts) OnToast(m);
                earlyToasts.Clear();
                OnToast(message);
            }
            else earlyToasts.Add(message); // shown once the HUD is ready
        }

        public void ResetGame()
        {
            save.Delete();
            started = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
