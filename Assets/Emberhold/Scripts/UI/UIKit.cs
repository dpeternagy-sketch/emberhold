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
