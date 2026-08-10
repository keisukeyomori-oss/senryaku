using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BirthdayTactics.Presentation
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _missingTracks = new HashSet<string>();
        private AudioSource _primary;
        private AudioSource _secondary;
        private AudioSource _effects;
        private AudioSource _active;
        private Coroutine _fadeRoutine;
        private float _volume = 0.8f;
        private bool _muted;

        public float Volume => _volume;
        public bool Muted => _muted;
        public string CurrentTrack { get; private set; } = string.Empty;

        public void Initialize(float volume, bool muted)
        {
            _primary = CreateSource("BGM A", true);
            _secondary = CreateSource("BGM B", true);
            _effects = CreateSource("SE", false);
            _active = _primary;
            SetVolume(volume);
            SetMuted(muted);
        }

        public void PlayBgm(string trackId, float fadeSeconds)
        {
            if (CurrentTrack == trackId && _active != null && _active.isPlaying) return;
            CurrentTrack = trackId ?? string.Empty;
            AudioClip clip = LoadTrack(trackId);
            if (clip == null) return;

            AudioSource next = _active == _primary ? _secondary : _primary;
            next.clip = clip;
            next.loop = true;
            next.volume = 0f;
            next.Play();
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Crossfade(_active, next, Mathf.Max(0f, fadeSeconds)));
            _active = next;
        }

        public void SetVolume(float value)
        {
            _volume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetMuted(bool value)
        {
            _muted = value;
            ApplyVolumes();
        }

        public void PlaySfx(string effectId)
        {
            if (_muted || _effects == null) return;
            AudioClip clip = EffectClip(effectId);
            if (clip != null) _effects.PlayOneShot(clip, _volume * 0.7f);
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private AudioClip LoadTrack(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId)) return null;
            if (_clips.TryGetValue(trackId, out AudioClip cached)) return cached;
            AudioClip loaded = Resources.Load<AudioClip>($"Audio/{trackId}");
            if (loaded != null)
            {
                _clips[trackId] = loaded;
                return loaded;
            }

            AudioClip fallback = CreateFallbackTrack(trackId);
            if (fallback != null)
            {
                _clips[trackId] = fallback;
                return fallback;
            }

            if (_missingTracks.Add(trackId))
                Debug.LogWarning($"BGM {trackId} is not installed. Continuing silently.");
            return null;
        }

        private static AudioClip CreateFallbackTrack(string trackId)
        {
            switch (trackId)
            {
                case "BD-01":
                    return CreateGentleTitleLoop();
                case "BD-02":
                    return CreateBattleLoop();
                case "BD-03":
                    return CreateRuinsLoop();
                case "BD-04":
                    return CreateHeadquartersLoop();
                case "BD-05":
                    return CreateJourneyLoop();
                default:
                    return null;
            }
        }

        private static AudioClip CreateGentleTitleLoop()
        {
            const float beatsPerMinute = 72f;
            const int beatsPerBar = 3;
            const int bars = 8;
            float beatSeconds = 60f / beatsPerMinute;
            float duration = beatSeconds * beatsPerBar * bars;
            float[] samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            int[] roots = { 48, 45, 41, 43, 48, 45, 43, 48 };
            int[] melody = { 67, 69, 71, 72, 71, 69, 67, 64, 65, 67, 69, 67, 64, 62, 64, 67, 69, 72, 71, 69, 67, 64, 62, 60 };

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * beatsPerBar * beatSeconds;
                int root = roots[bar];
                AddTone(samples, barStart, beatSeconds * 2.85f, MidiFrequency(root), 0.15f, 0.025f, 1.8f);
                AddTone(samples, barStart + beatSeconds, beatSeconds * 1.75f, MidiFrequency(root + 7), 0.09f, 0.02f, 1.4f);
                AddTone(samples, barStart + beatSeconds * 2f, beatSeconds * 0.8f, MidiFrequency(root + 12), 0.07f, 0.02f, 1.2f);

                for (int beat = 0; beat < beatsPerBar; beat++)
                {
                    int note = melody[bar * beatsPerBar + beat];
                    AddTone(
                        samples,
                        barStart + beat * beatSeconds,
                        beatSeconds * 0.82f,
                        MidiFrequency(note),
                        0.16f,
                        0.015f,
                        1.5f);
                }
            }

            return CreateLoopClip("BD-01 procedural fallback", samples);
        }

        private static AudioClip CreateBattleLoop()
        {
            const float beatsPerMinute = 155f;
            const int beatsPerBar = 4;
            const int bars = 8;
            float beatSeconds = 60f / beatsPerMinute;
            float duration = beatSeconds * beatsPerBar * bars;
            float[] samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            int[] roots = { 45, 45, 41, 43, 45, 48, 41, 43 };
            int[] lead = { 69, 72, 76, 74, 69, 72, 77, 76, 67, 69, 72, 74, 67, 71, 74, 76 };

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * beatsPerBar * beatSeconds;
                int root = roots[bar];
                for (int eighth = 0; eighth < beatsPerBar * 2; eighth++)
                {
                    float start = barStart + eighth * beatSeconds * 0.5f;
                    AddTone(samples, start, beatSeconds * 0.42f, MidiFrequency(root), 0.11f, 0.008f, 0.42f);
                    AddTone(samples, start, beatSeconds * 0.38f, MidiFrequency(root + 12), 0.035f, 0.006f, 0.35f);
                }

                for (int beat = 0; beat < beatsPerBar; beat++)
                {
                    float start = barStart + beat * beatSeconds;
                    AddPercussion(samples, start, beatSeconds * 0.42f, beat == 0 || beat == 2, bar * 17 + beat);
                }

                for (int half = 0; half < 2; half++)
                {
                    int note = lead[bar * 2 + half];
                    float start = barStart + half * beatSeconds * 2f;
                    AddTone(samples, start, beatSeconds * 1.75f, MidiFrequency(note), 0.13f, 0.01f, 0.75f);
                    AddTone(samples, start, beatSeconds * 1.65f, MidiFrequency(note - 12), 0.045f, 0.008f, 0.7f);
                }
            }

            return CreateLoopClip("BD-02 procedural fallback", samples);
        }

        private static AudioClip CreateRuinsLoop()
        {
            const float beatsPerMinute = 66f;
            const int beatsPerBar = 4;
            const int bars = 8;
            float beatSeconds = 60f / beatsPerMinute;
            float duration = beatSeconds * beatsPerBar * bars;
            float[] samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            int[] roots = { 38, 38, 41, 36, 38, 43, 41, 38 };
            int[] bells = { 62, 65, 69, 67, 62, 70, 69, 65 };

            for (int bar = 0; bar < bars; bar++)
            {
                float start = bar * beatsPerBar * beatSeconds;
                int root = roots[bar];
                AddTone(samples, start, beatSeconds * 3.9f, MidiFrequency(root), 0.105f, 0.65f, 2.8f);
                AddTone(samples, start, beatSeconds * 3.7f, MidiFrequency(root + 7), 0.052f, 0.8f, 2.6f);
                AddTone(samples, start + beatSeconds * 0.5f, beatSeconds * 1.8f,
                    MidiFrequency(bells[bar]), 0.075f, 0.008f, 1.4f);
                AddTone(samples, start + beatSeconds * 2.5f, beatSeconds * 1.3f,
                    MidiFrequency(bells[bar] + (bar % 2 == 0 ? 7 : 5)), 0.052f, 0.006f, 1.0f);
            }

            return CreateLoopClip("BD-03 Moonlit Ruins - original procedural score", samples);
        }

        private static AudioClip CreateHeadquartersLoop()
        {
            const float beatsPerMinute = 92f;
            const int beatsPerBar = 6;
            const int bars = 8;
            float pulseSeconds = 60f / beatsPerMinute * 0.5f;
            float duration = pulseSeconds * beatsPerBar * bars;
            float[] samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            int[] roots = { 48, 43, 45, 41, 48, 45, 43, 48 };
            int[] melody =
            {
                64, 67, 69, 67, 64, 62,
                62, 64, 67, 69, 67, 64,
                60, 64, 65, 69, 67, 65,
                62, 65, 67, 65, 62, 60,
                64, 67, 72, 71, 69, 67,
                65, 69, 72, 74, 72, 69,
                62, 67, 71, 69, 67, 65,
                64, 67, 69, 67, 64, 60
            };

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * beatsPerBar * pulseSeconds;
                int root = roots[bar];
                AddTone(samples, barStart, pulseSeconds * 5.8f, MidiFrequency(root), 0.105f, 0.025f, 1.8f);
                AddTone(samples, barStart, pulseSeconds * 5.6f, MidiFrequency(root + 7), 0.047f, 0.03f, 1.7f);
                for (int pulse = 0; pulse < beatsPerBar; pulse++)
                {
                    float start = barStart + pulse * pulseSeconds;
                    int note = melody[bar * beatsPerBar + pulse];
                    AddTone(samples, start, pulseSeconds * 0.78f,
                        MidiFrequency(note), 0.112f, 0.012f, 0.38f);
                    if (pulse == 0 || pulse == 3)
                        AddTone(samples, start, pulseSeconds * 0.62f,
                            MidiFrequency(root + 12), 0.055f, 0.006f, 0.26f);
                }
            }

            return CreateLoopClip("BD-04 Hall of Lights - original procedural score", samples);
        }

        private static AudioClip CreateJourneyLoop()
        {
            const float beatsPerMinute = 112f;
            const int beatsPerBar = 4;
            const int bars = 8;
            float beatSeconds = 60f / beatsPerMinute;
            float duration = beatSeconds * beatsPerBar * bars;
            float[] samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            int[] roots = { 43, 45, 47, 48, 43, 50, 45, 43 };
            int[] lead = { 67, 69, 71, 74, 72, 71, 69, 67, 69, 72, 74, 76, 74, 71, 69, 67 };

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * beatsPerBar * beatSeconds;
                int root = roots[bar];
                for (int eighth = 0; eighth < beatsPerBar * 2; eighth++)
                {
                    float start = barStart + eighth * beatSeconds * 0.5f;
                    int note = root + (eighth % 4 == 2 ? 7 : eighth % 4 == 3 ? 12 : 0);
                    AddTone(samples, start, beatSeconds * 0.38f,
                        MidiFrequency(note), 0.070f, 0.006f, 0.22f);
                }
                for (int half = 0; half < 2; half++)
                {
                    int note = lead[bar * 2 + half];
                    float start = barStart + half * beatSeconds * 2f;
                    AddTone(samples, start, beatSeconds * 1.72f,
                        MidiFrequency(note), 0.125f, 0.045f, 0.85f);
                    AddTone(samples, start, beatSeconds * 1.64f,
                        MidiFrequency(note - 12), 0.032f, 0.04f, 0.78f);
                }
                AddPercussion(samples, barStart, beatSeconds * 0.34f, true, bar * 31);
                AddPercussion(samples, barStart + beatSeconds * 2f, beatSeconds * 0.30f, false, bar * 31 + 1);
            }

            return CreateLoopClip("BD-05 Roads of the Lake - original procedural score", samples);
        }

        private static void AddTone(
            float[] samples,
            float startSeconds,
            float durationSeconds,
            float frequency,
            float amplitude,
            float attackSeconds,
            float decaySeconds)
        {
            int start = Mathf.Max(0, Mathf.RoundToInt(startSeconds * SampleRate));
            int length = Mathf.Min(
                samples.Length - start,
                Mathf.Max(1, Mathf.RoundToInt(durationSeconds * SampleRate)));
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)SampleRate;
                float attack = Mathf.Clamp01(time / Mathf.Max(0.001f, attackSeconds));
                float release = Mathf.Clamp01((durationSeconds - time) / Mathf.Max(0.001f, decaySeconds));
                float envelope = attack * release * release;
                float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time);
                float harmonic = Mathf.Sin(4f * Mathf.PI * frequency * time) * 0.28f;
                samples[start + i] += (fundamental + harmonic) * envelope * amplitude;
            }
        }

        private static void AddPercussion(
            float[] samples,
            float startSeconds,
            float durationSeconds,
            bool kick,
            int seed)
        {
            int start = Mathf.Max(0, Mathf.RoundToInt(startSeconds * SampleRate));
            int length = Mathf.Min(
                samples.Length - start,
                Mathf.Max(1, Mathf.RoundToInt(durationSeconds * SampleRate)));
            uint noise = (uint)(seed + 1) * 747796405u + 2891336453u;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = 1f - i / (float)length;
                noise = noise * 1664525u + 1013904223u;
                float random = ((noise >> 8) / 16777215f) * 2f - 1f;
                float value = kick
                    ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(115f, 52f, time / durationSeconds) * time) * 0.22f
                    : random * 0.11f;
                samples[start + i] += value * envelope * envelope;
            }
        }

        private static AudioClip CreateLoopClip(string name, float[] samples)
        {
            int fadeSamples = Mathf.Min(samples.Length / 8, Mathf.RoundToInt(SampleRate * 0.035f));
            for (int i = 0; i < samples.Length; i++)
            {
                float edge = 1f;
                if (i < fadeSamples) edge = i / (float)fadeSamples;
                else if (i >= samples.Length - fadeSamples)
                    edge = (samples.Length - 1 - i) / (float)fadeSamples;
                samples[i] = Mathf.Clamp(samples[i] * edge, -0.82f, 0.82f);
            }

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float MidiFrequency(int midiNote)
        {
            return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
        }

        private IEnumerator Crossfade(AudioSource previous, AudioSource next, float seconds)
        {
            float elapsed = 0f;
            float previousVolume = previous == null ? 0f : previous.volume;
            if (seconds <= 0f)
            {
                if (previous != null) previous.Stop();
                ApplyVolumes();
                yield break;
            }

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                if (previous != null) previous.volume = Mathf.Lerp(previousVolume, 0f, t);
                next.volume = Mathf.Lerp(0f, _muted ? 0f : _volume, t);
                yield return null;
            }

            if (previous != null) previous.Stop();
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            float target = _muted ? 0f : _volume;
            if (_primary != null) _primary.volume = _primary == _active ? target : 0f;
            if (_secondary != null) _secondary.volume = _secondary == _active ? target : 0f;
        }

        private AudioClip EffectClip(string effectId)
        {
            string key = $"se:{effectId}";
            if (_clips.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip installed = Resources.Load<AudioClip>($"Audio/SFX/{effectId}");
            if (installed != null)
            {
                _clips[key] = installed;
                return installed;
            }

            float frequency;
            float duration;
            switch (effectId)
            {
                case "move": frequency = 520f; duration = 0.07f; break;
                case "attack": frequency = 170f; duration = 0.12f; break;
                case "victory": frequency = 880f; duration = 0.28f; break;
                case "gift": frequency = 660f; duration = 0.42f; break;
                default: frequency = 420f; duration = 0.06f; break;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = 1f - i / (float)count;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.22f;
            }

            AudioClip clip = AudioClip.Create(key, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            _clips[key] = clip;
            return clip;
        }
    }
}
