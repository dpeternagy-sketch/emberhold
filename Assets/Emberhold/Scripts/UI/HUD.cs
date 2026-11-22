using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhold
{
    /// <summary>
    /// Minimal screen: a slim resource bar on top, three small buttons (Build, Defenses, Night),
    /// and context buttons that appear next to the building you are moving or placing.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        GameManager gm;
        InteractionController input;
        CameraRig rig;

        Text statusText, toastText;
        RawImage statusIcon;
        readonly Dictionary<string, Text> resourceTexts = new Dictionary<string, Text>();
        float toastUntil;
        readonly Queue<string> toasts = new Queue<string>();

        RectTransform buildPanel, buildRow;
        BuildingCategory? openCategory;
        readonly List<KeyValuePair<Button, BuildingDef>> cards = new List<KeyValuePair<Button, BuildingDef>>();

        Button nightButton;
        RectTransform ghostButtons, selectButtons;
        Button ghostOk;
        RectTransform infoPanel;
        Text infoText;
        Button upgradeButton, removeButton;

        RectTransform settlersPopup;
        Text settlersText;
        RectTransform menuPanel;

        public void Init(GameManager game, InteractionController interaction, CameraRig cameraRig)
        {
            gm = game; input = interaction; rig = cameraRig;
            var canvas = UIKit.CreateCanvas();
            var root = canvas.transform;

            // Top: resource icons with numbers, no background strip
            var res = UIKit.Rect("Resources", root);
            res.anchorMin = new Vector2(0, 1); res.anchorMax = new Vector2(1, 1); res.pivot = new Vector2(0.5f, 1);
            res.anchoredPosition = new Vector2(-60, -50); res.sizeDelta = new Vector2(-160, 90);
            var hl = res.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6; hl.childAlignment = TextAnchor.MiddleLeft; hl.childForceExpandWidth = false; hl.childControlWidth = false; hl.childControlHeight = false;
            foreach (var key in new[] { "food", "wood", "stone", "gold", "weapons", "people" })
                resourceTexts[key] = ResourceChip(res, key);

            var menuBtn = UIKit.IconButton(root, "menu", null, 96, ToggleMenu);
            UIKit.Anchor(menuBtn.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -46), new Vector2(96, 96));

            var status = UIKit.Rect("Status", root);
            UIKit.Anchor(status, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(700, 70));
            statusIcon = UIKit.Icon(status, "day", 64);
            UIKit.Anchor(statusIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(64, 64));
            statusText = UIKit.OutlinedLabel(status, "", 34, TextAnchor.MiddleLeft);
            statusText.rectTransform.offsetMin = new Vector2(120, 0);

            var toast = UIKit.Rect("Toast", root);
            UIKit.Anchor(toast, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(950, 90));
            toastText = UIKit.OutlinedLabel(toast, "", 40, TextAnchor.MiddleCenter);

            // Bottom-right: round icon buttons
            var menu = UIKit.Rect("MainButtons", root);
            UIKit.Anchor(menu, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 40), new Vector2(170, 520));
            var b1 = UIKit.IconButton(menu, "build", "Build", 150, () => TogglePanel(BuildingCategory.Economy));
            UIKit.Anchor(b1.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(150, 190));
            var b2 = UIKit.IconButton(menu, "defense", "Defenses", 150, () => TogglePanel(BuildingCategory.Defense));
            UIKit.Anchor(b2.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(150, 190));
            nightButton = UIKit.IconButton(menu, "night", "Night", 110, () => gm.Cycle.StartNightNow());
            UIKit.Anchor(nightButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(110, 150));

            // Build panel (cards row)
            buildPanel = UIKit.Box("BuildPanel", root, UIKit.Panel).rectTransform;
            buildPanel.anchorMin = new Vector2(0, 0); buildPanel.anchorMax = new Vector2(1, 0); buildPanel.pivot = new Vector2(0.5f, 0);
            buildPanel.anchoredPosition = new Vector2(-130, 40); buildPanel.sizeDelta = new Vector2(-300, 260);
            var scroll = buildPanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false;
            var viewport = UIKit.Rect("Viewport", buildPanel);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(10, 10); viewport.offsetMax = new Vector2(-10, -10);
            viewport.gameObject.AddComponent<RectMask2D>();
            buildRow = UIKit.Rect("Row", viewport);
            buildRow.anchorMin = new Vector2(0, 0); buildRow.anchorMax = new Vector2(0, 1); buildRow.pivot = new Vector2(0, 0.5f);
            buildRow.sizeDelta = new Vector2(0, 0);
            var layout = buildRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; layout.childControlWidth = false; layout.childControlHeight = true;
            buildRow.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = buildRow;
            buildPanel.gameObject.SetActive(false);

            // Floating buttons beside a ghost (OK / Rotate / Cancel)
            ghostButtons = UIKit.Rect("GhostButtons", root);
            ghostButtons.sizeDelta = new Vector2(340, 100);
            ghostOk = UIKit.Button(ghostButtons, "OK", 34, () => input.ConfirmPlacement(), new Color(0.2f, 0.45f, 0.2f, 0.95f));
            UIKit.Anchor(ghostOk.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 100));
            var rot = UIKit.IconButton(ghostButtons, "rotate", null, 100, () => input.RotateCurrent());
            UIKit.Anchor(rot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
            var cancel = UIKit.Button(ghostButtons, "X", 34, () => input.CancelPlacement(), new Color(0.5f, 0.15f, 0.12f, 0.95f));
            UIKit.Anchor(cancel.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(100, 100));

            // Small rotate icon beside the selected building
            selectButtons = UIKit.Rect("SelectButtons", root);
            selectButtons.sizeDelta = new Vector2(90, 90);
            var srot = UIKit.IconButton(selectButtons, "rotate", null, 90, () => input.RotateCurrent());
            var srr = srot.GetComponent<RectTransform>();
            srr.anchorMin = Vector2.zero; srr.anchorMax = Vector2.one; srr.offsetMin = srr.offsetMax = Vector2.zero;

            // Selected building info
            infoPanel = UIKit.Box("InfoPanel", root, UIKit.Panel).rectTransform;
            UIKit.Anchor(infoPanel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 40), new Vector2(740, 230));
            infoText = UIKit.Label(infoPanel, "", 32, TextAnchor.UpperLeft);
            infoText.rectTransform.offsetMin = new Vector2(20, 100); infoText.rectTransform.offsetMax = new Vector2(-20, -16);
            upgradeButton = UIKit.Button(infoPanel, "Upgrade", 28, () => { if (input.Selected != null) gm.TryUpgrade(input.Selected); }, new Color(0.45f, 0.32f, 0.12f, 0.95f));
            UIKit.Anchor(upgradeButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 16), new Vector2(480, 80));
            removeButton = UIKit.Button(infoPanel, "Remove", 28, () => { if (input.Selected != null) { gm.Demolish(input.Selected); input.Deselect(); } }, new Color(0.4f, 0.13f, 0.1f, 0.95f));
            UIKit.Anchor(removeButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 16), new Vector2(200, 80));

            // Settlers popup
            settlersPopup = UIKit.Box("SettlersPopup", root, UIKit.Panel).rectTransform;
            UIKit.Anchor(settlersPopup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(820, 380));
            settlersText = UIKit.Label(settlersPopup, "", 40);
            settlersText.rectTransform.offsetMin = new Vector2(30, 140);
            var accept = UIKit.Button(settlersPopup, "Accept", 36, () => { gm.AnswerSettlers(true); settlersPopup.gameObject.SetActive(false); }, new Color(0.2f, 0.45f, 0.2f, 0.95f));
            UIKit.Anchor(accept.GetComponent<RectTransform>(), new Vector2(0.27f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(330, 100));
            var decline = UIKit.Button(settlersPopup, "Decline", 36, () => { gm.AnswerSettlers(false); settlersPopup.gameObject.SetActive(false); });
            UIKit.Anchor(decline.GetComponent<RectTransform>(), new Vector2(0.73f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(330, 100));
            settlersPopup.gameObject.SetActive(false);

            // Menu
            menuPanel = UIKit.Box("Menu", root, UIKit.Panel).rectTransform;
            UIKit.Anchor(menuPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -170), new Vector2(460, 360));
            var title = UIKit.Label(menuPanel, "Emberhold", 40, TextAnchor.UpperCenter, UIKit.Accent);
            title.rectTransform.offsetMax = new Vector2(-8, -20);
            var reset = UIKit.Button(menuPanel, "Restart city", 30, () => gm.ResetGame(), new Color(0.4f, 0.13f, 0.1f, 0.95f));
            UIKit.Anchor(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(400, 90));
            var close = UIKit.Button(menuPanel, "Close", 30, ToggleMenu);
            UIKit.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(400, 90));
            menuPanel.gameObject.SetActive(false);

            gm.OnToast += ShowToast;
            gm.OnSettlersArrived += ShowSettlers;
            input.OnSelectionChanged += () => { if (input.Ghost != null) buildPanel.gameObject.SetActive(false); };
        }

        static void SetIfChanged(Text t, string v) { if (t.text != v) t.text = v; }

        Text ResourceChip(Transform parent, string icon)
        {
            var chip = UIKit.Rect("Chip_" + icon, parent);
            chip.sizeDelta = new Vector2(icon == "people" ? 190 : 150, 84);
            var ic = UIKit.Icon(chip, icon, 76);
            UIKit.Anchor(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(76, 76));
            var t = UIKit.OutlinedLabel(chip, "0", 38, TextAnchor.MiddleLeft);
            t.rectTransform.offsetMin = new Vector2(76, 0);
            t.rectTransform.offsetMax = new Vector2(0, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void ToggleMenu() { menuPanel.gameObject.SetActive(!menuPanel.gameObject.activeSelf); }

        void TogglePanel(BuildingCategory category)
        {
            if (buildPanel.gameObject.activeSelf && openCategory == category)
            {
                buildPanel.gameObject.SetActive(false);
                return;
            }
            openCategory = category;
            foreach (var kv in cards) Destroy(kv.Key.gameObject);
            cards.Clear();
            foreach (var def in BuildingCatalog.InCategory(category))
            {
                var d = def;
                var card = UIKit.Button(buildRow, d.displayName + "\n<size=24>" + d.cost + "</size>", 32, () => input.BeginPlacing(d));
                card.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 0);
                var le = card.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 250;
                var swatch = UIKit.Box("Swatch", card.transform, d.color);
                swatch.raycastTarget = false;
                UIKit.Anchor(swatch.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(60, 14));
                cards.Add(new KeyValuePair<Button, BuildingDef>(card, d));
            }
            buildPanel.gameObject.SetActive(true);
            input.Deselect();
        }

        void ShowToast(string message)
        {
            toasts.Enqueue(message);
        }

        void ShowSettlers(int count)
        {
            settlersText.text = count == 1
                ? "A settler asks to join your city.\nThey will need food and a home."
                : count + " settlers ask to join your city.\nThey will need food and a home.";
            settlersPopup.gameObject.SetActive(true);
        }

        void Update()
        {
            if (gm == null || gm.State == null) return;
            var s = gm.State;

            SetIfChanged(resourceTexts["food"], s.food.ToString());
            SetIfChanged(resourceTexts["wood"], s.wood.ToString());
            SetIfChanged(resourceTexts["stone"], s.stone.ToString());
            SetIfChanged(resourceTexts["gold"], s.gold.ToString());
            SetIfChanged(resourceTexts["weapons"], s.weapons.ToString());
            SetIfChanged(resourceTexts["people"], s.population + "/" + gm.HousingCapacity);

            var cycle = gm.Cycle;
            if (cycle.IsNight)
            {
                statusText.text = "Night " + (s.night + 1) + "   Enemies: " + Enemy.All.Count;
                statusIcon.texture = UIKit.IconTexture("night");
                nightButton.interactable = false;
            }
            else
            {
                statusText.text = "Day " + (s.night + 1) + "   Night in " + Util.FormatTime(cycle.DayTimeLeft);
                statusIcon.texture = UIKit.IconTexture("day");
                nightButton.interactable = cycle.Phase == Phase.Day;
            }

            // Toasts
            if (Time.time > toastUntil)
            {
                if (toasts.Count > 0) { toastText.text = toasts.Dequeue(); toastUntil = Time.time + 2.5f; }
                else toastText.text = "";
            }

            // Build cards affordability
            foreach (var kv in cards) kv.Key.interactable = s.CanAfford(kv.Value.cost);

            // Ghost buttons follow the ghost
            if (input.Ghost != null)
            {
                ghostButtons.gameObject.SetActive(true);
                Follow(ghostButtons, input.Ghost, -1.2f);
                ghostOk.interactable = input.GhostValid;
            }
            else ghostButtons.gameObject.SetActive(false);

            // Selected building: rotate icon + info
            var sel = input.Selected;
            if (sel != null && input.Ghost == null)
            {
                selectButtons.gameObject.SetActive(sel.Def.Movable);
                if (sel.Def.Movable) FollowSide(selectButtons, sel);
                infoPanel.gameObject.SetActive(true);
                infoText.text = Describe(sel);
                removeButton.gameObject.SetActive(sel.Def.Movable);
                if (sel.Data.IsUpgrading)
                {
                    UIKit.SetText(upgradeButton, "Upgrading... " + Util.FormatTime(sel.UpgradeSecondsLeft));
                    upgradeButton.interactable = false;
                }
                else if (sel.Data.level >= sel.Def.maxLevel)
                {
                    UIKit.SetText(upgradeButton, "Max level");
                    upgradeButton.interactable = false;
                }
                else
                {
                    UIKit.SetText(upgradeButton, "Upgrade: " + sel.UpgradeCost + "  (" + Util.FormatTime(sel.UpgradeDuration) + ")");
                    upgradeButton.interactable = s.CanAfford(sel.UpgradeCost);
                }
            }
            else
            {
                selectButtons.gameObject.SetActive(false);
                infoPanel.gameObject.SetActive(false);
            }
        }

        string Describe(Building b)
        {
            var d = b.Def;
            var text = d.displayName + "  -  Level " + b.Data.level + "\nHP " + Mathf.Max(0, b.Data.hp) + "/" + b.MaxHp;
            if (d.workers > 0) text += "    Workers " + b.AssignedWorkers + "/" + d.workers;
            if (b.Housing > 0) text += "    Homes " + b.Housing;
            if (d.produces != ResourceType.None) text += "\nMakes " + d.produces + " every " + d.produceInterval + "s";
            if (d.id == BuildingCatalog.Castle) text += "\nUpgrade the castle to get more land";
            return text;
        }

        void Follow(RectTransform ui, Building b, float yOffsetWorld)
        {
            var s = GridMap.Size(b.Def, b.Data.rotation);
            var world = b.Center + new Vector3(0, 0, -s.y * 0.5f + yOffsetWorld);
            PlaceAt(ui, world, Vector2.zero);
        }

        void FollowSide(RectTransform ui, Building b)
        {
            var s = GridMap.Size(b.Def, b.Data.rotation);
            var world = b.transform.position + new Vector3(s.x * 0.5f + 0.6f, 0, 0);
            PlaceAt(ui, world, Vector2.zero);
        }

        void PlaceAt(RectTransform ui, Vector3 world, Vector2 pixelOffset)
        {
            var sp = rig.Cam.WorldToScreenPoint(world);
            var canvas = ui.GetComponentInParent<Canvas>();
            ui.anchorMin = ui.anchorMax = Vector2.zero;
            ui.pivot = new Vector2(0.5f, 0.5f);
            ui.anchoredPosition = (new Vector2(sp.x, sp.y) + pixelOffset) / canvas.scaleFactor;
        }
    }
}
