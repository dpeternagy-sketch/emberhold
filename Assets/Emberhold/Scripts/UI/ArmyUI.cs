using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhold
{
    /// <summary>
    /// Army screens: the Train popup (Barracks / Robot Factory), the Commander pill (level + XP, tap to
    /// find him) and the Orbital Strike button that shows up while the city is under attack.
    /// </summary>
    public class ArmyUI : MonoBehaviour
    {
        GameManager gm; InteractionController input; CameraRig rig;
        RectTransform trainPopup, rows, heroPill, xpFill;
        Text trainTitle, trainStatus, heroText;
        Button strikeBtn; Text strikeCaption;
        Button troopsBtn, homeBtn, doneBtn, repairAllBtn;
        float repairAllArmedUntil;
        Building trainFor;
        readonly List<KeyValuePair<Button, UnitDef>> unitButtons = new List<KeyValuePair<Button, UnitDef>>();
        readonly List<Text> unitNotes = new List<Text>();

        public void Init(GameManager game, InteractionController interaction, CameraRig cameraRig, Transform root)
        {
            gm = game; input = interaction; rig = cameraRig;

            // ---- Train popup ----
            trainPopup = UIKit.PanelBox("TrainPopup", root).rectTransform;
            UIKit.Anchor(trainPopup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(860, 820));
            var header = UIKit.Skin("Header", trainPopup, "blue", "button_rectangle_depth_flat").rectTransform;
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
            header.anchoredPosition = new Vector2(0, -10); header.sizeDelta = new Vector2(-20, 96);
            trainTitle = UIKit.OutlinedLabel(trainPopup, "", 40, TextAnchor.UpperCenter);
            trainTitle.rectTransform.offsetMax = new Vector2(-90, -32);
            var x = UIKit.IconButton(trainPopup, "icon_close", null, 84, CloseTrain, "red");
            UIKit.Anchor(x.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-18, -18), new Vector2(84, 84));
            rows = UIKit.Rect("Rows", trainPopup);
            UIKit.Anchor(rows, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(800, 460));
            trainStatus = UIKit.Label(trainPopup, "", 32, TextAnchor.MiddleCenter);
            trainStatus.supportRichText = true;
            UIKit.Anchor(trainStatus.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(800, 100));
            var cancel = UIKit.SkinButton(trainPopup, "Cancel last", 30, () => { if (trainFor != null) gm.Troops.CancelLast(trainFor); }, "red");
            UIKit.Anchor(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 90));
            trainPopup.gameObject.SetActive(false);

            // ---- Commander pill (top left, under the resources) ----
            heroPill = UIKit.Rect("CommanderPill", root);
            UIKit.Anchor(heroPill, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -290), new Vector2(360, 84));
            var bg = UIKit.Pill("Bg", heroPill);
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            var hit = heroPill.gameObject.AddComponent<Button>();
            hit.targetGraphic = bg;
            hit.onClick.AddListener(() => { var h = gm.Troops.Hero; if (h != null) rig.FocusOn(h.transform.position); });
            var star = UIKit.Box("Star", heroPill, new Color(1f, 0.78f, 0.2f));
            UIKit.Anchor(star.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(46, 46));
            star.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            heroText = UIKit.OutlinedLabel(heroPill, "", 28, TextAnchor.UpperLeft);
            heroText.rectTransform.offsetMin = new Vector2(80, 0); heroText.rectTransform.offsetMax = new Vector2(-12, -8);
            var xpBack = UIKit.Box("XpBack", heroPill, new Color(0, 0, 0, 0.45f));
            UIKit.Anchor(xpBack.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 14), new Vector2(260, 14));
            xpFill = UIKit.Box("XpFill", xpBack.transform, new Color(1f, 0.78f, 0.2f)).rectTransform;
            xpFill.anchorMin = Vector2.zero; xpFill.anchorMax = new Vector2(0, 1); xpFill.pivot = new Vector2(0, 0.5f); xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;

            // ---- Orbital Strike (bottom left, only during an attack) ----
            strikeBtn = UIKit.IconButton(root, "icon_attack", "Strike", 150, () => { if (!gm.Troops.Strike()) gm.Toast("Strike not ready"); }, "red");
            UIKit.Anchor(strikeBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 360), new Vector2(150, 190));
            strikeCaption = strikeBtn.GetComponentInChildren<Text>();

            // ---- Troop orders (bottom left): select all, call home, done. Tap units / ground for the rest ----
            troopsBtn = UIKit.IconButton(root, "icon_select_all", "Troops", 120, () => input.SelectAllUnits(), "blue");
            UIKit.Anchor(troopsBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 210), new Vector2(120, 140));
            homeBtn = UIKit.IconButton(root, "icon_recall", "Home", 120, () => input.RecallUnits(), "green");
            UIKit.Anchor(homeBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(164, 210), new Vector2(120, 140));
            doneBtn = UIKit.IconButton(root, "icon_done", "Done", 120, () => input.ClearUnits(), "red");

            // ---- Repair all (same corner as Strike, which only shows during an attack) ----
            repairAllBtn = UIKit.IconButton(root, "icon_repair_all", "Repair all", 150, () =>
            {
                if (Time.unscaledTime < repairAllArmedUntil) { repairAllArmedUntil = 0f; gm.RepairAll(); return; }
                repairAllArmedUntil = Time.unscaledTime + 3f;
                gm.Toast("Repair everything for " + gm.RepairAllCost() + ". Tap again");
            }, "yellow");
            UIKit.Anchor(repairAllBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 360), new Vector2(150, 190));
            UIKit.Anchor(doneBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(294, 210), new Vector2(120, 140));
        }

        public void ShowTrain(Building b)
        {
            if (b == null || b.Def.trains == null) return;
            trainFor = b;
            foreach (Transform c in rows) Destroy(c.gameObject);
            unitButtons.Clear(); unitNotes.Clear();
            int i = 0;
            foreach (var id in b.Def.trains)
            {
                var d = UnitDef.Get(id);
                if (d == null) continue;
                var row = UIKit.Rect("Row_" + id, rows);
                UIKit.Anchor(row, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -i * 230), new Vector2(800, 215));
                var card = UIKit.Skin("Card", row, "grey", "button_square_depth_flat").rectTransform;
                card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one; card.offsetMin = card.offsetMax = Vector2.zero;
                var name = UIKit.OutlinedLabel(row, d.displayName, 36, TextAnchor.UpperLeft);
                name.rectTransform.offsetMin = new Vector2(26, 0); name.rectTransform.offsetMax = new Vector2(-300, -16);
                var info = UIKit.Label(row, "HP " + d.hp + "   Damage " + Mathf.RoundToInt(d.damage / d.interval) + "/s   Space " + d.space + "\n"
                                         + (d.flies ? "Flies over walls.  " : "") + d.counterHint + "   " + Mathf.RoundToInt(d.trainSeconds) + "s",
                                         26, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.22f));
                info.rectTransform.offsetMin = new Vector2(26, 0); info.rectTransform.offsetMax = new Vector2(-300, -66);
                var note = UIKit.Label(row, "", 26, TextAnchor.LowerLeft, new Color(0.85f, 0.2f, 0.15f));
                note.rectTransform.offsetMin = new Vector2(26, 14); note.rectTransform.offsetMax = new Vector2(-300, -150);
                unitNotes.Add(note);
                var dd = d;
                var btn = UIKit.SkinButton(row, "", 30, () => gm.Troops.Train(trainFor, dd), "green");
                UIKit.Anchor(btn.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(260, 150));
                CostChips(btn.transform, d.cost);
                unitButtons.Add(new KeyValuePair<Button, UnitDef>(btn, d));
                i++;
            }
            trainPopup.gameObject.SetActive(true);
            Refresh();
        }

        static void CostChips(Transform parent, Cost c)
        {
            var line = new List<string>();
            if (c.food > 0) line.Add(c.food + " food");
            if (c.steel > 0) line.Add(c.steel + " steel");
            if (c.energy > 0) line.Add(c.energy + " energy");
            if (c.coins > 0) line.Add(c.coins + " coins");
            var t = parent.GetComponentInChildren<Text>();
            t.text = "Train\n<size=22>" + string.Join("  ", line.ToArray()) + "</size>";
            t.supportRichText = true;
        }

        public void CloseTrain() { if (trainPopup != null) trainPopup.gameObject.SetActive(false); trainFor = null; }

        void Update()
        {
            if (gm == null || gm.Troops == null) return;
            if (trainPopup.gameObject.activeSelf)
            {
                if (trainFor == null || trainFor.Ruined || input.Selected != trainFor) CloseTrain();
                else Refresh();
            }

            bool hasHero = gm.Troops.HasCommandCenter;
            heroPill.gameObject.SetActive(hasHero);
            if (hasHero)
            {
                var s = gm.State;
                heroText.text = "Commander  Lv " + s.heroLevel + (s.heroLevel >= Army.MaxHeroLevel ? "  MAX" : "");
                xpFill.anchorMax = new Vector2(gm.Troops.XpProgress, 1);
            }

            // only while it can be used: Commander up, attackers on the map, cooldown over
            strikeBtn.gameObject.SetActive(hasHero && gm.Troops.StrikeReady);
            input.SelectedUnits.RemoveAll(u => u == null);
            bool anyUnits = Unit.All.Count > 0, picked = input.SelectedUnits.Count > 0;
            troopsBtn.gameObject.SetActive(anyUnits && input.Ghost == null);
            homeBtn.gameObject.SetActive(picked);
            repairAllBtn.gameObject.SetActive(Enemy.All.Count == 0 && Time.frameCount % 15 == 0 ? gm.BrokenBuildings().Count > 0 : repairAllBtn.gameObject.activeSelf && Enemy.All.Count == 0);
            doneBtn.gameObject.SetActive(picked);
            strikeCaption.text = "Strike";
        }

        void Refresh()
        {
            var b = trainFor;
            trainTitle.text = b.Def.displayName + "  Lv " + b.Data.level;
            for (int i = 0; i < unitButtons.Count; i++)
            {
                var d = unitButtons[i].Value;
                string why = gm.Troops.CantTrain(b, d);
                unitButtons[i].Key.interactable = why == null;
                unitNotes[i].text = why ?? "";
                int queued = 0; foreach (var id in Army.Queue(b)) if (id == d.id) queued++;
                if (why == null) unitNotes[i].text = "<color=#2E7D32>Have " + gm.Troops.Count(d.id) + (queued > 0 ? "   In queue " + queued : "") + "</color>";
                unitNotes[i].supportRichText = true;
            }
            var q = Army.Queue(b);
            string training = q.Count == 0 ? "Nothing in training" : "Training " + UnitDef.Get(q[0]).displayName + "  " + Mathf.CeilToInt(gm.Troops.SecondsLeft(b)) + "s   (" + q.Count + " in queue)";
            trainStatus.text = training + "\nArmy space  " + (gm.Troops.Used + gm.Troops.Queued) + " / " + gm.Troops.Capacity;
        }
    }
}
