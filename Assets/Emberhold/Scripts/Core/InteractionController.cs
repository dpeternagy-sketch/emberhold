using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Touch rules:
    ///  - drag empty ground: pan camera; pinch / wheel: zoom
    ///  - tap a building: select it; drag a building: move it (snaps to grid, red if not allowed)
    ///  - while placing a new building: drag the ghost, then press OK
    /// </summary>
    public class InteractionController : MonoBehaviour
    {
        enum Mode { None, Pan, DragGhost, DragBuilding, Ignored }

        public Building Selected { get; private set; }
        public Building Ghost { get; private set; }
        public bool GhostValid { get; private set; }
        public event Action OnSelectionChanged;

        static readonly Color ValidTint = new Color(0.3f, 0.9f, 0.4f);
        static readonly Color InvalidTint = new Color(0.95f, 0.25f, 0.2f);
        const float DragThresholdPixels = 12f;

        CameraRig rig;
        GameManager gm;
        Mode mode;
        bool wasPressed;
        Vector2 downScreen, lastScreen;
        bool movedBeyondThreshold;
        Vector3 grabOffset;
        int previewX, previewZ;
        bool previewValid;

        public void Init(CameraRig cameraRig, GameManager game)
        {
            rig = cameraRig;
            gm = game;
        }

        // ---------------- Public API used by the HUD ----------------

        public void BeginPlacing(BuildingDef def)
        {
            if (!gm.CanOwnMore(def, true)) return;
            if (def.id == "wallgun")
            {
                bool anyWall = false;
                foreach (var w in Building.All) if (w.Def.id == "wall" && !w.IsGhost) { anyWall = true; break; }
                if (!anyWall) { gm.Toast("Build a wall first. The gun sits on a wall piece"); return; }
            }
            CancelPlacement();
            Deselect();
            Vector3 center;
            if (!rig.GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out center)) center = Vector3.zero;
            var origin = FindFreeSpot(def, GridMap.OriginFromCenter(center, def, 0));
            var data = new BuildingData { defId = def.id, x = origin.x, z = origin.y, rotation = 0, level = 1 };
            Ghost = Building.Spawn(def, data, null, true);
            Ghost.SetRaised(true);
            RefreshGhost();
            Changed();
        }

        /// <summary>Nearest spot (spiralling out from the start) where the building fits on empty ground.</summary>
        Vector2Int FindFreeSpot(BuildingDef def, Vector2Int start)
        {
            for (int r = 0; r <= 30; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        // keep one free cell around other buildings so the new one never sits on top of them
                        int x = start.x + dx, z = start.y + dz;
                        if (gm.CanPlace(def, x, z, 0) && !Hidden(def, x, z))
                            return new Vector2Int(x, z);
                    }
            for (int r = 0; r <= 30; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) == r && gm.CanPlace(def, start.x + dx, start.y + dz, 0))
                            return new Vector2Int(start.x + dx, start.y + dz);
            return start;
        }

        /// <summary>
        /// True when a taller building stands between this spot and the camera (the camera looks
        /// toward +z), so a new building would appear behind it.
        /// </summary>
        bool Hidden(BuildingDef def, int x, int z)
        {
            var c = GridMap.FootprintCenter(x, z, def, 0);
            float half = Mathf.Max(def.width, def.depth) * 0.5f;
            foreach (var b in Building.All)
            {
                if (b.IsGhost || b.Def.height < 0.6f) continue;
                var bc = b.Center;
                var bs = GridMap.Size(b.Def, b.Data.rotation);
                float dz = c.z - bc.z;                       // > 0: the other building is in front
                if (dz <= 0f || dz > bs.y * 0.5f + b.Def.height * 1.3f + half) continue;
                if (Mathf.Abs(c.x - bc.x) < bs.x * 0.5f + half) return true;
            }
            return false;
        }

        // ---- Smart lines (walls, canals): the last piece you built is the start, the preview is the end ----
        Vector2Int? lineAnchor;
        readonly System.Collections.Generic.List<Building> linePreview = new System.Collections.Generic.List<Building>();
        const int MaxLine = 40;

        public static bool IsLineDef(BuildingDef d) { return d.width == 1 && d.depth == 1 && (d.id == "wall" || d.id == "canal"); }

        /// <summary>How many pieces the next Build press will place (1 unless a line is being drawn).</summary>
        public int LineCount { get; private set; }
        public int LineValidCount { get; private set; }

        /// <summary>Cells from the start (excluded) to the end (included): first along x, then along z.</summary>
        static System.Collections.Generic.List<Vector2Int> LinePath(Vector2Int from, Vector2Int to)
        {
            var list = new System.Collections.Generic.List<Vector2Int>();
            var c = from;
            int sx = Math.Sign(to.x - from.x), sz = Math.Sign(to.y - from.y);
            while (c.x != to.x && list.Count < MaxLine) { c.x += sx; list.Add(c); }
            while (c.y != to.y && list.Count < MaxLine) { c.y += sz; list.Add(c); }
            if (list.Count == 0) list.Add(to);
            return list;
        }

        System.Collections.Generic.List<Vector2Int> CurrentLine()
        {
            var end = new Vector2Int(Ghost.Data.x, Ghost.Data.z);
            if (!IsLineDef(Ghost.Def) || !lineAnchor.HasValue) return new System.Collections.Generic.List<Vector2Int> { end };
            return LinePath(lineAnchor.Value, end);
        }

        void ClearLinePreview()
        {
            foreach (var b in linePreview) if (b != null) Destroy(b.gameObject);
            linePreview.Clear();
        }

        void UpdateLinePreview()
        {
            ClearLinePreview();
            var cells = CurrentLine();
            LineCount = cells.Count;
            LineValidCount = 0;
            var budget = new GameState();
            budget.steel = gm.State.steel; budget.energy = gm.State.energy; budget.coins = gm.State.coins; budget.food = gm.State.food;
            foreach (var c in cells)
            {
                bool ok = gm.CanPlace(Ghost.Def, c.x, c.y, 0) && budget.Pay(Ghost.Def.cost);
                if (ok) LineValidCount++;
                if (c.x == Ghost.Data.x && c.y == Ghost.Data.z) continue;     // the ghost itself is the end piece
                var data = new BuildingData { defId = Ghost.Def.id, x = c.x, z = c.y, rotation = 0, level = 1 };
                var seg = Building.Spawn(Ghost.Def, data, null, true);
                seg.SetRaised(true);
                seg.SetTint(ok ? ValidTint : InvalidTint);
                linePreview.Add(seg);
            }
        }

        public void ConfirmPlacement()
        {
            if (Ghost == null) return;
            var def = Ghost.Def;
            var d = Ghost.Data;
            if (IsLineDef(def))
            {
                var cells = CurrentLine();
                int built = 0;
                Vector2Int last = new Vector2Int(d.x, d.z);
                foreach (var c in cells)
                {
                    if (!gm.CanPlace(def, c.x, c.y, 0) || !gm.State.CanAfford(def.cost)) continue;
                    if (!gm.CanOwnMore(def, built == 0)) break;
                    if (gm.PlaceNew(def, c.x, c.y, 0) != null) { built++; last = c; }
                }
                if (built == 0)
                {
                    gm.Toast(gm.State.CanAfford(def.cost) ? "Can't build there" : "Need more " + gm.State.Missing(def.cost));
                    return;
                }
                if (built > 1) gm.Toast("Built " + built + " x " + def.displayName);
                gm.RefreshWallJoins();
                lineAnchor = last;
                // the preview waits one step further, ready for the next stretch
                var dir = cells.Count > 1 ? cells[cells.Count - 1] - cells[cells.Count - 2] : new Vector2Int(1, 0);
                d.x = last.x + dir.x; d.z = last.y + dir.y;
                if (!gm.State.CanAfford(def.cost) || !gm.CanOwnMore(def, true)) { CancelPlacement(); return; }
                RefreshGhost();
                return;
            }
            if (!gm.CanOwnMore(def, true)) { CancelPlacement(); return; }
            var placed = gm.PlaceNew(def, d.x, d.z, d.rotation);
            if (placed == null) return;
            CancelPlacement();
            Select(placed);
        }

        public void CancelPlacement()
        {
            if (Ghost != null) Destroy(Ghost.gameObject);
            Ghost = null;
            lineAnchor = null;
            ClearLinePreview();
            LineCount = LineValidCount = 0;
            Changed();
        }

        public void RotateCurrent()
        {
            if (Ghost != null)
            {
                var center = Ghost.Center;
                Ghost.Data.rotation = (Ghost.Data.rotation + 1) % 4;
                var o = GridMap.OriginFromCenter(center, Ghost.Def, Ghost.Data.rotation);
                Ghost.Data.x = o.x; Ghost.Data.z = o.y;
                RefreshGhost();
                AudioManager.Play("ui", 0.5f);
                return;
            }
            if (Selected == null || !Selected.Def.Movable) return;
            int rot = (Selected.Data.rotation + 1) % 4;
            var c = Selected.Center;
            var origin = GridMap.OriginFromCenter(c, Selected.Def, rot);
            if (gm.Move(Selected, origin.x, origin.y, rot) || gm.Move(Selected, Selected.Data.x, Selected.Data.z, rot))
                AudioManager.Play("ui", 0.5f);
            else
                gm.Toast("No room to rotate here");
        }

        public void Select(Building b)
        {
            if (Selected == b) return;
            if (Selected != null) Selected.SetRaised(false);
            Selected = b;
            if (b != null) AudioManager.Play("ui", 0.4f);
            Changed();
        }

        public void Deselect() { Select(null); }

        void Changed() { if (OnSelectionChanged != null) OnSelectionChanged(); }

        void RefreshGhost()
        {
            if (Ghost == null) return;
            Ghost.ApplyTransform();
            GhostValid = gm.CanPlace(Ghost.Def, Ghost.Data.x, Ghost.Data.z, Ghost.Data.rotation) && gm.State.CanAfford(Ghost.Def.cost);
            Ghost.SetTint(GhostValid ? ValidTint : InvalidTint);
            if (IsLineDef(Ghost.Def))
            {
                UpdateLinePreview();
                GhostValid = LineValidCount > 0;
                Ghost.SetTint(GhostValid ? ValidTint : InvalidTint);
            }
            if (Ghost.Def.id == "canal")
            {
                GhostHasWater = gm.CanalWouldHaveWater(Ghost.Data.x, Ghost.Data.z);
                Ghost.SetWater(GhostHasWater);
            }
        }

        /// <summary>For canals being placed: true when this spot touches the river, a well or a wet canal.</summary>
        public bool GhostHasWater { get; private set; }

        // ---------------- Pointer handling ----------------

        /// <summary>The preview slides to its snapped cell instead of jumping, so it feels glued to the finger.</summary>
        void SmoothGhost()
        {
            if (Ghost == null) return;
            var target = GridMap.FootprintCenter(Ghost.Data.x, Ghost.Data.z, Ghost.Def, Ghost.Data.rotation);
            Ghost.transform.position = Vector3.Lerp(Ghost.transform.position, target, 1f - Mathf.Exp(-22f * Time.deltaTime));
        }

        /// <summary>Holding a building near the screen edge scrolls the map that way.</summary>
        void EdgePan(Vector2 screen)
        {
            float m = Screen.height * 0.09f;
            var dir = Vector2.zero;
            if (screen.x < m) dir.x = -1f; else if (screen.x > Screen.width - m) dir.x = 1f;
            if (screen.y < m * 1.6f) dir.y = -1f; else if (screen.y > Screen.height - m * 1.6f) dir.y = 1f;
            if (dir == Vector2.zero) return;
            float speed = rig.distance * 0.22f * Time.deltaTime;
            rig.Pan(-new Vector3(dir.x, 0f, dir.y) * speed);
        }

        void Update()
        {
            if (rig == null) return;
            SmoothGhost();
            rig.Zoom(PointerInput.ZoomDelta);
            UpdateRangeCircle();

            bool pressed = PointerInput.Pressed;
            var screen = PointerInput.Position;

            if (PointerInput.TouchCount >= 2)
            {
                // pinching: stop any drag, keep the building where it was
                if (mode == Mode.DragBuilding) EndBuildingDrag(false);
                mode = Mode.Ignored;
                wasPressed = pressed;
                return;
            }

            if (pressed && !wasPressed) PointerDown(screen);
            else if (pressed && wasPressed) PointerHeld(screen);
            else if (!pressed && wasPressed) PointerUp(screen);
            wasPressed = pressed;
            lastScreen = screen;
        }

        bool dragTinted;

        void PointerDown(Vector2 screen)
        {
            dragTinted = false;
            downScreen = lastScreen = screen;
            movedBeyondThreshold = false;
            if (PointerInput.IsOverUI(screen)) { mode = Mode.Ignored; return; }

            Vector3 ground;
            if (!rig.GroundPoint(screen, out ground)) { mode = Mode.Ignored; return; }
            var cell = GridMap.CellOf(ground);

            if (Ghost != null && NearGhost(screen, cell))
            {
                mode = Mode.DragGhost;
                grabOffset = Ghost.Center - ground;
                grabOffset.y = 0;
                return;
            }

            var b = gm.Grid.At(cell) as Building;
            if (b != null && SelectedUnits.Count > 0 && Ghost == null) { mode = Mode.Pan; return; }   // the tap sends troops (PointerUp)
            if (b != null && Ghost == null && UnitAt(screen) != null) { mode = Mode.Pan; return; }    // the tap picks a unit standing there
            if (b != null)
            {
                Select(b);
                mode = b.Def.Movable && Ghost == null ? Mode.DragBuilding : Mode.Pan;
                grabOffset = b.Center - ground;
                previewX = b.Data.x; previewZ = b.Data.z; previewValid = true;
                return;
            }
            mode = Mode.Pan;
        }

        void PointerHeld(Vector2 screen)
        {
            if ((screen - downScreen).sqrMagnitude > DragThresholdPixels * DragThresholdPixels) movedBeyondThreshold = true;

            switch (mode)
            {
                case Mode.Pan:
                {
                    Vector3 a, b;
                    if (rig.GroundPoint(lastScreen, out a) && rig.GroundPoint(screen, out b)) rig.Pan(b - a);
                    break;
                }
                case Mode.DragGhost:
                {
                    if (Ghost == null) break;
                    EdgePan(screen);
                    Vector3 g;
                    if (!rig.GroundPoint(screen, out g)) break;
                    var o = GridMap.OriginFromCenter(g + grabOffset, Ghost.Def, Ghost.Data.rotation);
                    if (o.x != Ghost.Data.x || o.y != Ghost.Data.z)
                    {
                        var was = Ghost.transform.position;
                        Ghost.Data.x = o.x; Ghost.Data.z = o.y;
                        RefreshGhost();
                        Ghost.transform.position = was;   // SmoothGhost slides it over
                    }
                    break;
                }
                case Mode.DragBuilding:
                {
                    if (!movedBeyondThreshold || Selected == null) break;
                    if (!dragTinted) { dragTinted = true; Selected.SetTint(ValidTint); }
                    Selected.SetRaised(true);
                    EdgePan(screen);
                    Vector3 g;
                    if (!rig.GroundPoint(screen, out g)) break;
                    var o = GridMap.OriginFromCenter(g + grabOffset, Selected.Def, Selected.Data.rotation);
                    if (o.x != previewX || o.y != previewZ)
                    {
                        previewX = o.x; previewZ = o.y;
                        previewValid = gm.CanPlace(Selected.Def, previewX, previewZ, Selected.Data.rotation, Selected);
                        Selected.transform.position = GridMap.FootprintCenter(previewX, previewZ, Selected.Def, Selected.Data.rotation);
                        Selected.SetTint(previewValid ? ValidTint : InvalidTint);
                    }
                    break;
                }
            }
        }

        void PointerUp(Vector2 screen)
        {
            if (mode == Mode.DragBuilding && movedBeyondThreshold) EndBuildingDrag(true);
            else if (mode == Mode.Pan && !movedBeyondThreshold)
            {
                // tap on empty ground (or on the castle while placing): clear selection
                Vector3 g;
                Building tapped = null;
                if (rig.GroundPoint(screen, out g)) tapped = gm.Grid.At(GridMap.CellOf(g)) as Building;
                var unitHit = Ghost == null ? UnitAt(screen) : null;
                if (unitHit != null) { ToggleUnit(unitHit); mode = Mode.None; return; }
                if (Ghost == null && SelectedUnits.Count > 0 && rig.GroundPoint(screen, out g)) { SendUnits(g); mode = Mode.None; return; }
                if (Ghost != null)
                {
                    // tap on the ground while placing: jump the preview there
                    if (rig.GroundPoint(screen, out g))
                    {
                        var o = GridMap.OriginFromCenter(g, Ghost.Def, Ghost.Data.rotation);
                        Ghost.Data.x = o.x; Ghost.Data.z = o.y;
                        RefreshGhost();
                    }
                }
                else if (tapped == null)
                {
                    if (TryClearObstacle(screen, g)) { mode = Mode.None; return; }
                    Deselect();
                }
            }
            mode = Mode.None;
        }

        // ---------------- Clearing trees and rocks ----------------
        // First tap on a tree, rock, log or stump says what clearing it gives; a second tap within
        // a few seconds clears it (no builder, no cost), like obstacles in other city builders.

        TreeNode armedTree;
        Vector3 armedRock;
        string armedRockName;
        float armedUntil;
        const float TreeTapPixels = 80f;

        bool TryClearObstacle(Vector2 screen, Vector3 ground)
        {
            TreeNode tree = null; float best = TreeTapPixels;
            foreach (var t in TreeNode.All)
            {
                if (t == null || !t.Standing) continue;
                var sp = rig.Cam.WorldToScreenPoint(t.transform.position + Vector3.up * 1.2f);
                if (sp.z < 0f) continue;
                float d = Vector2.Distance(screen, new Vector2(sp.x, sp.y));
                if (d < best) { best = d; tree = t; }
            }
            bool again = Time.unscaledTime < armedUntil;
            if (tree != null)
            {
                Deselect();
                if (again && armedTree == tree) { armedTree = null; armedUntil = 0f; gm.ClearTree(tree); return true; }
                armedTree = tree; armedRockName = null; armedUntil = Time.unscaledTime + 3f;
                gm.Toast("Tap again to clear the tree: +" + GameManager.TreeFood + " Food, +" + GameManager.TreeCoins + " Coins");
                return true;
            }
            Vector3 foot; string name;
            if (DecorField.Instance != null && DecorField.Instance.FindObstacle(ground, 0.8f, out foot, out name))
            {
                Deselect();
                if (again && armedRockName != null && (armedRock - foot).sqrMagnitude < 0.0001f)
                {
                    armedRockName = null; armedUntil = 0f;
                    gm.ClearRock(foot, name);
                    return true;
                }
                armedRock = foot; armedRockName = name; armedTree = null; armedUntil = Time.unscaledTime + 3f;
                gm.Toast(name.StartsWith("rock_") ? "Tap again to clear the rock: +" + GameManager.RockSteel + " Steel, +" + GameManager.RockCoins + " Coins"
                                                  : "Tap again to clear it: +" + GameManager.LogCoins + " Coins");
                return true;
            }
            return false;
        }

        // ---------------- Troop orders ----------------
        // Tap a soldier, robot or drone to select it (tap again to drop it). "All" selects the whole army.
        // With troops selected, tap the ground: they go there and guard that spot. "Home" calls them back.

        public readonly List<Unit> SelectedUnits = new List<Unit>();
        const float UnitTapPixels = 70f;

        Unit UnitAt(Vector2 screen)
        {
            Unit best = null; float bestD = UnitTapPixels;
            foreach (var u in Unit.All)
            {
                if (u == null || u.KnockedOut) continue;
                var sp = rig.Cam.WorldToScreenPoint(u.Center);
                if (sp.z <= 0f) continue;
                float d = Vector2.Distance(screen, new Vector2(sp.x, sp.y));
                if (d < bestD) { bestD = d; best = u; }
            }
            return best;
        }

        public void ToggleUnit(Unit u)
        {
            if (SelectedUnits.Remove(u)) u.SetSelected(false);
            else { SelectedUnits.Add(u); u.SetSelected(true); }
            AudioManager.Play("ui", 0.4f);
            Changed();
        }

        public void SelectAllUnits()
        {
            ClearUnits();
            foreach (var u in Unit.All) if (u != null && !u.KnockedOut) { SelectedUnits.Add(u); u.SetSelected(true); }
            if (SelectedUnits.Count == 0) gm.Toast("No troops yet. Train some at the Barracks");
            Changed();
        }

        public void ClearUnits()
        {
            foreach (var u in SelectedUnits) if (u != null) u.SetSelected(false);
            SelectedUnits.Clear();
            Changed();
        }

        public void RecallUnits()
        {
            foreach (var u in SelectedUnits) if (u != null) u.Order(null);
            gm.Toast("Troops are going home");
            ClearUnits();
        }

        void SendUnits(Vector3 spot)
        {
            SelectedUnits.RemoveAll(u => u == null);
            int n = SelectedUnits.Count;
            // spread them a little so they don't stack on one point
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.4f, r = i == 0 ? 0f : 0.6f + 0.25f * Mathf.Sqrt(i);
                SelectedUnits[i].Order(spot + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }
            DamageEffects.Puff(spot + Vector3.up * 0.1f, 0.5f);
            AudioManager.Play("step", 0.6f);
            gm.Toast("Sent " + n + (n == 1 ? " unit" : " units"));
        }

        void EndBuildingDrag(bool commit)
        {
            var b = Selected;
            if (b == null) return;
            b.SetRaised(false);
            b.SetTint(null);
            if (commit && previewValid && (previewX != b.Data.x || previewZ != b.Data.z))
            {
                gm.Move(b, previewX, previewZ, b.Data.rotation);
                AudioManager.Play("step", 0.6f);
            }
            else
            {
                if (commit && !previewValid) gm.Toast("Can't place it there");
                b.ApplyTransform();
            }
            Changed();
        }

        void UpdateRangeCircle()
        {
            var t = Ghost != null ? Ghost : Selected;
            if (t == null || t.Def.attackRange <= 0f) { RangeCircle.Hide(); return; }
            bool valid = t == Ghost ? GhostValid : (mode != Mode.DragBuilding || previewValid);
            var center = t == Ghost ? t.Center : t.transform.position;
            RangeCircle.Show(center, t.AttackRange, valid);
        }

        /// <summary>Generous hit test for the preview: its cells, one cell around them, or close to it on screen.</summary>
        bool NearGhost(Vector2 screen, Vector2Int cell)
        {
            foreach (var c in Ghost.Cells)
                if (Mathf.Abs(c.x - cell.x) <= 1 && Mathf.Abs(c.y - cell.y) <= 1) return true;
            var size = GridMap.Size(Ghost.Def, Ghost.Data.rotation);
            var center = Ghost.Center + Vector3.up * (Ghost.Def.height * 0.5f + 0.3f);
            var sc = rig.Cam.WorldToScreenPoint(center);
            var edge = rig.Cam.WorldToScreenPoint(center + rig.Cam.transform.right * (Mathf.Max(size.x, size.y) * 0.5f + 1f));
            float radius = Mathf.Max(Vector2.Distance(sc, edge) * 1.4f, Screen.height * 0.09f);
            return sc.z > 0 && Vector2.Distance(sc, screen) <= radius;
        }

        static bool Contains(Building b, Vector2Int cell)
        {
            foreach (var c in b.Cells) if (c == cell) return true;
            return false;
        }
    }
}
