using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Plays battle impact sounds from the shared library, with generated defaults when clips are unassigned.</summary>
    [DisallowMultipleComponent]
    public sealed class SFXPlayer : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private static readonly int[] DefaultLengths = { 4851, 7938 };
        private static readonly AudioClip[] DefaultClips = new AudioClip[2];
        private AudioSource _source;
        private SFXLibrary _library;

        public void Initialize(SFXLibrary library = null)
        {
            _library = library != null ? library : Resources.Load<SFXLibrary>("Audio/SFXLibrary");
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
                if (_source == null) _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = false;
                _source.spatialBlend = 0f;
                _source.dopplerLevel = 0f;
                _source.priority = 96;
            }
        }

        public void PlayHit(bool critical)
        {
            if (!Application.isPlaying) return;
            if (_source == null) Initialize();
            var index = critical ? 1 : 0;
            var clip = critical ? _library?.CriticalHit : _library?.Hit;
            if (clip == null) clip = GetDefault(index);
            _source.PlayOneShot(clip, critical ? (_library != null ? _library.CriticalVolume : .7f) : (_library != null ? _library.HitVolume : .55f));
        }

        private static AudioClip GetDefault(int index)
        {
            if (DefaultClips[index] != null) return DefaultClips[index];
            var length = DefaultLengths[index];
            var samples = new float[length];
            var duration = length / (float)SampleRate;
            var noise = index == 0 ? 0x3A41C2Du : 0x71B9E43u;
            var lowPass = 0f;
            for (var i = 0; i < length; i++)
            {
                var t = i / (float)SampleRate;
                var progress = t / duration;
                noise ^= noise << 13;
                noise ^= noise >> 17;
                noise ^= noise << 5;
                var white = ((noise & 0x7fffffff) / 1073741824f) - 1f;
                lowPass += (white - lowPass) * (index == 0 ? .22f : .12f);
                var envelope = Mathf.Pow(1f - progress, index == 0 ? 3.5f : 2.5f);
                var body = Mathf.Sin(2f * Mathf.PI * (index == 0 ? 105f : 155f) * t - 7f * t * t);
                var transient = Mathf.Sin(2f * Mathf.PI * (index == 0 ? 1250f : 2050f) * t) * Mathf.Exp(-t * (index == 0 ? 34f : 25f));
                samples[i] = Mathf.Clamp((body * .58f + lowPass * (index == 0 ? .33f : .25f) + transient * (index == 0 ? .2f : .45f)) * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create(index == 0 ? "Default Hit SFX" : "Default Critical Hit SFX", length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            DefaultClips[index] = clip;
            return clip;
        }

        private void OnDestroy()
        {
            if (_source != null) _source.Stop();
        }
    }
}
