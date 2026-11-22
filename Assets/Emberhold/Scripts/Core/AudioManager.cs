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
            ambience.volume = 0.35f;
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

        public static void Play(string name, float volume = 1f)
        {
            if (Instance == null) return;
            var c = Instance.Clip(name);
            if (c != null) Instance.sfx.PlayOneShot(c, volume);
        }

        public static void SetAmbience(string name)
        {
            if (Instance == null) return;
            var c = Instance.Clip(name);
            if (Instance.ambience.clip == c) return;
            Instance.ambience.Stop();
            Instance.ambience.clip = c;
            if (c != null) Instance.ambience.Play();
        }
    }
}
