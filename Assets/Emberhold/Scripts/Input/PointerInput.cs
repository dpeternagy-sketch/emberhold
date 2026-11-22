using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Emberhold
{
    /// <summary>
    /// One place that reads touch / mouse, for both Unity input backends.
    /// Gives a single "primary pointer" plus pinch and scroll for zooming.
    /// </summary>
    public static class PointerInput
    {
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        static float lastPinchDistance = -1f;

        public static int TouchCount
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Touchscreen.current == null) return 0;
                int n = 0;
                foreach (var t in Touchscreen.current.touches) if (t.press.isPressed) n++;
                return n;
#else
                return Input.touchCount;
#endif
            }
        }

        public static bool Pressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
                return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
                if (Input.touchCount > 0) return true;
                return Input.GetMouseButton(0);
#endif
            }
        }

        public static Vector2 Position
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                    return Touchscreen.current.primaryTouch.position.ReadValue();
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
                if (Input.touchCount > 0) return Input.GetTouch(0).position;
                return Input.mousePosition;
#endif
            }
        }

        /// <summary>Positive = zoom in. Combines mouse wheel and two-finger pinch.</summary>
        public static float ZoomDelta
        {
            get
            {
                float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) scroll = Mouse.current.scroll.ReadValue().y / 120f;
#else
                scroll = Input.mouseScrollDelta.y;
#endif
                Vector2 a, b;
                if (TwoTouches(out a, out b))
                {
                    float d = Vector2.Distance(a, b);
                    float delta = lastPinchDistance > 0f ? (d - lastPinchDistance) / 200f : 0f;
                    lastPinchDistance = d;
                    return scroll + delta;
                }
                lastPinchDistance = -1f;
                return scroll;
            }
        }

        static bool TwoTouches(out Vector2 a, out Vector2 b)
        {
            a = b = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current == null) return false;
            int n = 0;
            foreach (var t in Touchscreen.current.touches)
            {
                if (!t.press.isPressed) continue;
                if (n == 0) a = t.position.ReadValue(); else b = t.position.ReadValue();
                if (++n == 2) return true;
            }
            return false;
#else
            if (Input.touchCount < 2) return false;
            a = Input.GetTouch(0).position; b = Input.GetTouch(1).position;
            return true;
#endif
        }

        public static bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }
    }
}
