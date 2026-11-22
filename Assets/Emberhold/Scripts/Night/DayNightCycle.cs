using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberhold
{
    public enum Phase { Day, Dusk, Night, Dawn }

    /// <summary>
    /// Day: build and gather. Night: fog falls, enemies come from unknown directions,
    /// only light reveals them. Dawn: rewards and new settlers.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        public const float DayLength = 150f;
        const float TransitionSeconds = 4f;

        public Phase Phase { get; private set; }
        public bool IsNight { get { return Phase == Phase.Night || Phase == Phase.Dusk; } }
        public float Darkness { get; private set; }   // 0 = full day, 1 = deep night
        public float DayTimeLeft { get; set; }

        public event Action OnNightStarted;
        public event Action OnDawn;

        Light sun;
        float transition;
        Color dayAmbient = new Color(0.55f, 0.58f, 0.62f);
        Color nightAmbient = new Color(0.025f, 0.03f, 0.06f);
        Color dayFog = new Color(0.7f, 0.78f, 0.85f);
        Color nightFog = new Color(0.01f, 0.012f, 0.025f);
        Camera cam;

        void Awake() { Instance = this; }

        public void Init(Light sunLight, Camera camera, float dayTimeLeft)
        {
            sun = sunLight;
            cam = camera;
            DayTimeLeft = dayTimeLeft;
            Phase = Phase.Day;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            Apply(0f);
        }

        public void StartNightNow()
        {
            if (Phase != Phase.Day) return;
            Phase = Phase.Dusk;
            transition = 0f;
            AudioManager.Play("horn");
            AudioManager.SetAmbience("night_ambience");
        }

        public void EndNight()
        {
            if (Phase != Phase.Night) return;
            Phase = Phase.Dawn;
            transition = 0f;
            AudioManager.Play("rooster");
            AudioManager.SetAmbience("day_ambience");
        }

        void Update()
        {
            switch (Phase)
            {
                case Phase.Day:
                    DayTimeLeft -= Time.deltaTime;
                    if (DayTimeLeft <= 0f) StartNightNow();
                    Apply(0f);
                    break;
                case Phase.Dusk:
                    transition += Time.deltaTime / TransitionSeconds;
                    Apply(Mathf.SmoothStep(0f, 1f, transition));
                    if (transition >= 1f)
                    {
                        Phase = Phase.Night;
                        if (OnNightStarted != null) OnNightStarted();
                    }
                    break;
                case Phase.Night:
                    Apply(1f);
                    break;
                case Phase.Dawn:
                    transition += Time.deltaTime / TransitionSeconds;
                    Apply(1f - Mathf.SmoothStep(0f, 1f, transition));
                    if (transition >= 1f)
                    {
                        Phase = Phase.Day;
                        DayTimeLeft = DayLength;
                        if (OnDawn != null) OnDawn();
                    }
                    break;
            }
        }

        void Apply(float dark)
        {
            Darkness = dark;
            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(1.25f, 0.04f, dark);
                sun.color = Color.Lerp(new Color(1f, 0.95f, 0.86f), new Color(0.45f, 0.55f, 0.9f), dark);
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(50f, 25f, dark), -35f, 0f);
            }
            RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, dark);
            RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, dark);
            RenderSettings.fogDensity = Mathf.Lerp(0.006f, 0.022f, dark);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
            }
        }
    }
}
