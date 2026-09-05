// VNAudio.cs -- music and effects. Drops in real clips if present, synthesises them if not.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// Audio for a project that ships with no audio files. Anything under
    /// Resources/VN/Audio/BGM or /SFX is used verbatim; every other name is generated,
    /// so a scenario can call @bgm and @sfx freely from day one.
    /// </summary>
    public class VNAudio : MonoBehaviour
    {
        const int SampleRate = 44100;

        AudioSource _bgm;
        AudioSource _sfx;
        Coroutine _fade;
        string _currentBgm = "";

        readonly Dictionary<string, AudioClip> _generated = new Dictionary<string, AudioClip>();

        public string CurrentBgm { get { return _currentBgm; } }

        void Awake()
        {
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.volume = 0f;
            _bgm.spatialBlend = 0f;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.loop = false;
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
        }

        // ---------------------------------------------------------------- music

        public void PlayBgm(string name, float fade = 1.2f)
        {
            if (string.IsNullOrEmpty(name) || name == "stop" || name == "none")
            {
                StopBgm(fade);
                return;
            }
            if (_currentBgm == name && _bgm.isPlaying) return;

            var clip = VNAssets.LoadClip("VN/Audio/BGM/" + name) ?? GeneratePad(name);
            _currentBgm = name;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(SwapBgm(clip, fade));
        }

        public void StopBgm(float fade = 0.8f)
        {
            _currentBgm = "";
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(SwapBgm(null, fade));
        }

        public void RefreshVolume()
        {
            if (_bgm != null && _bgm.isPlaying && !string.IsNullOrEmpty(_currentBgm))
                _bgm.volume = VNSettings.BgmVolume;
        }

        IEnumerator SwapBgm(AudioClip next, float fade)
        {
            float start = _bgm.volume;
            if (_bgm.isPlaying && fade > 0f)
            {
                for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
                {
                    _bgm.volume = Mathf.Lerp(start, 0f, t / fade);
                    yield return null;
                }
            }
            _bgm.Stop();
            _bgm.volume = 0f;

            if (next == null) { _fade = null; yield break; }

            _bgm.clip = next;
            _bgm.Play();
            float target = VNSettings.BgmVolume;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                _bgm.volume = Mathf.Lerp(0f, target, fade <= 0f ? 1f : t / fade);
                yield return null;
            }
            _bgm.volume = VNSettings.BgmVolume;
            _fade = null;
        }

        // ---------------------------------------------------------------- effects

        public void PlaySfx(string name, float volumeScale = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(name)) return;
            var clip = VNAssets.LoadClip("VN/Audio/SFX/" + name) ?? GenerateSfx(name);
            if (clip == null) return;
            _sfx.pitch = pitch;
            _sfx.PlayOneShot(clip, Mathf.Clamp01(VNSettings.SfxVolume * volumeScale));
        }

        public void PlayType()
        {
            if (!VNSettings.TypeSound) return;
            PlaySfx("_type", 0.16f, Random.Range(0.94f, 1.10f));
        }

        public void PlayClick() { PlaySfx("click", 0.5f); }

        // ---------------------------------------------------------------- synthesis

        /// <summary>
        /// A slow, quiet pad. Every partial frequency is snapped to a multiple of 1/length,
        /// which makes the buffer loop with no click at the seam.
        /// </summary>
        AudioClip GeneratePad(string name)
        {
            string key = "pad:" + name;
            AudioClip cached;
            if (_generated.TryGetValue(key, out cached) && cached != null) return cached;

            const float length = 8f;
            int count = Mathf.RoundToInt(SampleRate * length);
            var data = new float[count];

            int hash = Mathf.Abs(name.GetHashCode());
            float root = 110f * Mathf.Pow(2f, (hash % 7) / 12f);       // A2 up a few semitones
            bool minor = (hash / 7) % 2 == 0;
            float third = root * (minor ? 1.1892f : 1.2599f);
            float fifth = root * 1.4983f;

            float[] partials = { root, root * 2f, third * 2f, fifth * 2f, root * 4f, fifth * 4f };
            float[] gains = { 0.42f, 0.26f, 0.20f, 0.17f, 0.09f, 0.06f };

            for (int p = 0; p < partials.Length; p++)
                partials[p] = Mathf.Round(partials[p] * length) / length;   // snap for a seamless loop

            float lfoA = Mathf.Round(0.11f * length) / length;
            float lfoB = Mathf.Round(0.07f * length) / length;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    float trem = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * (lfoA + p * lfoB * 0.13f) * t);
                    s += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * gains[p] * trem;
                }
                float swell = 0.62f + 0.38f * Mathf.Sin(2f * Mathf.PI * lfoB * t);
                data[i] = s * 0.16f * swell;
            }

            var clip = AudioClip.Create("vn_pad_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            _generated[key] = clip;
            return clip;
        }

        AudioClip GenerateSfx(string name)
        {
            string key = "sfx:" + name;
            AudioClip cached;
            if (_generated.TryGetValue(key, out cached) && cached != null) return cached;

            float length;
            float[] data;

            switch (name)
            {
                case "_type":
                    length = 0.032f;
                    data = Buffer(length);
                    Fill(data, (t, n) => Mathf.Sin(2f * Mathf.PI * 1750f * t) * Mathf.Exp(-t * 190f) * 0.7f);
                    break;

                case "click":
                    length = 0.09f;
                    data = Buffer(length);
                    Fill(data, (t, n) =>
                        (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.4f)
                        * Mathf.Exp(-t * 42f));
                    break;

                case "chime":
                    length = 1.4f;
                    data = Buffer(length);
                    Fill(data, (t, n) =>
                        (Mathf.Sin(2f * Mathf.PI * 784f * t) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * 1175f * t) * 0.32f
                       + Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.18f) * Mathf.Exp(-t * 3.4f) * 0.7f);
                    break;

                case "impact":
                case "thud":
                    length = 0.6f;
                    data = Buffer(length);
                    Fill(data, (t, n) =>
                        (Mathf.Sin(2f * Mathf.PI * (110f - 60f * t) * t) * 0.8f + (Random.value * 2f - 1f) * 0.25f)
                        * Mathf.Exp(-t * 8f));
                    break;

                case "whoosh":
                case "wind":
                    length = 1.1f;
                    data = Buffer(length);
                    float lp = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float t = i / (float)SampleRate;
                        float noise = Random.value * 2f - 1f;
                        lp += (noise - lp) * 0.06f;                       // one-pole low pass
                        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
                        data[i] = lp * env * 0.55f;
                    }
                    break;

                case "wave":
                case "sea":
                    length = 2.4f;
                    data = Buffer(length);
                    float lp2 = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float t = i / (float)SampleRate;
                        float noise = Random.value * 2f - 1f;
                        lp2 += (noise - lp2) * 0.02f;
                        float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length)), 1.6f);
                        data[i] = lp2 * env * 0.7f;
                    }
                    break;

                case "signal":
                case "alarm":
                    length = 1.0f;
                    data = Buffer(length);
                    Fill(data, (t, n) =>
                        Mathf.Sin(2f * Mathf.PI * 660f * t + Mathf.Sin(2f * Mathf.PI * 6f * t) * 3f)
                        * Mathf.Exp(-t * 2.2f) * 0.5f);
                    break;

                default:
                    length = 0.16f;
                    data = Buffer(length);
                    Fill(data, (t, n) => Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 20f) * 0.5f);
                    break;
            }

            var clip = AudioClip.Create("vn_sfx_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            _generated[key] = clip;
            return clip;
        }

        static float[] Buffer(float seconds)
        {
            return new float[Mathf.Max(16, Mathf.RoundToInt(SampleRate * seconds))];
        }

        static void Fill(float[] data, System.Func<float, int, float> f)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = f(i / (float)SampleRate, i);
        }
    }
}
