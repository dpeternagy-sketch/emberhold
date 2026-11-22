using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Emberhold
{
    /// <summary>Small helpers to build a clean uGUI interface from code.</summary>
    public static class UIKit
    {
        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("UI/Fonts/Main");          // Kenney font (CC0)
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static readonly Color Panel = new Color(0.08f, 0.07f, 0.06f, 0.82f);
        public static readonly Color ButtonColor = new Color(0.2f, 0.16f, 0.12f, 0.92f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.3f, 1f);
        /// <summary>Deep navy used for panels and pills (multiplied onto Kenney's light grey shapes).</summary>
        public static readonly Color PanelTint = new Color(0.2f, 0.25f, 0.38f, 0.96f);
        public static readonly Color PillTint = new Color(0.1f, 0.12f, 0.2f, 0.72f);

        // ---------- Kenney UI Pack (CC0) skin ----------
        // Shapes live in Resources/UI/{blue,green,red,yellow,grey}/ and are imported as 9-sliced sprites.

        public static Sprite Shape(string color, string shape)
        {
            return Resources.Load<Sprite>("UI/" + color + "/" + shape);
        }

        /// <summary>An image using a Kenney shape, stretched with 9-slicing so corners stay crisp.</summary>
        public static Image Skin(string name, Transform parent, string color, string shape, Color? tint = null)
        {
            var img = Box(name, parent, tint ?? Color.white);
            var sp = Shape(color, shape);
            if (sp != null) { img.sprite = sp; img.type = Image.Type.Sliced; }
            return img;
        }

        /// <summary>Navy rounded panel for popups and the build sheet.</summary>
        public static Image PanelBox(string name, Transform parent)
        {
            var img = Skin(name, parent, "grey", "button_square_flat", PanelTint);
            if (img.sprite == null) img.color = Panel;
            return img;
        }

        /// <summary>Dark see-through rounded pill (resource counters, status, toasts).</summary>
        public static Image Pill(string name, Transform parent)
        {
            var img = Skin(name, parent, "grey", "button_rectangle_flat", PillTint);
            if (img.sprite == null) img.color = new Color(0, 0, 0, 0.45f);
            return img;
        }

        /// <summary>Picks the Kenney colour closest to an old flat colour (green = go, red = danger, brown = gold).</summary>
        static string ColorName(Color c)
        {
            if (c.g > c.r * 1.3f && c.g > c.b) return "green";
            if (c.r > c.g * 2f) return "red";
            if (c.r > c.b * 2f && c.g > c.b * 1.5f) return "yellow";
            return "blue";
        }
        public static readonly Color TextColor = new Color(0.96f, 0.93f, 0.86f);

        public static Canvas CreateCanvas()
        {
            var go = new GameObject("HUD Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;   // portrait phones: keep widths fixed, extra height goes to the map
            go.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            return canvas;
        }

        /// <summary>A full-screen panel shrunk to the device's safe area (notch, home bar, rounded corners).</summary>
        public static Transform SafeArea(Transform canvas)
        {
            var r = Rect("SafeArea", canvas);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            r.gameObject.AddComponent<SafeAreaFitter>();
            return r;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static Image Box(string name, Transform parent, Color color)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            var r = Rect("Label", parent);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(8, 4); r.offsetMax = new Vector2(-8, -4);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Texture2D IconTexture(string name)
        {
            return Resources.Load<Texture2D>("Icons/" + name);
        }

        public static RawImage Icon(Transform parent, string name, float size)
        {
            var r = Rect("Icon_" + name, parent);
            r.sizeDelta = new Vector2(size, size);
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = IconTexture(name);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Bold text with a dark outline, readable on top of the map without a background strip.</summary>
        public static Text OutlinedLabel(Transform parent, string text, int size, TextAnchor align)
        {
            var t = Label(parent, text, size, align);
            t.fontStyle = FontStyle.Bold;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.1f, 0.06f, 0.03f, 0.95f);
            o.effectDistance = new Vector2(2.5f, -2.5f);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.5f);
            sh.effectDistance = new Vector2(3, -4);
            return t;
        }

        /// <summary>A round glossy icon button with a small caption under it.</summary>
        public static UnityEngine.UI.Button IconButton(Transform parent, string icon, string caption, float size, Action onClick, string color = "blue")
        {
            var r = Rect("IconButton_" + icon, parent);
            r.sizeDelta = new Vector2(size, size + (string.IsNullOrEmpty(caption) ? 0 : 40));
            var hit = r.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // invisible hit area
            var circle = Skin("Circle", r, color, "button_round_depth_gloss");
            if (circle.sprite == null) { circle.sprite = CircleSprite; circle.color = new Color(0.12f, 0.09f, 0.06f, 0.55f); }
            circle.raycastTarget = false;
            Anchor(circle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(size, size));
            var ic = Icon(r, icon, size * 0.62f);
            Anchor(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.47f), new Vector2(size * 0.62f, size * 0.62f));
            if (!string.IsNullOrEmpty(caption))
            {
                var t = OutlinedLabel(r, caption, 26, TextAnchor.LowerCenter);
                t.rectTransform.offsetMin = new Vector2(-30, -6);
                t.rectTransform.offsetMax = new Vector2(30, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = circle;
            var colors = b.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            b.colors = colors;
            b.onClick.AddListener(() => { AudioManager.Play("ui", 0.5f); if (onClick != null) onClick(); });
            return b;
        }

        static Sprite circleSprite;
        static Sprite CircleSprite
        {
            get
            {
                if (circleSprite != null) return circleSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(n / 2f - 1f - d);
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(d - (n / 2f - 4f)) / 2.5f);
                        byte v = (byte)(255 - ring * 60);
                        px[y * n + x] = new Color32(v, v, v, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return circleSprite;
            }
        }

        public static UnityEngine.UI.Button Button(Transform parent, string text, int fontSize, Action onClick, Color? color = null)
        {
            return SkinButton(parent, text, fontSize, onClick, color.HasValue ? ColorName(color.Value) : "blue");
        }

        /// <summary>Glossy Kenney button with a depth edge; colour is blue, green, red, yellow or grey.</summary>
        public static UnityEngine.UI.Button SkinButton(Transform parent, string text, int fontSize, Action onClick, string color)
        {
            var img = Skin("Button", parent, color, "button_rectangle_depth_gloss");
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            b.colors = colors;
            var label = OutlinedLabel(img.transform, text, fontSize, TextAnchor.MiddleCenter);
            label.name = "Text";
            label.rectTransform.offsetMin = new Vector2(10, 12);   // the depth edge sits at the bottom
            label.rectTransform.offsetMax = new Vector2(-10, -2);
            b.onClick.AddListener(() => { AudioManager.Play("ui", 0.5f); if (onClick != null) onClick(); });
            return b;
        }

        public static void SetText(UnityEngine.UI.Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null && t.text != text) t.text = text;
        }
    }

    /// <summary>Keeps its RectTransform inside Screen.safeArea, updating when the screen changes.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        Vector2Int lastSize;
        void Update()
        {
            var sa = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (sa == last && size == lastSize) return;
            last = sa; lastSize = size;
            var r = (RectTransform)transform;
            r.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, size.x), sa.yMin / Mathf.Max(1, size.y));
            r.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, size.x), sa.yMax / Mathf.Max(1, size.y));
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
