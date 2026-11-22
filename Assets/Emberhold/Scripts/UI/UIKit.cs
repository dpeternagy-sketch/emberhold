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
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static readonly Color Panel = new Color(0.08f, 0.07f, 0.06f, 0.82f);
        public static readonly Color ButtonColor = new Color(0.2f, 0.16f, 0.12f, 0.92f);
        public static readonly Color Accent = new Color(0.86f, 0.6f, 0.26f, 1f);
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
            scaler.matchWidthOrHeight = 0.5f;
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

        /// <summary>A round icon button with a small caption under it.</summary>
        public static UnityEngine.UI.Button IconButton(Transform parent, string icon, string caption, float size, Action onClick)
        {
            var r = Rect("IconButton_" + icon, parent);
            r.sizeDelta = new Vector2(size, size + (string.IsNullOrEmpty(caption) ? 0 : 40));
            var hit = r.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // invisible hit area
            var circle = Box("Circle", r, new Color(0.12f, 0.09f, 0.06f, 0.55f));
            circle.sprite = CircleSprite;
            circle.raycastTarget = false;
            Anchor(circle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(size, size));
            var ic = Icon(r, icon, size * 0.72f);
            Anchor(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.5f), new Vector2(size * 0.72f, size * 0.72f));
            if (!string.IsNullOrEmpty(caption))
            {
                var t = OutlinedLabel(r, caption, 28, TextAnchor.LowerCenter);
                t.rectTransform.offsetMin = new Vector2(-20, -6);
                t.rectTransform.offsetMax = new Vector2(20, 0);
            }
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = circle;
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
            var img = Box("Button", parent, color ?? ButtonColor);
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            b.colors = colors;
            var label = Label(img.transform, text, fontSize);
            label.name = "Text";
            b.onClick.AddListener(() => { AudioManager.Play("ui", 0.5f); if (onClick != null) onClick(); });
            return b;
        }

        public static void SetText(UnityEngine.UI.Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null && t.text != text) t.text = text;
        }
    }
}
