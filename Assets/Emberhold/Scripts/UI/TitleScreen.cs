using UnityEngine;
using UnityEngine.UI;

namespace Emberhold
{
    /// <summary>
    /// The title screen over the city at launch: Peter's valley picture, the Mega Fort logo and "Tap to play".
    /// One tap anywhere fades it away. (Resources/UI/Backgrounds/title.png, Resources/UI/logo_megafort.png)
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public static TitleScreen Instance { get; private set; }
        CanvasGroup group;
        Text tap;
        float fadeFrom = -1f;

        public static void Create(Transform screenRoot)
        {
            var bgTex = Resources.Load<Texture2D>("UI/Backgrounds/title");
            if (bgTex == null) return;
            var rt = UIKit.Rect("TitleScreen", screenRoot);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            var t = rt.gameObject.AddComponent<TitleScreen>();
            t.group = rt.gameObject.AddComponent<CanvasGroup>();
            var black = rt.gameObject.AddComponent<Image>();
            black.color = new Color(0.05f, 0.08f, 0.16f, 1f);   // behind the picture on very tall screens
            // the valley picture fills the screen (cropped, never stretched)
            var bg = UIKit.Rect("Picture", rt);
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
            var raw = bg.gameObject.AddComponent<RawImage>();
            raw.texture = bgTex; raw.raycastTarget = false;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = bgTex.width / (float)bgTex.height;
            var logoTex = Resources.Load<Texture2D>("UI/logo_megafort");
            if (logoTex != null)
            {
                var lg = UIKit.Rect("Logo", rt);
                lg.anchorMin = lg.anchorMax = new Vector2(0.5f, 1f); lg.pivot = new Vector2(0.5f, 1f);
                lg.anchoredPosition = new Vector2(0f, -150f);
                lg.sizeDelta = new Vector2(900f, 900f * logoTex.height / logoTex.width);
                var li = lg.gameObject.AddComponent<RawImage>();
                li.texture = logoTex; li.raycastTarget = false;
            }
            t.tap = UIKit.OutlinedLabel(rt, "Tap to play", 52, TextAnchor.MiddleCenter);
            UIKit.Anchor(t.tap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(800f, 90f));
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = black;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(t.Dismiss);
            Instance = t;
        }

        public void Dismiss()
        {
            if (fadeFrom < 0f) { fadeFrom = Time.unscaledTime; AudioManager.Play("ui", 0.6f); }
        }

        /// <summary>Gone at once (dev preview).</summary>
        public static void HideNow()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        void Update()
        {
            if (tap != null) tap.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)));
            if (fadeFrom < 0f) return;
            float k = (Time.unscaledTime - fadeFrom) / 0.35f;
            group.alpha = 1f - k;
            if (k >= 1f) { Instance = null; Destroy(gameObject); }
        }
    }
}
