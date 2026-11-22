using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Plays clips from Resources/Audio/{name}. Missing clips are ignored, so new sounds
    /// (owl, rooster, horn, birds...) can be dropped in later without code changes.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        AudioSource sfx;
        AudioSource ambience;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        void Awake()
        {
            Instance = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.volume = 0.12f;
            ambience.playOnAwake = false;
        }

        AudioClip Clip(string name)
        {
            AudioClip c;
            if (!cache.TryGetValue(name, out c))
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                cache[name] = c;
            }
            return c;
        }

        // Mixing: everything is quieter than full scale, the same sound can't stack up,
        // and only a few effects play at once, so battles never turn into noise.
        const float SfxMaster = 0.45f;
        const int MaxVoices = 5;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly Queue<float> voices = new Queue<float>();

        public static float SoundLevel
        {
            get { return PlayerPrefs.GetFloat("eh.sound", 0.7f); }
            set { PlayerPrefs.SetFloat("eh.sound", value); if (Instance != null) Instance.ApplyLevels(); }
        }
        public static bool AmbienceOn
        {
            get { return PlayerPrefs.GetInt("eh.amb", 1) == 1; }
            set { PlayerPrefs.SetInt("eh.amb", value ? 1 : 0); if (Instance != null) Instance.ApplyLevels(); }
        }

        /// <summary>Volume of birds, owls and other nature sounds (0..1).</summary>
        public static float AmbienceLevel
        {
            get { return PlayerPrefs.GetFloat("eh.amblevel", 0.7f); }
            set { PlayerPrefs.SetFloat("eh.amblevel", value); if (Instance != null) Instance.ApplyLevels(); }
        }

        void ApplyLevels() { ambience.volume = AmbienceOn ? 0.17f * AmbienceLevel : 0f; }
        void Start() { ApplyLevels(); }

        public static void Play(string name, float volume = 1f)
        {
            if (Instance == null || SoundLevel <= 0f) return;
            var c = Instance.Clip(name);
            if (c == null) return;
            float now = Time.unscaledTime;
            float last;
            if (Instance.lastPlayed.TryGetValue(name, out last) && now - last < 0.12f) return;   // same sound too soon
            while (Instance.voices.Count > 0 && Instance.voices.Peek() < now) Instance.voices.Dequeue();
            if (Instance.voices.Count >= MaxVoices) return;
            Instance.lastPlayed[name] = now;
            Instance.voices.Enqueue(now + Mathf.Min(c.length, 1.5f));
            Instance.sfx.PlayOneShot(c, volume * SfxMaster * SoundLevel);
        }

        /// <summary>Plays a sound in the world: quieter when far from what the camera looks at.</summary>
        public static void PlayAt(string name, Vector3 position, float volume = 1f)
        {
            if (Instance == null) return;
            var cam = Camera.main;
            float k = 1f;
            if (cam != null)
            {
                // distance from the point the camera is looking at, on the ground
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                float tHit = ray.direction.y < -0.01f ? -ray.origin.y / ray.direction.y : 30f;
                var focus = ray.GetPoint(tHit);
                float d = Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(focus.x, 0, focus.z));
                k = Mathf.Clamp01(1.15f - d / 40f);
            }
            if (k > 0.05f) Play(name, volume * k);
        }

        /// <summary>Plays one random clip of a few variants: name, name2, name3...</summary>
        public static void PlayVariant(string name, int variants, float volume = 1f)
        {
            int i = Random.Range(1, variants + 1);
            Play(i == 1 ? name : name + i, volume);
        }

        public static void SetAmbience(string name)
        {
            if (Instance == null) return;
            var c = Instance.Clip(name);
            if (Instance.ambience.clip == c) return;
            Instance.ambience.Stop();
            Instance.ambience.clip = c;
            Instance.ApplyLevels();
            if (c != null) Instance.ambience.Play();
        }
    }
}
