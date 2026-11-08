using System;
using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>Simple generated sounds, so the prototype needs no audio files.</summary>
    public class ProtoAudio : MonoBehaviour
    {
        public static ProtoAudio I { get; private set; }

        [Range(0, 1)] public float volume = 0.5f;

        const int Rate = 44100;
        AudioSource[] _sources;
        int _next;
        AudioClip _hit, _crack, _pickup, _rare, _sell, _deny;
        float _lastPickup;

        void Awake()
        {
            I = this;
            _sources = new AudioSource[10];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }

            var rng = new System.Random(1);
            float Noise() => (float)(rng.NextDouble() * 2 - 1);

            _hit = Make("hit", 0.06f, t => (Mathf.Sin(2 * Mathf.PI * 240 * t) * 0.6f + Noise() * 0.4f) * Mathf.Exp(-t * 70));
            _crack = Make("crack", 0.2f, t => (Noise() * 0.7f + Mathf.Sin(2 * Mathf.PI * 85 * t) * 0.5f) * Mathf.Exp(-t * 18));
            _pickup = Make("pickup", 0.07f, t => Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Exp(-t * 45));
            _sell = Make("sell", 0.18f, t =>
            {
                float f = t < 0.07f ? 1250 : 1680;
                float local = t < 0.07f ? t : t - 0.07f;
                return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-local * 30);
            });
            _deny = Make("deny", 0.12f, t => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 140 * t)) * 0.4f * Mathf.Exp(-t * 20));
            _rare = Make("rare", 0.5f, t =>
            {
                float[] notes = { 660, 880, 1320 };
                int n = Mathf.Min((int)(t / 0.12f), notes.Length - 1);
                float local = t - n * 0.12f;
                float f = notes[n];
                return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * t)) * 0.6f * Mathf.Exp(-local * (n == notes.Length - 1 ? 6 : 14));
            });
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        static AudioClip Make(string name, float duration, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(duration * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float fadeOut = Mathf.Clamp01((count - i) / (Rate * 0.005f));   // avoid clicks
                data[i] = Mathf.Clamp(sample(t) * fadeOut, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip clip, float pitch, float vol)
        {
            var s = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            s.pitch = pitch;
            s.PlayOneShot(clip, vol * volume);
        }

        public void Hit(bool fromTap) => Play(_hit, UnityEngine.Random.Range(0.9f, 1.15f), fromTap ? 0.7f : 0.25f);
        public void Crack(float size) => Play(_crack, Mathf.Clamp(1.3f - size * 0.3f, 0.6f, 1.3f), 0.8f);
        public void Sell() => Play(_sell, 1f, 0.8f);
        public void Deny() => Play(_deny, 1f, 0.5f);
        public void Rare(int grade) => Play(_rare, 1f + 0.06f * grade, 0.9f);

        public void Pickup(int grade)
        {
            if (Time.unscaledTime - _lastPickup < 0.03f) return;
            _lastPickup = Time.unscaledTime;
            Play(_pickup, 1f + 0.15f * grade, 0.35f + 0.1f * grade);
        }
    }
}
