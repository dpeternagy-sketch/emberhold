using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Peter's wall: one square loop around the city, just outside the land you can build on, in 4 sides.
    /// Each side is bought whole (one price, built in one go) and has its own level (1-5). A side's level sets the
    /// look of its wall (art tiers 1, 3, 5) and the strength of its gate, which sits in the middle of the side.
    /// When the HQ grows the land, the loop moves out with it: bought sides keep their level, gate and wall guns.
    /// Single wall pieces are never placed by hand.
    /// </summary>
    public partial class GameManager
    {
        public enum WallSide { North = 0, East = 1, South = 2, West = 3 }
        public static readonly string[] WallSideNames = { "North wall", "East wall", "South wall", "West wall" };
        public const int WallMaxLevel = 5;

        /// <summary>The loop's corners for an HQ level: xMin..xMax, zMin..zMax (cells, inclusive).</summary>
        public static RectInt WallBounds(int hq)
        {
            int r = 9 + 3 * hq;   // = BuildRadius
            int xMin = Mathf.Max(-r - 1, -GridMap.ForestX + 1), xMax = Mathf.Min(r, GridMap.ForestX - 2);
            int zMax = GridMap.BuildBackZ, zMin = Mathf.Max(GridMap.BuildBackZ - 2 * r - 1, GridMap.SeaZ + 2);
            return new RectInt(xMin, zMin, xMax - xMin, zMax - zMin);
        }

        /// <summary>The cells of one side, in order. North and South own the 4 corners.</summary>
        public static List<Vector2Int> WallCells(WallSide side, int hq)
        {
            var b = WallBounds(hq);
            int xMin = b.xMin, xMax = b.xMax, zMin = b.yMin, zMax = b.yMax;
            var list = new List<Vector2Int>();
            switch (side)
            {
                case WallSide.North: for (int x = xMin; x <= xMax; x++) list.Add(new Vector2Int(x, zMax)); break;
                case WallSide.South: for (int x = xMin; x <= xMax; x++) list.Add(new Vector2Int(x, zMin)); break;
                case WallSide.West: for (int z = zMin + 1; z < zMax; z++) list.Add(new Vector2Int(xMin, z)); break;
                case WallSide.East: for (int z = zMin + 1; z < zMax; z++) list.Add(new Vector2Int(xMax, z)); break;
            }
            // nothing on the river or its banks: the wall meets the water
            list.RemoveAll(c => !GridMap.IsTerrainBuildable(c) || GridMap.OnRiverBank(c));
            return list;
        }

        public int WallLevel(WallSide side) { return State.wallSides != null && State.wallSides.Length == 4 ? State.wallSides[(int)side] : 0; }

        public Cost WallBuyCost(WallSide side)
        {
            int n = WallCells(side, CastleLevel).Count;
            // the pieces, the gate and the side's two wall guns, all in one price
            var w = BuildingCatalog.Get("wall").cost; var g = BuildingCatalog.Get("gate").cost; var u = BuildingCatalog.Get("wallgun").cost;
            return new Cost(w.steel * n + g.steel + 2 * u.steel, w.energy * n + g.energy + 2 * u.energy, w.coins * n + g.coins + 2 * u.coins, w.food * n + g.food + 2 * u.food);
        }

        /// <summary>Price to take a side from a level to the next: steel, energy and coins for every piece
        /// (walls are the slowest, most expensive thing to max out).</summary>
        public Cost WallUpgradeCostAt(WallSide side, int level)
        {
            int n = WallCells(side, CastleLevel).Count, next = level + 1;
            return new Cost(steel: (6 + 6 * next) * n, energy: (2 + 3 * next) * n, coins: 20 * next * next);
        }

        public Cost WallUpgradeCost(WallSide side) { return WallUpgradeCostAt(side, WallLevel(side)); }

        /// <summary>Builder time for a side to reach the next level: 5, 20, 45, 80 minutes.</summary>
        public static float WallUpgradeSeconds(int level) { return 300f * level * level; }

        /// <summary>Buys a whole side: every piece, its gate and its two guns, built in one go.</summary>
        public bool BuyWallSide(WallSide side)
        {
            if (WallLevel(side) > 0) return false;
            var cost = WallBuyCost(side);
            if (!State.Pay(cost)) { Toast("Need more " + State.Missing(cost)); return false; }
            EnsureWallState();
            State.wallSides[(int)side] = 1;
            RebuildWalls(true);
            Toast(WallSideNames[(int)side] + " built");
            AudioManager.Play("step", 0.6f);
            SaveNow();
            return true;
        }

        public Building GateOf(WallSide side)
        {
            foreach (var b in Building.All) if (!b.IsGhost && b.Def.id == "gate" && WallSideOf(b) == (int)side) return b;
            return null;
        }

        /// <summary>Null when the side can start an upgrade now, else why not.</summary>
        public string WallUpgradeBlock(WallSide side)
        {
            int lv = WallLevel(side);
            var g = GateOf(side);
            if (lv <= 0 || g == null) return "Build it first";
            if (g.Data.IsUpgrading) return "Upgrading";
            if (lv >= WallMaxLevel) return "Max level";
            if (lv >= CastleLevel) return "Needs HQ level " + (lv + 1);
            if (!BuilderFree) return "All builders busy";
            if (!State.CanAfford(WallUpgradeCost(side))) return "Need more " + State.Missing(WallUpgradeCost(side));
            return null;
        }

        /// <summary>Starts the side's upgrade (a builder works on it; its gate shows the timer).</summary>
        public bool UpgradeWallSide(WallSide side)
        {
            var block = WallUpgradeBlock(side);
            if (block != null) { Toast(block); return false; }
            return TryUpgrade(GateOf(side));
        }

        /// <summary>Starts every side that can go up, as long as builders are free.</summary>
        public int UpgradeAllWalls()
        {
            int started = 0;
            for (int i = 0; i < 4; i++)
                if (WallUpgradeBlock((WallSide)i) == null && TryUpgrade(GateOf((WallSide)i))) started++;
            if (started == 0)
            {
                string why = null;
                for (int i = 0; i < 4 && why == null; i++) if (WallLevel((WallSide)i) > 0) why = WallUpgradeBlock((WallSide)i);
                Toast(why ?? "Build a wall side first");
            }
            else Toast(started == 1 ? "1 wall side is being upgraded" : started + " wall sides are being upgraded");
            return started;
        }

        /// <summary>Total price of upgrading every side that can go up now.</summary>
        public Cost WallUpgradeAllCost(out int sides)
        {
            sides = 0;
            var t = new Cost();
            for (int i = 0; i < 4; i++)
            {
                var s = (WallSide)i; int lv = WallLevel(s); var g = GateOf(s);
                if (lv <= 0 || g == null || g.Data.IsUpgrading || lv >= WallMaxLevel || lv >= CastleLevel) continue;
                var c = WallUpgradeCost(s);
                t = new Cost(t.steel + c.steel, t.energy + c.energy, t.coins + c.coins, t.food + c.food);
                sides++;
            }
            return t;
        }

        public void EnsureWallState()
        {
            if (State.wallSides == null || State.wallSides.Length != 4) State.wallSides = new int[4];
        }

        /// <summary>Which side a ring cell belongs to (-1 = not on the loop).</summary>
        static int SideOf(Vector2Int c, RectInt b)
        {
            if (c.y == b.yMax && c.x >= b.xMin && c.x <= b.xMax) return (int)WallSide.North;
            if (c.y == b.yMin && c.x >= b.xMin && c.x <= b.xMax) return (int)WallSide.South;
            if (c.x == b.xMin && c.y > b.yMin && c.y < b.yMax) return (int)WallSide.West;
            if (c.x == b.xMax && c.y > b.yMin && c.y < b.yMax) return (int)WallSide.East;
            return -1;
        }

        int ringBuiltFor = -1;
        void GrowWalls() { RebuildWalls(true); SaveNow(); }
        void RelevelWalls() { RebuildWalls(false); SaveNow(); }

        /// <summary>
        /// Lays every bought side on the loop for the current HQ level. Wall guns keep their place along their side
        /// and their level; a gate keeps its damage only when the loop did not move.
        /// </summary>
        /// <summary>Wall guns that came with the bought sides (2 per side long enough to carry them).</summary>
        public int IncludedWallGuns()
        {
            if (State == null || State.wallSides == null) return 0;
            int n = 0, hq = CastleLevel;
            for (int s = 0; s < 4 && s < State.wallSides.Length; s++)
                if (State.wallSides[s] > 0 && WallCells((WallSide)s, hq).Count >= 6) n += 2;
            return n;
        }

        /// <summary>Where a side's gate goes: where the painted path meets that side (South on the north-south path,
        /// East and West on the east-west path), else the middle. Never at either end.</summary>
        static int GateIndex(WallSide side, List<Vector2Int> cells)
        {
            int at = cells.Count / 2;
            if (cells.Count < 5 || side == WallSide.North) return at;
            float best = float.MaxValue;
            for (int i = 2; i < cells.Count - 2; i++)
            {
                var c = cells[i];
                float d = side == WallSide.South ? Mathf.Abs(c.x + 0.5f - WorldBuilder.PathX(c.y + 0.5f))
                                                 : Mathf.Abs(c.y + 0.5f - WorldBuilder.PathZ(c.x + 0.5f));
                if (d < best) { best = d; at = i; }
            }
            return at;
        }

        public void RebuildWalls(bool clearAround)
        {
            EnsureWallState();
            int hq = CastleLevel;
            var oldB = WallBounds(ringBuiltFor > 0 ? ringBuiltFor : hq);
            var guns = new List<KeyValuePair<int, KeyValuePair<float, int>>>();   // side, (place along it 0..1, level)
            var gateHp = new Dictionary<int, int>();
            foreach (var b in Building.All.ToArray())
            {
                if (b.IsGhost) continue;
                var id = b.Def.id;
                if (id != "wall" && id != "gate" && id != "wallgun") continue;
                var c = new Vector2Int(b.Data.x, b.Data.z);
                int s = SideOf(c, oldB);
                if (id == "wallgun" && s >= 0)
                {
                    var cells = WallCells((WallSide)s, ringBuiltFor > 0 ? ringBuiltFor : hq);
                    int i = cells.IndexOf(c);
                    guns.Add(new KeyValuePair<int, KeyValuePair<float, int>>(s, new KeyValuePair<float, int>(cells.Count > 1 ? Mathf.Max(0, i) / (float)(cells.Count - 1) : 0.5f, b.Data.level)));
                }
                if (id == "gate" && s >= 0 && ringBuiltFor == hq) gateHp[s] = b.Data.hp;
                RemoveInternal(b);
            }
            var wall = BuildingCatalog.Get("wall"); var gate = BuildingCatalog.Get("gate"); var gun = BuildingCatalog.Get("wallgun");
            for (int s = 0; s < 4; s++)
            {
                int lv = State.wallSides[s];
                if (lv <= 0) continue;
                var cells = WallCells((WallSide)s, hq);
                int gateAt = GateIndex((WallSide)s, cells);
                var gunAt = new Dictionary<int, int>();
                // Peter: every side comes with its own two guns (a quarter of the way in from each end); they level with the side
                if (cells.Count >= 6) { gunAt[cells.Count / 4] = lv; gunAt[cells.Count - 1 - cells.Count / 4] = lv; }
                foreach (var g in guns)
                    if (g.Key == s && cells.Count > 0)
                    {
                        int i = Mathf.Clamp(Mathf.RoundToInt(g.Value.Key * (cells.Count - 1)), 0, cells.Count - 1);
                        if (i == gateAt) i = Mathf.Min(cells.Count - 1, i + 1);
                        gunAt[i] = g.Value.Value;
                    }
                for (int i = 0; i < cells.Count; i++)
                {
                    var c = cells[i];
                    var o = Grid.At(c);
                    if (o is TreeNode) { Grid.Free(new[] { c }, o); Destroy(((TreeNode)o).gameObject); }
                    else if (o != null) continue;   // never on top of anything else
                    var def = i == gateAt ? gate : gunAt.ContainsKey(i) ? gun : wall;
                    var b = PlaceInternal(def, c.x, c.y, 0);
                    b.Data.level = lv;   // pieces, gate and guns all follow the side's level
                    b.Data.hp = def == gate && gateHp.ContainsKey(s) ? Mathf.Min(gateHp[s], b.MaxHp) : b.MaxHp;
                }
                if (clearAround) ClearAroundSide((WallSide)s, hq);
            }
            ringBuiltFor = hq;
            RefreshWallJoins();
            RecalculateCity();
        }

        /// <summary>Nothing may hide the wall: trees and rocks right next to it go, and on the two sides that face
        /// the camera (South and West) also the tall trees up to 4 cells in front of it.</summary>
        void ClearAroundSide(WallSide side, int hq)
        {
            var b = WallBounds(hq);
            foreach (var t in TreeNode.All.ToArray())
            {
                if (t == null) continue;
                var c = t.cell;
                bool hit = false;
                switch (side)
                {
                    case WallSide.North: hit = c.y >= b.yMax - 1 && c.y <= b.yMax + 1 && c.x >= b.xMin - 1 && c.x <= b.xMax + 1; break;
                    case WallSide.East: hit = c.x >= b.xMax - 1 && c.x <= b.xMax + 1 && c.y >= b.yMin - 1 && c.y <= b.yMax + 1; break;
                    case WallSide.South: hit = c.y >= b.yMin - 4 && c.y <= b.yMin + 1 && c.x >= b.xMin - 4 && c.x <= b.xMax + 1; break;
                    case WallSide.West: hit = c.x >= b.xMin - 4 && c.x <= b.xMin + 1 && c.y >= b.yMin - 4 && c.y <= b.yMax + 1; break;
                }
                if (hit) { Grid.Free(new[] { c }, t); Destroy(t.gameObject); }
            }
            if (DecorField.Instance == null) return;
            foreach (var c in WallCells(side, hq))
            {
                Vector3 foot; string what;
                var p = new Vector3(c.x + 0.5f, 0f, c.y + 0.5f);
                for (int i = 0; i < 4 && DecorField.Instance.FindObstacle(p, 1.2f, out foot, out what); i++) DecorField.Instance.Remove(foot);
            }
        }

        /// <summary>At start: older saves had hand-placed wall pieces. They are taken down (the loop replaces them);
        /// a save with bought sides gets its loop laid again if the HQ level changed since.</summary>
        void InitWalls()
        {
            EnsureWallState();
            bool any = false;
            for (int i = 0; i < 4; i++) if (State.wallSides[i] > 0) any = true;
            if (!any)
            {
                foreach (var b in Building.All.ToArray())
                    if (!b.IsGhost && (b.Def.id == "wall" || b.Def.id == "gate" || b.Def.id == "wallgun")) RemoveInternal(b);
                ringBuiltFor = CastleLevel;
                return;
            }
            // the saved pieces are already on the loop when it matches this HQ level
            var bounds = WallBounds(CastleLevel);
            bool onLoop = true;
            foreach (var b in Building.All)
                if (!b.IsGhost && (b.Def.id == "wall" || b.Def.id == "gate" || b.Def.id == "wallgun") && SideOf(new Vector2Int(b.Data.x, b.Data.z), bounds) < 0) onLoop = false;
            ringBuiltFor = CastleLevel;
            if (!onLoop) { ringBuiltFor = -1; RebuildWalls(false); }
        }

        /// <summary>The side a wall piece or gate belongs to (-1 if it is not on the loop).</summary>
        public int WallSideOf(Building b)
        {
            if (b == null) return -1;
            return SideOf(new Vector2Int(b.Data.x, b.Data.z), WallBounds(ringBuiltFor > 0 ? ringBuiltFor : CastleLevel));
        }
    }
}
