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
        RectTransform toastBg;
        RawImage statusIcon;
        readonly Dictionary<string, Text> resourceTexts = new Dictionary<string, Text>();
        float toastUntil;
        readonly Queue<string> toasts = new Queue<string>();

        RectTransform buildPanel, buildRow;
        BuildingCategory? openCategory;
        readonly List<KeyValuePair<Button, BuildingDef>> cards = new List<KeyValuePair<Button, BuildingDef>>();

        Button nightButton;
        RectTransform ghostButtons, mainButtons;
        Text ghostHint;
        Button ghostOk;
        // Tapped building: name above it, round action buttons at the bottom (like CoC / Boom Beach)
        Text selTitle;
        RectTransform actionBar, actionBack, upgradeCostHolder, repairCostHolder;
        Button infoBtn, upgradeBtn, repairBtn, speedBtn, rotateBtn, removeBtn, trainBtn, wallsBtn;
        WallPanel wallPanel;
        Transform titleRoot;
        bool titleMade;
        ArmyUI armyUI;
        Text speedCaption, removeCaption;
        RectTransform infoPopup, upgradePopup;
        Text infoPopupTitle, infoPopupBody, upPopupTitle, upPopupBody, upPopupTime, upPopupNote;
        RectTransform upPopupCost;
        Button upPopupGo;
        Text upPopupGoText;
        Button infoUpgradeBtn;
        bool goFreesBuilder;
        Text builderText;
        Building costShownFor; int costShownLevel = -1; bool costShownRepair;
        readonly Dictionary<Building, RectTransform> timerBars = new Dictionary<Building, RectTransform>();
        readonly Dictionary<Building, RectTransform> badges = new Dictionary<Building, RectTransform>();
        RectTransform overlayRoot;

        RectTransform settlersPopup;
        Text settlersText;
        RectTransform menuPanel;

        public void Init(GameManager game, InteractionController interaction, CameraRig cameraRig)
        {
            gm = game; input = interaction; rig = cameraRig;
            instance = this;
            var canvas = UIKit.CreateCanvas();
            var screenRoot = canvas.transform;            // for things pinned to the map (follow screen pixels)
            var root = UIKit.SafeArea(screenRoot);         // fixed HUD stays clear of notches and rounded corners

            // Top: resource icons with numbers, no background strip
            var res = UIKit.Rect("Resources", root);
            UIKit.Anchor(res, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -30), new Vector2(3 * 250 + 2 * 14, 2 * 72 + 12));
            var grid = res.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(250, 72); grid.spacing = new Vector2(14, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            foreach (var key in new[] { "food", "water", "energy", "steel", "coins", "people" })
                resourceTexts[key] = ResourceChip(res, key);

            var menuBtn = UIKit.IconButton(root, "icon_menu", null, 84, ToggleMenu, "blue");
            UIKit.Anchor(menuBtn.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(84, 84));

            // Builders: how many upgrades can run at once (busy / total)
            var bld = UIKit.Pill("Builders", root).rectTransform;
            var bldBtn = bld.gameObject.AddComponent<Button>();
            bld.GetComponent<Image>().raycastTarget = true;
            bldBtn.onClick.AddListener(() =>
            {
                var busyB = gm.SoonestUpgrade();
                if (busyB == null) { gm.Toast("Builders are free: upgrade something!"); return; }
                input.Select(busyB);
                rig.FocusOn(busyB.Center);
            });
            UIKit.Anchor(bld, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -150), new Vector2(190, 72));
            var bIc = UIKit.Icon(bld, "k_wrench", 60);
            UIKit.Anchor(bIc.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-6, 2), new Vector2(60, 60));
            builderText = UIKit.OutlinedLabel(bld, "0/1", 34, TextAnchor.MiddleCenter);
            builderText.rectTransform.offsetMin = new Vector2(56, 0);
            builderText.rectTransform.offsetMax = new Vector2(-8, 0);

            var status = UIKit.Rect("Status", root);
            statusRoot = status;
            UIKit.Anchor(status, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -206), new Vector2(560, 70));
            var statusBg = UIKit.Pill("Bg", status);
            statusBg.rectTransform.anchorMin = Vector2.zero; statusBg.rectTransform.anchorMax = Vector2.one;
            statusBg.rectTransform.offsetMin = statusBg.rectTransform.offsetMax = Vector2.zero;
            statusIcon = UIKit.Icon(status, "day", 64);
            var alarm = Resources.Load<Texture2D>("UI/Badges/icon_attack");
            if (alarm != null) statusIcon.texture = alarm;
            UIKit.Anchor(statusIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(64, 64));
            statusText = UIKit.OutlinedLabel(status, "", 30, TextAnchor.MiddleLeft);
            statusText.rectTransform.offsetMin = new Vector2(112, 0);

            var toast = UIKit.Rect("Toast", root);
            UIKit.Anchor(toast, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(960, 96));
            toastBg = UIKit.Pill("Bg", toast).rectTransform;
            UIKit.Anchor(toastBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 96));
            toastText = UIKit.OutlinedLabel(toast, "", 34, TextAnchor.MiddleCenter);

            // Bottom-right: round icon buttons
            var menu = UIKit.Rect("MainButtons", root);
            mainButtons = menu;
            UIKit.Anchor(menu, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 40), new Vector2(130, 470));
            var b1 = UIKit.IconButton(menu, "icon_build", "Build", 108, () => TogglePanel(BuildingCategory.Economy), "green");
            UIKit.Anchor(b1.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(112, 142));
            var b2 = UIKit.IconButton(menu, "icon_defenses", "Defenses", 108, () => TogglePanel(BuildingCategory.Defense), "red");
            UIKit.Anchor(b2.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -156), new Vector2(112, 142));
            var b3 = UIKit.IconButton(menu, "icon_army", "Army", 108, () => TogglePanel(BuildingCategory.Army), "yellow");
            UIKit.Anchor(b3.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -312), new Vector2(112, 142));
            nightButton = UIKit.IconButton(menu, "night", "Night", 110, () => gm.Cycle.StartNightNow(), "blue");
            UIKit.Anchor(nightButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -600), new Vector2(110, 150));
            nightButton.gameObject.SetActive(false);   // Peter: no night mode; dinos just wander in one at a time

            // Build panel (cards row)
            buildPanel = UIKit.PanelBox("BuildPanel", root).rectTransform;
            buildPanel.anchorMin = new Vector2(0, 0); buildPanel.anchorMax = new Vector2(1, 0); buildPanel.pivot = new Vector2(0.5f, 0);
            buildPanel.anchoredPosition = new Vector2(-110, 40); buildPanel.sizeDelta = new Vector2(-260, 270);
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
            ghostButtons = UIKit.Rect("GhostButtons", screenRoot);
            ghostButtons.sizeDelta = new Vector2(420, 110);
            ghostOk = UIKit.SkinButton(ghostButtons, "Build", 32, () => input.ConfirmPlacement(), "green");
            UIKit.Anchor(ghostOk.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(180, 100));
            var rot = UIKit.IconButton(ghostButtons, "icon_rotate", null, 84, () => input.RotateCurrent(), "blue");
            UIKit.Anchor(rot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(84, 84));
            var cancel = UIKit.IconButton(ghostButtons, "icon_close", null, 84, () => input.CancelPlacement(), "red");
            UIKit.Anchor(cancel.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(84, 84));

            ghostHint = UIKit.OutlinedLabel(screenRoot, "", 30, TextAnchor.MiddleCenter);
            ghostHint.rectTransform.anchorMin = ghostHint.rectTransform.anchorMax = Vector2.zero;
            ghostHint.rectTransform.sizeDelta = new Vector2(700, 50);
            ghostHint.horizontalOverflow = HorizontalWrapMode.Overflow;

            BuildSelectionUI(root, screenRoot);
            titleRoot = screenRoot;

            // Settlers popup
            settlersPopup = UIKit.PanelBox("SettlersPopup", root).rectTransform;
            UIKit.Anchor(settlersPopup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(820, 380));
            settlersText = UIKit.Label(settlersPopup, "", 40);
            settlersText.rectTransform.offsetMin = new Vector2(30, 140);
            var accept = UIKit.Button(settlersPopup, "Accept", 36, () => { gm.AnswerSettlers(true); settlersPopup.gameObject.SetActive(false); }, new Color(0.2f, 0.45f, 0.2f, 0.95f));
            UIKit.Anchor(accept.GetComponent<RectTransform>(), new Vector2(0.27f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(330, 100));
            var decline = UIKit.Button(settlersPopup, "Decline", 36, () => { gm.AnswerSettlers(false); settlersPopup.gameObject.SetActive(false); });
            UIKit.Anchor(decline.GetComponent<RectTransform>(), new Vector2(0.73f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(330, 100));
            settlersPopup.gameObject.SetActive(false);

            // Menu
            menuPanel = UIKit.PanelBox("Menu", root).rectTransform;
            UIKit.Anchor(menuPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -170), new Vector2(460, 680));
            Header(menuPanel);
            var title = UIKit.OutlinedLabel(menuPanel, "Mega Fort", 40, TextAnchor.UpperCenter);
            title.rectTransform.offsetMax = new Vector2(-8, -32);
            Button reset = null;
            float resetArmedUntil = -1f;
            reset = UIKit.Button(menuPanel, "Restart city", 30, () =>
            {
                // two taps: a whole city is never lost by one mis-tap
                if (Time.unscaledTime < resetArmedUntil) { gm.ResetGame(); return; }
                resetArmedUntil = Time.unscaledTime + 3f;
                reset.GetComponentInChildren<Text>().text = "Sure? Tap again";
            }, new Color(0.4f, 0.13f, 0.1f, 0.95f));
            UIKit.Anchor(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(400, 90));
            var fx = UIKit.VolumeSlider(menuPanel, "Effects", AudioManager.SoundLevel, v => AudioManager.SoundLevel = v);
            UIKit.Anchor(fx.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(400, 110));
            var amb = UIKit.VolumeSlider(menuPanel, "Nature sounds", AudioManager.AmbienceLevel, v => AudioManager.AmbienceLevel = v);
            UIKit.Anchor(amb.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(400, 110));
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
            var chip = UIKit.Pill("Chip_" + icon, parent).rectTransform;
            var ic = UIKit.Icon(chip, icon, 76);
            UIKit.Anchor(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-14, 2), new Vector2(76, 76));
            var t = UIKit.OutlinedLabel(chip, "0", 34, TextAnchor.MiddleCenter);
            t.rectTransform.offsetMin = new Vector2(62, 0);
            t.rectTransform.offsetMax = new Vector2(-10, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static string SoundLabel()
        {
            float l = AudioManager.SoundLevel;
            return "Sound: " + (l > 0.75f ? "High" : l > 0.25f ? "Low" : "Off");
        }

        /// <summary>Dev preview helpers (PreviewTour).</summary>
        public void PreviewCloseAll()
        {
            buildPanel.gameObject.SetActive(false); menuPanel.gameObject.SetActive(false);
            infoPopup.gameObject.SetActive(false); upgradePopup.gameObject.SetActive(false);
            if (armyUI != null) armyUI.CloseTrain();
        }
        public void PreviewShowTrain(Building b) { armyUI.ShowTrain(b); }
        public void PreviewOpenBuild(BuildingCategory c) { buildPanel.gameObject.SetActive(false); TogglePanel(c); }

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
            costLabels.Clear();
            if (category == BuildingCategory.Defense) WallsCard();
            foreach (var def in BuildingCatalog.InCategory(category))
            {
                var d = def;
                if (d.id == "wall" || d.id == "gate") continue;   // the wall is bought by side (Walls card)
                bool locked = gm.CastleLevel < d.unlockLevel;
                var card = UIKit.Button(buildRow, "", 30, () =>
                {
                    if (!gm.CanOwnMore(d, true)) { AudioManager.Play("ui", 0.5f); return; }
                    input.BeginPlacing(d);
                });
                var cimg = card.GetComponent<Image>();
                var cardShape = UIKit.Shape("grey", "button_square_depth_flat");
                if (cardShape != null) cimg.sprite = cardShape;
                cimg.color = locked ? new Color(0.55f, 0.57f, 0.62f, 1f) : Color.white;
                card.GetComponent<RectTransform>().sizeDelta = new Vector2(230, 0);
                var le = card.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 230;
                // picture of the real building
                var pic = UIKit.Rect("Picture", card.transform);
                pic.anchorMin = new Vector2(0, 1); pic.anchorMax = new Vector2(1, 1); pic.pivot = new Vector2(0.5f, 1);
                pic.anchoredPosition = new Vector2(0, -12); pic.sizeDelta = new Vector2(-24, 140);
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = Thumbnails.Get(d);
                raw.raycastTarget = false;
                raw.uvRect = new Rect(0.07f, 0.12f, 0.86f, 0.62f);
                if (locked) raw.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                if (d.attackRange > 0f)
                {
                    // what it can hit: flyers cross walls, so the player must see which weapons stop them
                    var tag = UIKit.Pill("AirTag", card.transform).rectTransform;
                    tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1); tag.pivot = new Vector2(0.5f, 1);
                    tag.anchoredPosition = new Vector2(0, -10); tag.sizeDelta = new Vector2(196, 36);
                    var tl = UIKit.OutlinedLabel(tag, d.hitsAir ? "Hits air" : "Ground only", 22, TextAnchor.MiddleCenter);
                    tl.color = d.hitsAir ? new Color(0.5f, 1f, 1f) : new Color(1f, 0.75f, 0.5f);
                }
                var name = UIKit.Label(card.transform, d.displayName, 26, TextAnchor.LowerCenter, new Color(0.13f, 0.16f, 0.26f));
                name.rectTransform.offsetMin = new Vector2(4, 48);
                if (locked)
                {
                    var cost = UIKit.Label(card.transform, "HQ level " + d.unlockLevel, 22, TextAnchor.LowerCenter, new Color(0.75f, 0.75f, 0.75f));
                    cost.rectTransform.offsetMin = new Vector2(4, 6);
                    var lockIcon = UIKit.OutlinedLabel(card.transform, "LOCKED", 26, TextAnchor.UpperCenter);
                    lockIcon.rectTransform.offsetMax = new Vector2(0, -60);
                }
                else
                {
                    CostRow(card.transform, d.cost);
                    // how many you have / may have at this HQ level
                    int cap = d.CapAt(gm.CastleLevel);
                    if (cap < int.MaxValue)
                    {
                        var cnt = UIKit.OutlinedLabel(card.transform, gm.CountOf(d) + "/" + cap, 24, TextAnchor.UpperRight);
                        cnt.rectTransform.offsetMin = new Vector2(0, 0); cnt.rectTransform.offsetMax = new Vector2(-14, -8);
                        if (gm.CountOf(d) >= cap) cnt.color = new Color(1f, 0.55f, 0.45f);
                    }
                }
                cards.Add(new KeyValuePair<Button, BuildingDef>(card, d));
            }
            buildPanel.gameObject.SetActive(true);
            input.Deselect();
        }

        /// <summary>First card in Defenses: the wall loop (opens the Walls panel).</summary>
        void WallsCard()
        {
            var wallDef = BuildingCatalog.Get("wall");
            var card = UIKit.Button(buildRow, "", 30, () => { buildPanel.gameObject.SetActive(false); openCategory = null; wallPanel.Show(); });
            var cimg = card.GetComponent<Image>();
            var cardShape = UIKit.Shape("grey", "button_square_depth_flat");
            if (cardShape != null) cimg.sprite = cardShape;
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 230;
            var pic = UIKit.Rect("Picture", card.transform);
            pic.anchorMin = new Vector2(0, 1); pic.anchorMax = new Vector2(1, 1); pic.pivot = new Vector2(0.5f, 1);
            pic.anchoredPosition = new Vector2(0, -12); pic.sizeDelta = new Vector2(-24, 140);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = Thumbnails.Get(wallDef);
            raw.raycastTarget = false;
            raw.uvRect = new Rect(0.07f, 0.12f, 0.86f, 0.62f);
            var name = UIKit.Label(card.transform, "Walls", 26, TextAnchor.LowerCenter, new Color(0.13f, 0.16f, 0.26f));
            name.rectTransform.offsetMin = new Vector2(4, 48);
            int built = 0;
            for (int i = 0; i < 4; i++) if (gm.WallLevel((GameManager.WallSide)i) > 0) built++;
            var sub = UIKit.Label(card.transform, built + " / 4 sides", 22, TextAnchor.LowerCenter, new Color(0.2f, 0.24f, 0.36f));
            sub.rectTransform.offsetMin = new Vector2(4, 12);
            cards.Add(new KeyValuePair<Button, BuildingDef>(card, wallDef));
        }

        public void PreviewOpenWalls(int side) { wallPanel.Show(side); }
        public void PreviewCloseWalls() { wallPanel.Close(); }

        // Every price on a card: an icon and the amount per resource. Amounts you can't pay turn red.
        readonly List<KeyValuePair<Text, KeyValuePair<ResourceType, int>>> costLabels = new List<KeyValuePair<Text, KeyValuePair<ResourceType, int>>>();
        static readonly Color CostOk = new Color(1f, 0.95f, 0.85f), CostShort = new Color(1f, 0.32f, 0.27f);

        void CostRow(Transform card, Cost c)
        {
            var row = UIKit.Rect("Cost", card);
            row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0); row.pivot = new Vector2(0.5f, 0);
            row.anchoredPosition = new Vector2(0, 8); row.sizeDelta = new Vector2(-8, 44);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4; hl.childAlignment = TextAnchor.MiddleCenter; hl.childForceExpandWidth = false; hl.childControlWidth = false; hl.childControlHeight = false;
            var items = new List<KeyValuePair<ResourceType, int>>();
            if (c.steel > 0) items.Add(new KeyValuePair<ResourceType, int>(ResourceType.Steel, c.steel));
            if (c.energy > 0) items.Add(new KeyValuePair<ResourceType, int>(ResourceType.Energy, c.energy));
            if (c.coins > 0) items.Add(new KeyValuePair<ResourceType, int>(ResourceType.Coins, c.coins));
            if (c.food > 0) items.Add(new KeyValuePair<ResourceType, int>(ResourceType.Food, c.food));
            if (items.Count == 0)
            {
                var free = UIKit.OutlinedLabel(row, "Free", 24, TextAnchor.MiddleCenter);
                free.rectTransform.sizeDelta = new Vector2(80, 34);
                return;
            }
            foreach (var it in items)
            {
                var chip = UIKit.Rect("C", row);
                string num = it.Value.ToString();
                chip.sizeDelta = new Vector2(40 + 16 * num.Length + 6, 44);
                var ic = UIKit.Icon(chip, IconOf(it.Key), 40);
                UIKit.Anchor(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(40, 40));
                var t = UIKit.OutlinedLabel(chip, num, 30, TextAnchor.MiddleLeft);
                t.rectTransform.offsetMin = new Vector2(40, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                costLabels.Add(new KeyValuePair<Text, KeyValuePair<ResourceType, int>>(t, it));
            }
        }

        static string IconOf(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Steel: return "steel";
                case ResourceType.Energy: return "energy";
                case ResourceType.Coins: return "coins";
                case ResourceType.Water: return "water";
                default: return "food";
            }
        }

        void RefreshCostColors()
        {
            for (int i = costLabels.Count - 1; i >= 0; i--)
            {
                var kv = costLabels[i];
                if (kv.Key == null) { costLabels.RemoveAt(i); continue; }
                kv.Key.color = gm.State.Get(kv.Value.Key) >= kv.Value.Value ? CostOk : CostShort;
            }
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

        RectTransform statusRoot;
        static HUD instance;
        public static bool BuildMenuOpen { get { return instance != null && instance.buildPanel != null && instance.buildPanel.gameObject.activeSelf; } }

        void Update()
        {
            if (!titleMade && titleRoot != null) { titleMade = true; TitleScreen.Create(titleRoot); }
            if (gm == null || gm.State == null) return;
            var s = gm.State;

            SetIfChanged(resourceTexts["food"], s.food.ToString());
            SetIfChanged(resourceTexts["water"], s.water.ToString());
            SetIfChanged(resourceTexts["energy"], s.energy.ToString());
            SetIfChanged(resourceTexts["steel"], s.steel.ToString());
            SetIfChanged(resourceTexts["coins"], s.coins.ToString());
            SetIfChanged(resourceTexts["people"], s.population + "/" + gm.HousingCapacity);
            int busy = gm.BuildersBusy, slots = gm.BuilderSlots;
            SetIfChanged(builderText, (slots - busy) + "/" + slots);
            builderText.color = busy >= slots ? new Color(1f, 0.55f, 0.45f) : Color.white;

            // the status pill only appears while something is attacking (no night countdown any more)
            int foes = Enemy.All.Count;
            statusRoot.gameObject.SetActive(foes > 0);
            if (foes > 0) SetIfChanged(statusText, foes == 1 ? "A dino is attacking!" : foes + " attackers");

            // Toasts
            if (Time.time > toastUntil)
            {
                while (toasts.Count > 2) toasts.Dequeue();   // never a backlog of stale toasts
                if (toasts.Count > 0) { toastText.text = toasts.Dequeue(); toastUntil = Time.time + 2.5f; }
                else toastText.text = "";
                toastBg.gameObject.SetActive(toastText.text.Length > 0);
                toastBg.sizeDelta = new Vector2(Mathf.Min(980f, toastText.preferredWidth + 70f), Mathf.Min(2, Mathf.CeilToInt(toastText.preferredWidth / 900f)) * 46f + 30f);
            }

            // Build cards affordability
            RefreshCostColors();
            foreach (var kv in cards)
            {
                bool ok = s.CanAfford(kv.Value.cost) && gm.CastleLevel >= kv.Value.unlockLevel && gm.CountOf(kv.Value) < kv.Value.CapAt(gm.CastleLevel);
                var cg = kv.Key.GetComponent<CanvasGroup>();
                if (cg == null) cg = kv.Key.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = ok ? 1f : 0.6f;
            }

            // Ghost buttons follow the ghost
            if (input.Ghost != null)
            {
                ghostButtons.gameObject.SetActive(true);
                Follow(ghostButtons, input.Ghost, -1.2f);
                ghostOk.interactable = true; // pressing it explains what is missing
                var g = input.Ghost;
                var img = ghostOk.GetComponent<Image>();
                if (img != null) img.color = input.GhostValid ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
                UIKit.SetText(ghostOk, input.LineCount > 1 ? "Build x" + input.LineValidCount : "Build");
                ghostHint.gameObject.SetActive(true);
                ghostHint.text = GhostHintText(g);
                var sp = rig.Cam.WorldToScreenPoint(g.Center + Vector3.up * (g.Def.height + 1.2f));
                ghostHint.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / ghostHint.canvas.scaleFactor;
            }
            else { ghostButtons.gameObject.SetActive(false); ghostHint.gameObject.SetActive(false); }

            UpdateSelectionUI();
            UpdateTimerBars();
            UpdateUpgradeBadges();
        }

        string GhostHintText(Building g)
        {
            var d = g.Def;
            if (!gm.State.CanAfford(d.cost)) return "Need more " + gm.State.Missing(d.cost);
            if (d.nearMountain && !GameManager.TouchesMountain(d, g.Data.x, g.Data.z, g.Data.rotation)) return "Build it against the mountain";
            if (!input.GhostValid) return "No room here - drag it to free ground";
            if (d.id == "canal") return input.GhostHasWater ? "Water will flow here" : "Dry - connect to the river or a well";
            if (InteractionController.IsLineDef(d)) return input.LineCount > 1 ? input.LineValidCount + " pieces - " + d.cost.Scaled(input.LineValidCount) : "Tap where the line should end";
            return "";
        }

        // ---------------- Tapped building (CoC / Boom Beach style) ----------------

        Building popupFor;

        void BuildSelectionUI(Transform root, Transform screenRoot)
        {
            // world-following bits (timer bars, upgrade arrows) sit under every other control
            overlayRoot = UIKit.Rect("MapOverlay", screenRoot);
            overlayRoot.anchorMin = Vector2.zero; overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = overlayRoot.offsetMax = Vector2.zero;
            overlayRoot.SetAsFirstSibling();

            selTitle = UIKit.OutlinedLabel(screenRoot, "", 36, TextAnchor.MiddleCenter);
            selTitle.rectTransform.sizeDelta = new Vector2(700, 60);
            selTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

            actionBar = UIKit.Rect("ActionBar", root);
            UIKit.Anchor(actionBar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(900, 170));
            // dark see-through strip behind the glass buttons so they stay readable over bright building art
            actionBack = UIKit.Pill("ActionBack", actionBar).rectTransform;

            infoBtn = UIKit.IconButton(actionBar, "icon_info", "Info", 96, () => ShowInfo(input.Selected), "blue");
            upgradeBtn = UIKit.IconButton(actionBar, "icon_upgrade", "Upgrade", 96, () => ShowUpgrade(input.Selected), "green");
            repairBtn = UIKit.IconButton(actionBar, "icon_repair_all", "Repair", 96, OnRepairPressed, "yellow");
            speedBtn = UIKit.IconButton(actionBar, "icon_finish", "Finish", 96, SpeedUp, "yellow");
            speedCaption = speedBtn.GetComponentInChildren<Text>();
            rotateBtn = UIKit.IconButton(actionBar, "icon_rotate", "Rotate", 96, () => input.RotateCurrent(), "blue");
            removeBtn = UIKit.IconButton(actionBar, "icon_remove", "Remove", 96, OnRemovePressed, "red");
            removeCaption = removeBtn.GetComponentInChildren<Text>();
            armyUI = gameObject.AddComponent<ArmyUI>();
            armyUI.Init(gm, input, rig, root);
            trainBtn = UIKit.IconButton(actionBar, "icon_army", "Train", 96, () => armyUI.ShowTrain(input.Selected), "green");
            wallPanel = gameObject.AddComponent<WallPanel>();
            wallPanel.Init(gm, root);
            // a wall piece, gate or wall gun opens the Walls panel with its side picked (sides upgrade as a whole)
            wallsBtn = UIKit.IconButton(actionBar, "icon_upgrade", "Walls", 96, () => { var b = input.Selected; wallPanel.Show(gm.WallSideOf(b)); }, "green");

            // cost chips under the Upgrade / Repair buttons
            upgradeCostHolder = CostHolder(upgradeBtn);
            repairCostHolder = CostHolder(repairBtn);

            // Info popup: name, level and two or three key numbers
            infoPopup = UIKit.PanelBox("InfoPopup", root).rectTransform;
            Header(infoPopup);
            UIKit.Anchor(infoPopup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(740, 640));
            infoPopupTitle = UIKit.OutlinedLabel(infoPopup, "", 40, TextAnchor.UpperCenter);
            infoPopupTitle.rectTransform.offsetMax = new Vector2(-90, -32);
            infoPopupBody = UIKit.Label(infoPopup, "", 36, TextAnchor.MiddleCenter);
            infoPopupBody.rectTransform.offsetMin = new Vector2(30, 140); infoPopupBody.rectTransform.offsetMax = new Vector2(-30, -110);
            infoPopupBody.supportRichText = true;
            infoUpgradeBtn = UIKit.SkinButton(infoPopup, "Upgrade", 36, () => { var b = popupFor; if (b != null) ShowUpgrade(b); }, "green");
            UIKit.Anchor(infoUpgradeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(360, 96));
            CloseX(infoPopup, () => infoPopup.gameObject.SetActive(false));
            infoPopup.gameObject.SetActive(false);

            // Upgrade popup: what gets better, the cost, the time, and one big button
            upgradePopup = UIKit.PanelBox("UpgradePopup", root).rectTransform;
            Header(upgradePopup);
            UIKit.Anchor(upgradePopup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(760, 600));
            upPopupTitle = UIKit.OutlinedLabel(upgradePopup, "", 40, TextAnchor.UpperCenter);
            upPopupTitle.rectTransform.offsetMax = new Vector2(-90, -32);
            upPopupBody = UIKit.Label(upgradePopup, "", 36, TextAnchor.UpperCenter);
            upPopupBody.fontSize = 38;
            upPopupBody.rectTransform.offsetMin = new Vector2(30, 300); upPopupBody.rectTransform.offsetMax = new Vector2(-30, -120);
            upPopupCost = UIKit.Rect("CostLine", upgradePopup);
            UIKit.Anchor(upPopupCost, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(600, 44));
            upPopupTime = UIKit.Label(upgradePopup, "", 32, TextAnchor.MiddleCenter);
            UIKit.Anchor(upPopupTime.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(600, 40));
            upPopupNote = UIKit.Label(upgradePopup, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.4f));
            UIKit.Anchor(upPopupNote.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(720, 40));
            upPopupGo = UIKit.Button(upgradePopup, "Upgrade", 40, () =>
            {
                var b = popupFor;
                if (b == null) return;
                if (goFreesBuilder)
                {
                    // All builders busy: pay coins to finish the upgrade that ends first, then this one can start.
                    var other = gm.SoonestUpgrade();
                    if (other == null) return;
                    int c = SpeedUpCost(other);
                    if (gm.State.coins < c) { gm.Toast("Need " + c + " coins"); return; }
                    gm.State.coins -= c;
                    AudioManager.Play("coin", 0.7f);
                    other.FinishUpgradeNow();
                    RefreshUpgradePopup(b);
                    return;
                }
                if (gm.TryUpgrade(b)) upgradePopup.gameObject.SetActive(false);
            }, new Color(0.2f, 0.55f, 0.22f, 0.97f));
            UIKit.Anchor(upPopupGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(420, 96));
            upPopupGoText = upPopupGo.GetComponentInChildren<Text>();
            CloseX(upgradePopup, () => upgradePopup.gameObject.SetActive(false));
            upgradePopup.gameObject.SetActive(false);
        }

        static RectTransform CostHolder(Button b)
        {
            var h = UIKit.Rect("Cost", b.transform);
            UIKit.Anchor(h, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(300, 40));
            return h;
        }

        /// <summary>Blue title strip across the top of a popup (the title label goes on top of it).</summary>
        static void Header(RectTransform panel)
        {
            var h = UIKit.Skin("Header", panel, "blue", "button_rectangle_depth_flat").rectTransform;
            h.anchorMin = new Vector2(0, 1); h.anchorMax = new Vector2(1, 1); h.pivot = new Vector2(0.5f, 1);
            h.anchoredPosition = new Vector2(0, -10); h.sizeDelta = new Vector2(-20, 96);
        }

        void CloseX(RectTransform panel, System.Action onClose)
        {
            var x = UIKit.IconButton(panel, "icon_close", null, 72, onClose, "red");
            UIKit.Anchor(x.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-18, -18), new Vector2(84, 84));
        }

        string Title(Building b)
        {
            if (b.Def.maxLevel <= 1) return b.Def.displayName;
            return b.Def.displayName + "  Lv " + b.Data.level;
        }

        void UpdateSelectionUI()
        {
            var sel = input.Selected;
            bool show = sel != null && input.Ghost == null;
            actionBar.gameObject.SetActive(show);
            selTitle.gameObject.SetActive(show);
            mainButtons.gameObject.SetActive(!show);
            if (show && buildPanel.gameObject.activeSelf) { buildPanel.gameObject.SetActive(false); openCategory = null; }
            if (!show || popupFor != sel)
            {
                infoPopup.gameObject.SetActive(false);
                upgradePopup.gameObject.SetActive(false);
            }
            if (!show) { costShownFor = null; return; }

            // the popup already names the building; a floating title would sit on top of it
            selTitle.gameObject.SetActive(!infoPopup.gameObject.activeSelf && !upgradePopup.gameObject.activeSelf && (armyUI == null || !armyUI.TrainOpen));
            selTitle.text = sel.Ruined ? sel.Def.displayName + "  (ruined)" : Title(sel);
            var top = sel.transform.position + Vector3.up * (sel.Def.height + 1.0f);
            PlaceAt(selTitle.rectTransform, top, new Vector2(0, 30));
            ClampOnScreen(selTitle.rectTransform, selTitle.preferredWidth + 20f, 60f);

            bool broken = sel.Ruined || (sel.Damaged && !sel.UnderConstruction);
            bool upgrading = sel.Data.IsUpgrading;
            bool onWall = gm.WallSideOf(sel) >= 0 && (sel.Def.id == "wall" || sel.Def.id == "gate" || sel.Def.id == "wallgun");
            bool canLevel = !broken && !upgrading && sel.Data.level < sel.Def.maxLevel && !onWall;
            wallsBtn.gameObject.SetActive(onWall);
            infoBtn.gameObject.SetActive(!sel.Ruined);
            trainBtn.gameObject.SetActive(!sel.Ruined && !sel.UnderConstruction && sel.Def.trains != null);
            repairBtn.gameObject.SetActive(broken);
            if (broken) repairBtn.GetComponentInChildren<Text>().text = sel.Ruined ? "Rebuild" : "Repair";
            upgradeBtn.gameObject.SetActive(canLevel);
            speedBtn.gameObject.SetActive(upgrading && !broken);
            if (upgrading) speedCaption.text = "Finish  " + SpeedUpCost(sel);
            rotateBtn.gameObject.SetActive(sel.Def.Movable && !sel.Ruined && sel.Def.width != sel.Def.depth);
            removeBtn.gameObject.SetActive(sel.Def.Movable);
            bool confirming = removeArmedFor == sel && Time.unscaledTime < removeArmedUntil;
            removeCaption.text = confirming ? "Sure?" : sel.Ruined ? "Clear" : sel.Def.removeLabel;
            LayoutActions();

            // cost under the Upgrade / Repair button
            int costKey = broken ? -1 - sel.Data.hp : sel.Data.level;
            if ((broken || canLevel) && (costShownFor != sel || costShownLevel != costKey || costShownRepair != broken))
            {
                costShownFor = sel; costShownLevel = costKey; costShownRepair = broken;
                foreach (Transform c in repairCostHolder) Destroy(c.gameObject);
                foreach (Transform c in upgradeCostHolder) Destroy(c.gameObject);
                // no chips on action buttons (U6): the first Repair tap says the price, the second pays
            }

            if (infoPopup.gameObject.activeSelf) infoPopupBody.text = InfoText(sel);
            if (upgradePopup.gameObject.activeSelf) RefreshUpgradePopup(sel);
        }

        /// <summary>Lines up the visible action buttons in the middle of the bar.</summary>
        void LayoutActions()
        {
            var list = new List<RectTransform>();
            foreach (var b in new[] { trainBtn, wallsBtn, infoBtn, upgradeBtn, repairBtn, speedBtn, rotateBtn, removeBtn })
                if (b.gameObject.activeSelf) list.Add(b.GetComponent<RectTransform>());
            const float step = 140f;
            actionBack.anchorMin = actionBack.anchorMax = new Vector2(0.5f, 1f);
            actionBack.pivot = new Vector2(0.5f, 1f);
            actionBack.anchoredPosition = new Vector2(0f, 12f);
            actionBack.sizeDelta = new Vector2(list.Count * step + 24f, 176f);
            actionBack.gameObject.SetActive(list.Count > 0);
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2((i - (list.Count - 1) * 0.5f) * step, 0f);
            }
        }

        public void ShowInfo(Building b)
        {
            if (b == null) return;
            popupFor = b;
            upgradePopup.gameObject.SetActive(false);
            infoPopupTitle.text = Title(b);
            infoPopupBody.text = InfoText(b);
            infoUpgradeBtn.gameObject.SetActive(!b.Ruined && !b.Data.IsUpgrading && b.Data.level < b.Def.maxLevel && gm.WallSideOf(b) < 0);
            infoPopup.gameObject.SetActive(true);
        }

        /// <summary>How many people live in this house (people fill homes in build order).</summary>
        int Residents(Building house)
        {
            int left = gm.State.population;
            foreach (var b in Building.All)
            {
                if (b.IsGhost || b.Housing <= 0) continue;
                int here = Mathf.Min(left, b.Housing);
                if (b == house) return here;
                left -= here;
            }
            return 0;
        }

        /// <summary>Level, strength, output and the people: one short row each.</summary>
        string InfoText(Building b)
        {
            var d = b.Def;
            var lines = new List<string>();
            if (d.maxLevel > 1) lines.Add("Level  " + b.Data.level + " / " + d.maxLevel + (b.Data.level >= d.maxLevel ? "  (max)" : ""));
            if (d.indestructible) lines.Add("Can't be broken");
            else if (d.maxHp < 9999) lines.Add("Health  " + Mathf.Max(0, b.Data.hp) + " / " + b.MaxHp);
            if (d.attackRange > 0f)
            {
                lines.Add("Damage  " + b.AttackDamage + "     Range  " + b.AttackRange.ToString("0"));
                lines.Add(d.hitsAir ? "Hits ground + air" : "Hits ground only");
            }
            if (d.produces != ResourceType.None && d.produces != ResourceType.Coins)
            {
                int amount = Mathf.RoundToInt(d.produceAmount * (1f + 0.5f * (b.Data.level - 1)));
                lines.Add("Makes  " + Mathf.RoundToInt(amount * 60f / d.produceInterval) + " " + d.produces.ToString().ToLower() + " / min");
            }
            if (d.housing > 0) lines.Add("Residents  " + Residents(b) + " / " + b.Housing);
            if (d.id == "house") lines.Add("Pays " + Mathf.RoundToInt(d.produceAmount * (1f + 0.5f * (b.Data.level - 1)) * 60f / d.produceInterval) + " coins / min");
            if (d.workers > 0)
            {
                int missing = d.workers - b.AssignedWorkers;
                lines.Add("Workers  " + b.AssignedWorkers + " / " + d.workers);
                if (missing > 0) lines.Add("<color=#FF6A5A>Needs " + missing + " more worker" + (missing > 1 ? "s" : "") + "</color>");
            }
            if (d.penCapacity > 0) lines.Add("Tamed dinos  " + Mathf.Min(gm.State.tamedRaptors + gm.State.tamedTrikes, d.penCapacity * b.Data.level) + " / " + d.penCapacity * b.Data.level);
            if (d.trap) lines.Add(Time.time < b.trapReadyAt ? "Resetting..." : "Ready to catch");
            if (d.id == "canal") lines.Add(b.HasWater ? "Water is flowing" : "Dry: connect to the river or a pump");
            if ((d.id == "farm" || d.id == "well") && b.HasWater) lines.Add("Next to water  +75%");
            if (d.campSpace > 0 && gm.Troops != null) lines.Add("Army  " + gm.Troops.Used + " / " + gm.Troops.Capacity + " space");
            if (d.trains != null && gm.Troops != null)
            {
                var q = Army.Queue(b);
                lines.Add(q.Count == 0 ? "Training: nothing" : "Training  " + q.Count + "  (next in " + Mathf.CeilToInt(gm.Troops.SecondsLeft(b)) + "s)");
            }
            if (d.commandCenter && gm.Troops != null) lines.Add("Commander  Lv " + gm.State.heroLevel + "   XP " + gm.State.heroXp);
            if (b.Starved) lines.Add("<color=#FF6A5A>Needs energy!</color>");
            if (lines.Count > 6) lines.RemoveRange(6, lines.Count - 6);
            return string.Join("\n", lines.ToArray());
        }

        public void ShowUpgrade(Building b)
        {
            if (b == null) return;
            popupFor = b;
            infoPopup.gameObject.SetActive(false);
            foreach (Transform c in upPopupCost) Destroy(c.gameObject);
            CostRow(upPopupCost, b.UpgradeCost);
            upPopupCost.localScale = Vector3.one * 1.4f;
            RefreshUpgradePopup(b);
            upgradePopup.gameObject.SetActive(true);
        }

        void RefreshUpgradePopup(Building b)
        {
            var d = b.Def;
            int L = b.Data.level, N = L + 1;
            upPopupTitle.text = d.displayName + "  Lv " + L + "  >  " + N;
            var lines = new List<string>();
            if (d.maxHp < 9999 && !d.indestructible) lines.Add(Gain("Health", b.MaxHp, Mathf.RoundToInt(d.maxHp * (1f + 0.5f * L))));
            if (d.attackRange > 0f) lines.Add(Gain("Damage", b.AttackDamage, Mathf.RoundToInt(d.attackDamage * (1f + 0.4f * L))));
            if (d.produces != ResourceType.None)
            {
                int now = Mathf.RoundToInt(d.produceAmount * (1f + 0.5f * (L - 1)) * 60f / d.produceInterval);
                int next = Mathf.RoundToInt(d.produceAmount * (1f + 0.5f * L) * 60f / d.produceInterval);
                lines.Add(Gain(d.produces.ToString() + " / min", now, next));
            }
            if (d.housing > 0) lines.Add(Gain("Homes", d.housing * L, d.housing * N));
            if (d.penCapacity > 0) lines.Add(Gain("Dinos", d.penCapacity * L, d.penCapacity * N));
            if (d.id == BuildingCatalog.Castle) lines.Add("More land and new buildings");
            if (lines.Count > 3) lines.RemoveRange(3, lines.Count - 3);
            upPopupBody.supportRichText = true;
            upPopupBody.text = string.Join("\n", lines.ToArray());
            upPopupTime.text = "Time  " + Util.FormatTime(b.UpgradeDuration);
            string block = null;
            if (b != gm.Castle && L >= gm.CastleLevel + 1) block = "Upgrade the HQ first";
            else if (!gm.State.CanAfford(b.UpgradeCost)) block = "Need more " + gm.State.Missing(b.UpgradeCost);
            goFreesBuilder = false;
            if (block == null && !gm.BuilderFree)
            {
                var other = gm.SoonestUpgrade();
                block = "All builders busy (" + gm.BuilderSlots + "/" + gm.BuilderSlots + ")"
                        + (other != null ? "  -  free in " + Util.FormatTime(other.UpgradeSecondsLeft) : "");
                goFreesBuilder = other != null;
            }
            upPopupNote.text = block ?? "";
            upPopupGo.interactable = block == null || goFreesBuilder;
            if (upPopupGoText != null)
                upPopupGoText.text = goFreesBuilder ? "Free builder  " + SpeedUpCost(gm.SoonestUpgrade()) + " coins" : "Upgrade";
        }

        static string Gain(string label, int now, int next)
        {
            return label + "  " + now + "  <color=#7CFF7A>+" + (next - now) + "</color>";
        }

        /// <summary>Finishing an upgrade early costs coins (later: gems or a rewarded ad).</summary>
        static int SpeedUpCost(Building b) { return Mathf.Max(1, Mathf.CeilToInt(b.UpgradeSecondsLeft / 20f)); }

        void SpeedUp()
        {
            var b = input.Selected;
            if (b == null || !b.Data.IsUpgrading) return;
            int cost = SpeedUpCost(b);
            if (gm.State.coins < cost) { gm.Toast("Need " + cost + " coins"); return; }
            gm.State.coins -= cost;
            AudioManager.Play("coin", 0.7f);
            b.FinishUpgradeNow();
        }

        /// <summary>A progress bar with the time left above every building that is upgrading.</summary>
        void UpdateTimerBars()
        {
            var seen = new HashSet<Building>();
            foreach (var b in Building.All)
            {
                if (b.IsGhost || !b.Data.IsUpgrading) continue;
                seen.Add(b);
                RectTransform bar;
                if (!timerBars.TryGetValue(b, out bar) || bar == null)
                {
                    bar = UIKit.Box("TimerBar", overlayRoot, new Color(0.05f, 0.05f, 0.05f, 0.85f)).rectTransform;
                    bar.sizeDelta = new Vector2(190, 34);
                    var fill = UIKit.Box("Fill", bar, new Color(0.35f, 0.8f, 0.3f, 1f)).rectTransform;
                    fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, 0.5f);
                    fill.offsetMin = new Vector2(3, 3); fill.offsetMax = new Vector2(3, -3);
                    var t = UIKit.OutlinedLabel(bar, "", 24, TextAnchor.MiddleCenter);
                    t.name = "T";
                    timerBars[b] = bar;
                }
                float total = b.UpgradeDuration > 0f ? b.Def.upgradeSeconds * b.Data.level * b.Data.level : 1f;
                float frac = 1f - Mathf.Clamp01(b.UpgradeSecondsLeft / Mathf.Max(1f, total));
                var f = (RectTransform)bar.Find("Fill");
                f.sizeDelta = new Vector2((bar.sizeDelta.x - 6f) * frac, f.sizeDelta.y);
                bar.Find("T").GetComponent<Text>().text = Util.FormatTime(b.UpgradeSecondsLeft);
                PlaceAt(bar, b.transform.position + Vector3.up * (b.Def.height + 0.6f), Vector2.zero);
            }
            var gone = new List<Building>();
            foreach (var kv in timerBars) if (kv.Key == null || !seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var b in gone) { if (timerBars[b] != null) Destroy(timerBars[b].gameObject); timerBars.Remove(b); }
        }

        class MapBadge { public RectTransform root, level, status; public Text levelText; public RawImage statusIcon; public Image statusBg; public string statusKind; }
        readonly Dictionary<Building, MapBadge> mapBadges = new Dictionary<Building, MapBadge>();

        /// <summary>
        /// Every levelled building always shows its level (a small blue badge at its front corner) and at most
        /// one status icon above it, in priority order: needs workers > no energy > upgrade ready.
        /// Tapping either one selects the building and opens the matching popup.
        /// </summary>
        void UpdateUpgradeBadges()
        {
            var seen = new HashSet<Building>();
            bool farZoom = rig.distance > 96f;   // only the widest zoom hides them
            bool night = (gm.Cycle != null && gm.Cycle.IsNight) || Enemy.All.Count > 0;   // in a fight the map shows the fight, not the to-do list
            bool zoomedIn = rig.distance < 62f;
            // green upgrade arrows: by day, with a free builder, on the 3 cheapest upgrades only (no clutter)
            var arrows = new HashSet<Building>();
            if (!night && gm.BuilderFree)
            {
                var cands = new List<Building>();
                foreach (var b in Building.All)
                {
                    var d = b.Def;
                    if (b.IsGhost || b.Ruined || b.UnderConstruction || b.Data.IsUpgrading || d.maxLevel <= 1 || b.Data.level >= d.maxLevel) continue;
                    if (b != gm.Castle && b.Data.level >= gm.CastleLevel + 1) continue;
                    if (!gm.State.CanAfford(b.UpgradeCost)) continue;
                    cands.Add(b);
                }
                cands.Sort((a, c) => (a == gm.Castle ? -1000000 : CostWeight(a.UpgradeCost)).CompareTo(c == gm.Castle ? -1000000 : CostWeight(c.UpgradeCost)));
                for (int i = 0; i < cands.Count && i < 3; i++) arrows.Add(cands[i]);
            }
            foreach (var b in Building.All)
            {
                if (b.IsGhost || b.Ruined || b.UnderConstruction || farZoom) continue;
                var d = b.Def;
                bool levelled = d.maxLevel > 1;
                string status = null;
                if (!b.Data.IsUpgrading)
                {
                    if (d.workers > 0 && b.AssignedWorkers < d.workers) status = "workers";
                    else if (b.Starved) status = "energy";
                    // Peter: no upgrade arrows on the map; the Upgrade button shows when you tap the building
                }
                if (!levelled && status == null) continue;
                seen.Add(b);
                MapBadge mb;
                if (!mapBadges.TryGetValue(b, out mb) || mb.root == null) { mb = NewMapBadge(b); mapBadges[b] = mb; }

                bool selected = b == input.Selected;
                // level badge at the front corner of the footprint
                // Peter: levels show only on tap (the title says "House Lv 2"), never as floating numbers on the map
                mb.level.gameObject.SetActive(false);
                if (levelled)
                {
                    SetIfChanged(mb.levelText, b.Data.level.ToString());
                    var sz = GridMap.Size(d, b.Data.rotation);
                    PlaceAt(mb.level, b.Center + new Vector3(-sz.x * 0.5f + 0.2f, 0.15f, -sz.y * 0.5f + 0.1f), Vector2.zero);
                }
                // one status icon above the building (the timer bar takes this spot while upgrading)
                mb.status.gameObject.SetActive(status != null && !selected);
                if (status != null && mb.statusKind != status)
                {
                    mb.statusKind = status;
                    mb.statusIcon.texture = UIKit.IconTexture(status == "workers" ? "people" : status == "energy" ? "energy" : "k_up");
                    mb.statusBg.sprite = UIKit.Shape(status == "upgrade" ? "green" : "red", "button_round_depth_gloss");
                }
                if (status != null)
                {
                    float bob = Mathf.Sin(Time.time * 3f + b.Data.x) * 4f;
                    PlaceAt(mb.status, b.transform.position + Vector3.up * (d.height + 0.4f), new Vector2(0, bob));
                }
            }
            var gone = new List<Building>();
            foreach (var kv in mapBadges) if (kv.Key == null || !seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var b in gone) { var mb = mapBadges[b]; if (mb.root != null) Destroy(mb.root.gameObject); mapBadges.Remove(b); }
        }

        Building repairArmedFor; float repairArmedUntil;
        void OnRepairPressed()
        {
            var b = input.Selected;
            if (b == null) return;
            if (repairArmedFor == b && Time.unscaledTime < repairArmedUntil) { repairArmedFor = null; gm.Repair(b); return; }
            repairArmedFor = b; repairArmedUntil = Time.unscaledTime + 3f;
            gm.Toast((b.Ruined ? "Rebuild" : "Repair") + " costs " + gm.RepairCost(b) + ". Tap again");
        }

        static int CostWeight(Cost c) { return c.steel + c.energy + c.coins + c.food; }

        MapBadge NewMapBadge(Building b)
        {
            var mb = new MapBadge();
            mb.root = UIKit.Rect("Badge", overlayRoot);
            mb.root.anchorMin = mb.root.anchorMax = Vector2.zero; mb.root.sizeDelta = Vector2.zero;

            var lvlBtn = UIKit.IconButton(overlayRoot, "k_info", null, 36, () => { input.Select(b); ShowInfo(b); }, "blue");
            mb.level = lvlBtn.GetComponent<RectTransform>();
            mb.level.SetParent(mb.root, false);
            foreach (var raw in mb.level.GetComponentsInChildren<RawImage>()) raw.enabled = false;   // number instead of the icon
            mb.levelText = UIKit.OutlinedLabel(mb.level, "1", 22, TextAnchor.MiddleCenter);
            mb.level.gameObject.AddComponent<CanvasGroup>().alpha = 0.7f;   // small and see-through so the city stays readable
            mb.levelText.rectTransform.offsetMin = mb.levelText.rectTransform.offsetMax = Vector2.zero;

            var stBtn = UIKit.IconButton(overlayRoot, "k_up", null, 46, () =>
            {
                input.Select(b);
                if (mb.statusKind == "upgrade") ShowUpgrade(b); else ShowInfo(b);
            }, "green");
            mb.status = stBtn.GetComponent<RectTransform>();
            mb.status.SetParent(mb.root, false);
            mb.statusIcon = mb.status.GetComponentInChildren<RawImage>();
            mb.statusBg = stBtn.GetComponent<Image>();
            mb.statusKind = "upgrade";
            mb.status.gameObject.AddComponent<CanvasGroup>().alpha = 0.8f;
            return mb;
        }

        Building removeArmedFor;
        float removeArmedUntil;

        /// <summary>Remove needs two taps ("Remove" then "Sure?") so nothing is removed by mistake.</summary>
        void OnRemovePressed()
        {
            var sel = input.Selected;
            if (sel == null) return;
            if (removeArmedFor == sel && Time.unscaledTime < removeArmedUntil)
            {
                removeArmedFor = null;
                gm.Demolish(sel);
                input.Deselect();
                return;
            }
            removeArmedFor = sel;
            removeArmedUntil = Time.unscaledTime + 3f;
            AudioManager.Play("ui", 0.5f);
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

        /// <summary>Keeps a floating label inside the safe area, below the resource bar (QC: clipped HQ title).</summary>
        void ClampOnScreen(RectTransform ui, float width, float height)
        {
            var canvas = ui.GetComponentInParent<Canvas>();
            var safe = Screen.safeArea;
            float k = canvas.scaleFactor;
            float minX = safe.xMin / k + width * 0.5f + 16f, maxX = safe.xMax / k - width * 0.5f - 16f;
            float minY = safe.yMin / k + height * 0.5f + 16f, maxY = safe.yMax / k - 440f;   // under the resource bar and Commander row
            var p = ui.anchoredPosition;
            p.x = minX > maxX ? (minX + maxX) * 0.5f : Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Clamp(p.y, minY, Mathf.Max(minY, maxY));
            ui.anchoredPosition = p;
        }

        public void PreviewClearToasts() { toasts.Clear(); toastUntil = 0f; }

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
