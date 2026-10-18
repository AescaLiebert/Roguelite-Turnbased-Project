using System;
using UnityEngine;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Owned, deterministic reveal cues. Generating these never advances the summon RNG.</summary>
    internal sealed class SummonSequenceAudio : IDisposable
    {
        private const int SampleRate = 44100;
        private readonly AudioClip[] _arrivals = new AudioClip[3];
        private readonly AudioClip[] _charges = new AudioClip[3];
        private readonly AudioClip[] _impacts = new AudioClip[3];
        private GameObject _owner;
        private AudioSource _source;
        private bool _disposed;

        public void PlayArrival(FighterRarity rarity, int index)
        {
            if (!CanPlay()) return;
            var variant = ((index % 3) + 3) % 3;
            if (_arrivals[variant] == null) _arrivals[variant] = CreateArrival(variant);
            Play(_arrivals[variant], .68f + Band(rarity) * .06f, .97f + Band(rarity) * .025f);
        }

        public void PlayCharge(FighterRarity rarity)
        {
            if (!CanPlay()) return;
            var band = Band(rarity);
            if (_charges[band] == null) _charges[band] = CreateCharge(band);
            Play(_charges[band], .82f, 1f);
        }

        public void PlayImpact(FighterRarity rarity)
        {
            if (!CanPlay()) return;
            var band = Band(rarity);
            if (_impacts[band] == null) _impacts[band] = CreateImpact(band);
            Play(_impacts[band], .88f, 1f);
        }

        private bool CanPlay()
        {
            if (_disposed || !Application.isPlaying) return false;
            if (_source != null) return true;
            _owner = new GameObject("Summon sequence audio") { hideFlags = HideFlags.HideAndDontSave };
            _source = _owner.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.ignoreListenerPause = true;
            _source.bypassReverbZones = true;
            _source.priority = 96;
            return true;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            // Each cue replaces the previous voice, keeping the complete output below the peak cap.
            _source.Stop();
            _source.clip = clip;
            _source.volume = volume;
            _source.pitch = pitch;
            _source.Play();
        }

        private static int Band(FighterRarity rarity) => rarity >= FighterRarity.SSR ? 2 : rarity == FighterRarity.SR ? 1 : 0;

        private static AudioClip CreateArrival(int variant)
        {
            // Starting synthesis values: a quick air sweep follows the upward fade, with a
            // delayed bright glint at the settled pose. Compare at ordinary ten-pull speed.
            var duration = .27f + variant * .012f;
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            var frequency = 1740f + variant * 146f;
            var noiseState = 0x71A2C59Du + (uint)variant * 977u;
            var lowNoise = 0f; var softNoise = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)SampleRate;
                var noise = Noise(ref noiseState);
                var sweep = Smooth(time / .11f);
                lowNoise += (noise - lowNoise) * Mathf.Lerp(.045f, .33f, sweep);
                softNoise += (noise - softNoise) * .016f;
                var air = ((lowNoise - softNoise) * .82f + (noise - lowNoise) * .13f) *
                    Smooth(time / .009f) * Mathf.Exp(-time * 23f);
                var glintTime = Mathf.Max(0, time - .045f);
                var glass = .18f * Sine(frequency * glintTime) + .13f * Sine(frequency * 1.503f * glintTime) +
                    .095f * Sine(frequency * 2.027f * glintTime) + .055f * Sine(frequency * 2.739f * glintTime);
                var glint = glass * Smooth(glintTime / .007f) * Mathf.Exp(-glintTime * 22f);
                var sparkleTime = Mathf.Max(0, time - .105f);
                var sparkle = (.065f * Sine(frequency * 2.52f * sparkleTime) + .035f * Sine(frequency * 3.17f * sparkleTime)) *
                    Smooth(sparkleTime / .004f) * Mathf.Exp(-sparkleTime * 34f);
                samples[i] = (air + glint + sparkle) * Smooth((duration - time) / .035f);
            }
            return Clip("Summon airy settle shimmer " + variant, samples, .23f);
        }

        private static AudioClip CreateCharge(int band)
        {
            // The visual impact is at 1.35 s; this air/metal rise reaches it without a dead gap.
            const float duration = 1.34f;
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            var noiseState = 0x49DE82B1u + (uint)band * 1223u;
            var lowNoise = 0f; var softNoise = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)SampleRate;
                var progress = time / duration;
                var noise = Noise(ref noiseState);
                lowNoise += (noise - lowNoise) * Mathf.Lerp(.016f, .35f, progress * progress);
                softNoise += (noise - softNoise) * .009f;
                var air = (lowNoise - softNoise) * .66f + (noise - lowNoise) * .21f * progress;
                var risingPhase = (92f + band * 11f) * time + (315f + band * 65f) * time * time / duration;
                var sheenPhase = 930f * time + 1700f * time * time / duration;
                var pulsePhase = 8f * time + 8f * time * time / duration;
                var pulse = .88f + .12f * Sine(pulsePhase);
                var mechanical = .13f * Sine(risingPhase) + .065f * Sine(risingPhase * 2.009f) +
                    .028f * Sine(risingPhase * 3.071f);
                var teeth = Mathf.Pow(Mathf.Max(0, Sine(pulsePhase)), 14f) * air * .13f;
                var energy = (mechanical + .024f * Sine(sheenPhase) + air + teeth) * pulse;
                var envelope = Smooth(time / .1f) * Smooth((duration - time) / .028f) * Mathf.Lerp(.055f, 1f, Mathf.Pow(progress, 1.65f));
                var impulse = (.34f * Sine(64f * time + 2.8f * (1 - Mathf.Exp(-time * 34f))) +
                    (noise - lowNoise) * .14f) * Smooth(time / .003f) * Mathf.Exp(-time * 32f);
                samples[i] = energy * envelope + impulse;
            }
            return Clip("Summon mechanical air rise " + band, samples, .31f);
        }

        private static AudioClip CreateImpact(int band)
        {
            const float duration = 1.06f;
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            var noiseState = 0x5E84AC73u + (uint)band * 1877u;
            var root = 293.665f * (band == 2 ? 1.33484f : band == 1 ? 1.12246f : 1f);
            var lowNoise = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)SampleRate;
                var noise = Noise(ref noiseState);
                lowNoise += (noise - lowNoise) * .12f;
                var thumpPhase = 52f * time + 100f * (1f - Mathf.Exp(-time * 35f)) / 35f;
                var thump = .48f * Sine(thumpPhase) * Mathf.Exp(-time * 25f);
                var chord = (Sine(root * time) + .64f * Sine(root * 1.25f * time) +
                             .58f * Sine(root * 1.5f * time) + .28f * Sine(root * 2f * time)) * .19f;
                var shimmer = (.13f * Sine(root * 4.013f * time) + .085f * Sine(root * 5.031f * time) +
                    .055f * Sine(root * 6.091f * time)) * Mathf.Exp(-time * 6.8f);
                var lateTime = Mathf.Max(0, time - .075f);
                var lateGlint = (.072f * Sine(root * 6.01f * lateTime) + .045f * Sine(root * 8.027f * lateTime)) *
                    Smooth(lateTime / .012f) * Mathf.Exp(-lateTime * 9f);
                var air = (noise - lowNoise) * .35f * Mathf.Exp(-time * 32f);
                // Short, irregular glass fractures follow the main strike, matching the flying shards.
                var fractures = 0f;
                for (var crack = 0; crack < 5; crack++)
                {
                    var age = time - (.018f + crack * .029f + (crack % 2) * .011f);
                    if (age < 0 || age > .09f) continue;
                    fractures += ((noise - lowNoise) * .2f + .055f * Sine((3370f + crack * 529f) * age)) *
                        Smooth(age / .0015f) * Mathf.Exp(-age * (65f + crack * 8f));
                }
                var envelope = Smooth(time / .0025f) * Smooth((duration - time) / .22f);
                samples[i] = (thump + chord * Mathf.Exp(-time * 4.2f) + shimmer + lateGlint + air + fractures) * envelope;
            }
            return Clip("Summon crystalline reveal chord " + band, samples, .36f);
        }

        private static float Noise(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFFu) / 8388607.5f - 1f;
        }

        private static float Sine(float cycles) => Mathf.Sin(cycles * Mathf.PI * 2f);
        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static AudioClip Clip(string name, float[] samples, float peak)
        {
            var maximum = .0001f;
            for (var i = 0; i < samples.Length; i++) maximum = Mathf.Max(maximum, Mathf.Abs(samples[i]));
            var gain = Mathf.Min(.36f, peak) / maximum;
            for (var i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -.36f, .36f);
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        public void Stop()
        {
            if (_source != null) _source.Stop();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_source != null) { _source.Stop(); _source.clip = null; }
            DestroyOwned(_owner);
            _owner = null;
            _source = null;
            DestroyClips(_arrivals);
            DestroyClips(_charges);
            DestroyClips(_impacts);
        }

        private static void DestroyClips(AudioClip[] clips)
        {
            for (var i = 0; i < clips.Length; i++) { DestroyOwned(clips[i]); clips[i] = null; }
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
