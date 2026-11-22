using System;
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
                        if (gm.CanPlace(def, x, z, 0))
                            return new Vector2Int(x, z);
                    }
            return start;
        }

        public void ConfirmPlacement()
        {
            if (Ghost == null) return;
            var def = Ghost.Def;
            var d = Ghost.Data;
            var placed = gm.PlaceNew(def, d.x, d.z, d.rotation);
            if (placed == null) return;
            // Walls and torches: keep placing the next piece right beside the last one.
            if (def.width == 1 && def.depth == 1 && gm.State.CanAfford(def.cost))
            {
                d.x += 1;
                RefreshGhost();
            }
            else
            {
                CancelPlacement();
                Select(placed);
            }
        }

        public void CancelPlacement()
        {
            if (Ghost != null) Destroy(Ghost.gameObject);
            Ghost = null;
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
        }

        // ---------------- Pointer handling ----------------

        void Update()
        {
            if (rig == null) return;
            rig.Zoom(PointerInput.ZoomDelta);

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

        void PointerDown(Vector2 screen)
        {
            downScreen = lastScreen = screen;
            movedBeyondThreshold = false;
            if (PointerInput.IsOverUI(screen)) { mode = Mode.Ignored; return; }

            Vector3 ground;
            if (!rig.GroundPoint(screen, out ground)) { mode = Mode.Ignored; return; }
            var cell = GridMap.CellOf(ground);

            if (Ghost != null && Contains(Ghost, cell))
            {
                mode = Mode.DragGhost;
                grabOffset = Ghost.Center - ground;
                return;
            }

            var b = gm.Grid.At(cell) as Building;
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
                    Vector3 g;
                    if (!rig.GroundPoint(screen, out g)) break;
                    var o = GridMap.OriginFromCenter(g + grabOffset, Ghost.Def, Ghost.Data.rotation);
                    if (o.x != Ghost.Data.x || o.y != Ghost.Data.z)
                    {
                        Ghost.Data.x = o.x; Ghost.Data.z = o.y;
                        RefreshGhost();
                    }
                    break;
                }
                case Mode.DragBuilding:
                {
                    if (!movedBeyondThreshold || Selected == null) break;
                    Selected.SetRaised(true);
                    Vector3 g;
                    if (!rig.GroundPoint(screen, out g)) break;
                    var o = GridMap.OriginFromCenter(g + grabOffset, Selected.Def, Selected.Data.rotation);
                    if (o.x != previewX || o.y != previewZ)
                    {
                        previewX = o.x; previewZ = o.y;
                        previewValid = gm.CanPlace(Selected.Def, previewX, previewZ, Selected.Data.rotation, Selected);
                        Selected.transform.position = GridMap.FootprintCenter(previewX, previewZ, Selected.Def, Selected.Data.rotation);
                        Selected.SetTint(previewValid ? (Color?)null : InvalidTint);
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
                if (tapped == null && Ghost == null) Deselect();
            }
            mode = Mode.None;
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

        static bool Contains(Building b, Vector2Int cell)
        {
            foreach (var c in b.Cells) if (c == cell) return true;
            return false;
        }
    }
}
