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

        /// <summary>Castle grid origin: at the back of the valley, against the mountain.</summary>
        public const int CastleX = -2, CastleZ = GridMap.MountainZ - 8;
        public static readonly Vector3 CastleCenter = new Vector3(CastleX + 2f, 0f, CastleZ + 2f);

        public int CastleLevel { get { return Castle != null ? Castle.Data.level : 1; } }
        public int BuildRadius { get { return 9 + 3 * CastleLevel; } }
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
            started = true;
        }

        // ---------------- Placement ----------------

        public bool CanPlace(BuildingDef def, int x, int z, int rotation, Building ignore = null)
        {
            return Grid.CanPlace(x, z, def, rotation, ignore, BuildRadius);
        }

        public Building PlaceNew(BuildingDef def, int x, int z, int rotation)
        {
            if (!CanPlace(def, x, z, rotation)) { Toast("Can't build there"); return null; }
            if (!State.Pay(def.cost)) { Toast("Not enough resources"); return null; }
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
            if (b.Def.id == "canal")
            {
                RemoveInternal(b);
                Toast("Canal filled in");
                return;
            }
            State.Refund(b.Def.cost, 0.5f);
            RemoveInternal(b);
            Toast(b.Def.displayName + " removed (50% refunded)");
        }

        public void DestroyBuilding(Building b)
        {
            if (b == Castle)
            {
                // The castle never disappears: it falls, the night ends and the city loses part of its stock.
                b.Data.hp = Mathf.Max(1, b.MaxHp / 4);
                State.food /= 2; State.wood /= 2; State.stone /= 2; State.gold /= 2;
                Toast("The castle has fallen! Raiders took half your stock.");
                Spawner.EndWave();
                Cycle.EndNight();
                return;
            }
            Toast(b.Def.displayName + " was destroyed!");
            AudioManager.Play("impact");
            RemoveInternal(b);
        }

        public bool TryUpgrade(Building b)
        {
            if (b.Data.IsUpgrading) return false;
            if (b.Data.level >= b.Def.maxLevel) { Toast("Max level"); return false; }
            if (b != Castle && b.Data.level >= CastleLevel + 1) { Toast("Upgrade the Castle first"); return false; }
            if (!State.Pay(b.UpgradeCost)) { Toast("Not enough resources"); return false; }
            b.StartUpgrade();
            AudioManager.Play("coin", 0.6f);
            SaveNow();
            return true;
        }

        public void OnBuildingUpgraded(Building b)
        {
            Toast(b.Def.displayName + " reached level " + b.Data.level);
            if (b == Castle) { World.ShowBuildBorder(BuildRadius); Toast("Your land grows!"); }
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
            if (def.id == "lumberjack")
            {
                var tree = TreeNode.NearestStanding(b.Center, def.harvestRadius);
                if (tree == null) return; // no trees nearby: idle until they regrow
                tree.Chop();
            }
            if (def.consumeWood > 0 || def.consumeStone > 0)
            {
                if (State.wood < def.consumeWood || State.stone < def.consumeStone) return;
                State.wood -= def.consumeWood; State.stone -= def.consumeStone;
            }
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

        /// <summary>Recount housing, assign homes and jobs, and match villagers to the population.</summary>
        public void RecalculateCity()
        {
            HousingCapacity = 0;
            foreach (var b in Building.All) HousingCapacity += b.Housing;
            State.population = Mathf.Min(State.population, Mathf.Max(HousingCapacity, 1));

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
            foreach (var b in Building.All)
            {
                for (int i = 0; i < b.Def.workers && wi < Villager.All.Count; i++)
                {
                    Villager.All[wi++].SetJob(b);
                    b.AssignedWorkers++;
                }
            }
            for (; wi < Villager.All.Count; wi++) Villager.All[wi].SetJob(null);

            RecalculateWater();
            if (OnBuildingsChanged != null) OnBuildingsChanged();
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        /// <summary>
        /// Water flows from the river (and wells) through connected canals.
        /// Farms and wells next to water get a bonus.
        /// </summary>
        public void RecalculateWater()
        {
            var wet = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            foreach (var b in Building.All)
            {
                if (b.Def.id != "canal") continue;
                bool source = false;
                foreach (var c in b.Cells)
                    foreach (var d in Dirs)
                    {
                        var o = Grid.At(c + d);
                        if (o is RiverCell) source = true;
                        var ob = o as Building;
                        if (ob != null && ob.Def.id == "well") source = true;
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
                    if (ob != null && ob.Def.id == "canal" && wet.Add(n)) queue.Enqueue(n);
                }
            }
            foreach (var b in Building.All)
            {
                if (b.Def.id == "canal") { bool w = false; foreach (var c in b.Cells) if (wet.Contains(c)) w = true; b.SetWater(w); continue; }
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
            State.gold += reward;
            Toast("You survived night " + State.night + "!  +" + reward + " Gold");

            int room = HousingCapacity - State.population;
            if (room > 0 && State.food >= 5)
            {
                PendingSettlers = Mathf.Min(room, UnityEngine.Random.Range(1, 4));
                if (OnSettlersArrived != null) OnSettlersArrived(PendingSettlers);
            }
            SaveNow();
        }

        public void OnEnemyKilled(Enemy e)
        {
            State.gold += e.reward;
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

        public void Toast(string message)
        {
            if (OnToast != null) OnToast(message);
        }

        public void ResetGame()
        {
            save.Delete();
            started = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
