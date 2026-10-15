using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    public enum SfxId { Click, Place, Remove, Coin, Bell, Sparkle, Error, DayEnd, Bubble, Fire }

    /// <summary>
    /// 합성(신시사이즈) 플레이스홀더 효과음. 오디오 에셋 없이 동전·종·보글 소리를 만든다.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        static Sfx instance;
        static readonly Dictionary<SfxId, AudioClip> clips = new();
        static readonly Dictionary<SfxId, float> lastPlayed = new();
        AudioSource[] sources;
        int next;
        public static float Volume = 0.6f;

        void Awake()
        {
            instance = this;
            sources = new AudioSource[8];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
        }

        public static void Play(SfxId id, float volume = 1f)
        {
            if (!instance) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out var t) && now - t < 0.05f) return;
            lastPlayed[id] = now;
            if (!clips.TryGetValue(id, out var clip)) clips[id] = clip = Make(id);
            var src = instance.sources[instance.next];
            instance.next = (instance.next + 1) % instance.sources.Length;
            src.pitch = 1f + Random.Range(-0.04f, 0.04f);
            src.PlayOneShot(clip, volume * Volume);
        }

        static AudioClip Make(SfxId id)
        {
            float len = id switch
            {
                SfxId.Bell => 0.9f, SfxId.DayEnd => 1.2f, SfxId.Sparkle => 0.5f, SfxId.Coin => 0.35f,
                SfxId.Bubble => 0.25f, SfxId.Fire => 0.7f, _ => 0.15f,
            };
            int n = (int)(Rate * len);
            var d = new float[n];
            var rng = new System.Random((int)id * 31 + 7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 8f / len);
                float v = 0f;
                switch (id)
                {
                    case SfxId.Click:
                        v = Mathf.Sin(2 * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 60f) * 0.5f;
                        break;
                    case SfxId.Place:
                        v = (Mathf.Sin(2 * Mathf.PI * 180f * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-t * 35f);
                        break;
                    case SfxId.Remove:
                        v = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(500f, 150f, t / len) * t) * env * 0.5f;
                        break;
                    case SfxId.Coin:
                    {
                        float f = t < 0.07f ? 988f : 1318f;
                        v = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2f * t)) * Mathf.Exp(-t * 9f) * 0.4f;
                        break;
                    }
                    case SfxId.Bell:
                        v = (Mathf.Sin(2 * Mathf.PI * 880f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 1320f * t) +
                             0.25f * Mathf.Sin(2 * Mathf.PI * 2640f * t)) * Mathf.Exp(-t * 4f) * 0.3f;
                        break;
                    case SfxId.Sparkle:
                    {
                        float f = 1200f + 800f * Mathf.Floor(t * 16f) % 4f * 0.5f + 400f * Mathf.Floor(t * 16f);
                        v = Mathf.Sin(2 * Mathf.PI * f * t) * env * 0.25f;
                        break;
                    }
                    case SfxId.Error:
                        v = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 140f * t)) * env * 0.25f;
                        break;
                    case SfxId.DayEnd:
                    {
                        float[] notes = { 523f, 659f, 784f, 1046f };
                        int k = Mathf.Min(3, (int)(t / 0.18f));
                        v = Mathf.Sin(2 * Mathf.PI * notes[k] * t) * Mathf.Exp(-(t - k * 0.18f) * 5f) * 0.3f;
                        break;
                    }
                    case SfxId.Fire:
                        v = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 4f) * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 9f * t)) * 0.45f +
                            Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(120f, 50f, t / len) * t) * Mathf.Exp(-t * 5f) * 0.4f;
                        break;
                    case SfxId.Bubble:
                        v = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(300f, 900f, t / len) * t) * Mathf.Sin(Mathf.PI * t / len) * 0.35f;
                        break;
                }
                d[i] = v;
            }
            var clip = AudioClip.Create(id.ToString(), n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
