using UnityEngine;
using UnityEngine.UI;

namespace Emberhold
{
    /// <summary>
    /// The Walls panel: the city's wall loop in 4 sides. Each row shows a side's level and one button:
    /// Build (whole side, with its gate and two guns, in one price), Upgrade (a builder works on it), or the reason
    /// it can't go up yet. "Upgrade all" starts every side that can go up.
    /// Opened from the Walls card in the Defenses menu, or by tapping any wall piece, gate or wall gun.
    /// </summary>
    public class WallPanel : MonoBehaviour
    {
        GameManager gm;
        RectTransform panel;
        Text title;
        readonly Text[] names = new Text[4], levels = new Text[4], notes = new Text[4];
        readonly Button[] buttons = new Button[4];
        readonly Image[] rowBacks = new Image[4];
        Button allBtn; Text allNote;
        int highlight = -1;
        float refreshIn;

        public bool Open { get { return panel != null && panel.gameObject.activeSelf; } }

        public void Init(GameManager game, Transform root)
        {
            gm = game;
            panel = UIKit.PanelBox("WallPanel", root).rectTransform;
            UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(900, 1040));
            var header = UIKit.Skin("Header", panel, "blue", "button_rectangle_depth_flat").rectTransform;
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
            header.anchoredPosition = new Vector2(0, -10); header.sizeDelta = new Vector2(-20, 96);
            title = UIKit.OutlinedLabel(panel, "Walls", 42, TextAnchor.UpperCenter);
            title.rectTransform.offsetMax = new Vector2(-90, -30);
            var x = UIKit.IconButton(panel, "icon_close", null, 72, Close, "red");
            UIKit.Anchor(x.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-58, -58), new Vector2(84, 84));

            for (int i = 0; i < 4; i++)
            {
                int side = i;
                var row = UIKit.Pill("Row" + i, panel);
                rowBacks[i] = row;
                UIKit.Anchor(row.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -128 - i * 166), new Vector2(840, 150));
                names[i] = UIKit.OutlinedLabel(row.transform, GameManager.WallSideNames[i], 36, TextAnchor.UpperLeft);
                names[i].rectTransform.offsetMin = new Vector2(26, 0); names[i].rectTransform.offsetMax = new Vector2(-330, -16);
                levels[i] = UIKit.Label(row.transform, "", 30, TextAnchor.MiddleLeft);
                levels[i].rectTransform.offsetMin = new Vector2(26, 0); levels[i].rectTransform.offsetMax = new Vector2(-330, -40);
                notes[i] = UIKit.Label(row.transform, "", 26, TextAnchor.LowerLeft, new Color(0.85f, 0.88f, 0.95f));
                notes[i].rectTransform.offsetMin = new Vector2(26, 14); notes[i].rectTransform.offsetMax = new Vector2(-330, 0);
                buttons[i] = UIKit.SkinButton(row.transform, "Build", 30, () => Press(side), "green");
                UIKit.Anchor(buttons[i].GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(300, 110));
            }
            allBtn = UIKit.SkinButton(panel, "Upgrade all", 36, () => { gm.UpgradeAllWalls(); Refresh(); }, "yellow");
            UIKit.Anchor(allBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(460, 104));
            allNote = UIKit.Label(panel, "", 28, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.95f));
            UIKit.Anchor(allNote.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 162), new Vector2(840, 46));
            allNote.horizontalOverflow = HorizontalWrapMode.Wrap; allNote.resizeTextForBestFit = true; allNote.resizeTextMinSize = 18; allNote.resizeTextMaxSize = 28;
            panel.gameObject.SetActive(false);
        }

        public void Show(int side = -1)
        {
            highlight = side;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            Refresh();
        }

        public void Close() { panel.gameObject.SetActive(false); }

        void Press(int i)
        {
            var s = (GameManager.WallSide)i;
            if (gm.WallLevel(s) <= 0) gm.BuyWallSide(s);
            else gm.UpgradeWallSide(s);
            Refresh();
        }

        void Update()
        {
            if (!Open) return;
            if ((refreshIn -= Time.unscaledDeltaTime) > 0f) return;
            refreshIn = 0.5f;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < 4; i++)
            {
                var s = (GameManager.WallSide)i;
                int lv = gm.WallLevel(s);
                var gate = gm.GateOf(s);
                rowBacks[i].color = i == highlight ? new Color(0.25f, 0.45f, 0.85f, 0.85f) : UIKit.PillTint;
                string text, note;
                bool can;
                if (lv <= 0)
                {
                    var c = gm.WallBuyCost(s);
                    levels[i].text = "Not built  (gate + 2 guns included)";
                    text = "Build";
                    can = gm.State.CanAfford(c);
                    note = can ? Short(c) : "Need more " + gm.State.Missing(c);
                }
                else if (gate != null && gate.Data.IsUpgrading)
                {
                    levels[i].text = "Level " + lv + "  >  " + (lv + 1);
                    note = "Upgrading  " + Util.FormatTime(gate.UpgradeSecondsLeft);
                    text = "Working";
                    can = false;
                }
                else
                {
                    var block = gm.WallUpgradeBlock(s);
                    can = block == null;
                    if (lv >= GameManager.WallMaxLevel) { levels[i].text = "Level " + lv + " (max)"; text = "Max"; note = "Fully upgraded"; }
                    else
                    {
                        levels[i].text = "Level " + lv + " / " + GameManager.WallMaxLevel + "    Time " + Util.FormatTime(GameManager.WallUpgradeSeconds(lv));
                        text = "Upgrade";
                        note = block ?? Short(gm.WallUpgradeCost(s));
                    }
                }
                notes[i].color = can || lv >= GameManager.WallMaxLevel || text == "Working" ? new Color(0.85f, 0.88f, 0.95f) : new Color(1f, 0.45f, 0.4f);
                notes[i].text = note;
                UIKit.SetText(buttons[i], text);
                buttons[i].interactable = can;
            }
            int sides;
            var all = gm.WallUpgradeAllCost(out sides);
            allBtn.gameObject.SetActive(sides > 1);
            allNote.text = sides > 1 ? "All " + sides + " sides:  " + Short(all) + (gm.State.CanAfford(all) ? "" : "   (need more)") : "";
            allBtn.interactable = sides > 1;
        }

        /// <summary>A compact price for a button: "720 St  320 En  80 Co".</summary>
        static string Short(Cost c)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (c.steel > 0) parts.Add(c.steel + " Steel");
            if (c.energy > 0) parts.Add(c.energy + " Energy");
            if (c.coins > 0) parts.Add(c.coins + " Coins");
            if (c.food > 0) parts.Add(c.food + " Food");
            return string.Join("  ", parts.ToArray());
        }
    }
}
