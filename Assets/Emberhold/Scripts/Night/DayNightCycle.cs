using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberhold
{
    public enum Phase { Day, Dusk, Night, Dawn }

    /// <summary>
    /// Day: build and gather. Night: moonlit blue light, enemies come from unknown directions.
    /// Dawn: rewards and new settlers.
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
        Color nightAmbient = new Color(0.2f, 0.23f, 0.36f);
        Color dayFog = new Color(0.7f, 0.78f, 0.85f);
        Color nightFog = new Color(0.08f, 0.1f, 0.18f);
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
            RenderSettings.fogMode = FogMode.Linear;   // start/end follow the camera zoom (CameraRig)
            Apply(0f);
        }

        public void StartNightNow()
        {
            if (Phase != Phase.Day) return;
            Phase = Phase.Dusk;
            transition = 0f;
            AudioManager.Play("horn");
            AudioManager.SetAmbience("night_amb");
        }

        public void EndNight()
        {
            if (Phase != Phase.Night) return;
            Phase = Phase.Dawn;
            transition = 0f;
            AudioManager.Play("rooster");
            AudioManager.SetAmbience("day_amb");
            nextCall = Time.time + 3f;   // birds wake up right after the rooster
        }

        float nextCall;

        /// <summary>Owls hoot through the night; birds sing now and then by day.</summary>
        void AnimalCalls()
        {
            if (Time.time < nextCall) return;
            if (Phase == Phase.Night) { AudioManager.Play("owl", 0.55f); nextCall = Time.time + UnityEngine.Random.Range(14f, 28f); }
            else if (Phase == Phase.Day) { AudioManager.Play("birds", 0.45f); nextCall = Time.time + UnityEngine.Random.Range(25f, 50f); }
            else nextCall = Time.time + 2f;
        }

        void Update()
        {
            AnimalCalls();
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
                        nextCall = Time.time + 4f;   // first owl soon after dark
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
                sun.intensity = Mathf.Lerp(1.25f, 0.45f, dark); // moonlight: darker and blue, but everything stays visible
                sun.color = Color.Lerp(new Color(1f, 0.95f, 0.86f), new Color(0.45f, 0.55f, 0.9f), dark);
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(50f, 25f, dark), -35f, 0f);
            }
            RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, dark);
            RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, dark);
            RenderSettings.fogDensity = Mathf.Lerp(0.006f, 0.009f, dark);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
            }
        }
    }
}
