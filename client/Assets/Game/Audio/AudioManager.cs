using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;

namespace Worms.Game.Audio
{
    /// <summary>
    /// Plays the synthesized stereo sounds (SfxSynth) through a small pool of 2D
    /// AudioSources, balanced toward where they happen on screen, plus the background loop.
    /// Everything is generated and played here on the client; the server only sends events.
    /// Volumes are multiplied in code (no AudioMixer effects, docs/PLAN.md §3.14).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const string PrefSfx = "worms.vol.sfx", PrefMusic = "worms.vol.music", PrefMuted = "worms.vol.muted";
        const int Voices = 14;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly AudioSource[] _voices = new AudioSource[Voices];
        AudioSource _music;
        int _next;

        public float SfxVolume { get; private set; } = 0.8f;
        public float MusicVolume { get; private set; } = 0.35f;
        public bool Muted { get; private set; }

        void Awake()
        {
            Instance = this;
            gameObject.AddComponent<AudioListener>();
            SfxVolume = PlayerPrefs.GetFloat(PrefSfx, SfxVolume);
            MusicVolume = PlayerPrefs.GetFloat(PrefMusic, MusicVolume);
            Muted = PlayerPrefs.GetInt(PrefMuted, 0) == 1;
            for (int i = 0; i < Voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0;
                _voices[i] = src;
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0;
            foreach (Sfx s in Enum.GetValues(typeof(Sfx))) _clips[s] = Clip(s.ToString(), SfxSynth.Make(s));
            StartCoroutine(BuildMusic());
        }

        /// <summary>A clip from interleaved stereo PCM.</summary>
        static AudioClip Clip(string name, float[] pcm)
        {
            var clip = AudioClip.Create(name, pcm.Length / SfxSynth.Channels, SfxSynth.Channels, SfxSynth.Rate, false);
            clip.SetData(pcm, 0);
            return clip;
        }

        IEnumerator BuildMusic()
        {
            yield return null; // after the first frame, so startup stays quick
            _music.clip = Clip("Music", SfxSynth.Music(7));
            ApplyVolumes();
            _music.Play();
        }

        public void Play(Sfx sfx, float pan = 0f, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (Muted || SfxVolume <= 0 || !_clips.TryGetValue(sfx, out var clip)) return;
            var src = _voices[_next];
            _next = (_next + 1) % Voices;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * SfxVolume;
            src.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            src.panStereo = Mathf.Clamp(pan, -1f, 1f) * 0.6f;
            src.Play();
        }

        /// <summary>Stereo pan for a world X given the camera's X and half the visible width.</summary>
        public static float PanFor(float worldX, float cameraX, float halfWidth)
        {
            return halfWidth <= 0 ? 0 : Mathf.Clamp((worldX - cameraX) / halfWidth, -1f, 1f);
        }

        public void SetVolumes(float sfx, float music)
        {
            SfxVolume = Mathf.Clamp01(sfx);
            MusicVolume = Mathf.Clamp01(music);
            PlayerPrefs.SetFloat(PrefSfx, SfxVolume);
            PlayerPrefs.SetFloat(PrefMusic, MusicVolume);
            ApplyVolumes();
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(PrefMuted, muted ? 1 : 0);
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            _music.volume = Muted ? 0 : MusicVolume;
        }
    }
}
