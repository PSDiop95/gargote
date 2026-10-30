using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    /// <summary>
    /// Sons placeholders via la SoundBank : pool de sources préalloué, limitation de voix et d'intervalle par son,
    /// variation de pitch, ambiance de rue selon l'activité, musique jour/nuit en fondu croisé, klaxon lointain.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string MasterParam = "MasterVolume";
        public const string MusicParam = "MusicVolume";
        public const string SfxParam = "SfxVolume";
        public const string VoiceParam = "VoiceVolume";

        private AudioConfig _config;
        private SoundEntry[] _entries;
        private float[] _lastPlayed;
        private AudioSource[] _voices;
        private int[] _voiceSound;
        private float[] _voiceEnd;
        private AudioSource _ambience, _grill, _musicDay, _musicNight;
        private float _nightBlend, _hornTimer;
        private uint _random = 0xA341316Cu;

        public int VoiceCount => _voices?.Length ?? 0;
        public int PlayingVoices { get; private set; }

        public void Setup(AudioConfig config)
        {
            _config = config;
            int count = System.Enum.GetValues(typeof(SoundId)).Length;
            _entries = new SoundEntry[count];
            _lastPlayed = new float[count];
            for (int i = 0; i < count; i++)
            {
                _entries[i] = config.bank != null ? config.bank.Find((SoundId)i) : null;
                _lastPlayed[i] = -999f;
            }

            _voices = new AudioSource[Mathf.Max(1, config.sfxVoices)];
            _voiceSound = new int[_voices.Length];
            _voiceEnd = new float[_voices.Length];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = CreateSource("Sfx" + i, config.sfx, false);
                _voiceSound[i] = -1;
            }

            _ambience = Loop(SoundId.CrowdLoop, config.sfx);
            _grill = Loop(SoundId.Grill, config.sfx);
            _musicDay = Loop(SoundId.MusicDay, config.music);
            _musicNight = Loop(SoundId.MusicNight, config.music);
            if (_musicNight != null) _musicNight.volume = 0f;
            _hornTimer = Random(config.hornInterval.x, config.hornInterval.y);

            if (config.mixer != null)
            {
                config.mixer.SetFloat(MasterParam, config.defaultMasterDb);
                config.mixer.SetFloat(MusicParam, config.defaultMusicDb);
                config.mixer.SetFloat(SfxParam, config.defaultSfxDb);
                config.mixer.SetFloat(VoiceParam, config.defaultVoiceDb);
            }
        }

        private AudioSource CreateSource(string name, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        private AudioSource Loop(SoundId id, AudioMixerGroup group)
        {
            var entry = _entries[(int)id];
            if (entry == null || entry.clips.Length == 0 || entry.clips[0] == null) return null;
            var source = CreateSource(id.ToString(), group, true);
            source.clip = entry.clips[0];
            source.volume = entry.volume;
            source.Play();
            return source;
        }

        /// <summary>Joue un son (pitch de base × variation aléatoire ± pitchVariation). Faux si limité.</summary>
        public bool Play(SoundId id, float pitch = 1f, float volumeScale = 1f)
        {
            if (_entries == null) return false;
            var entry = _entries[(int)id];
            if (entry == null || entry.clips.Length == 0) return false;
            float now = Time.unscaledTime;
            if (now - _lastPlayed[(int)id] < entry.minInterval) return false;

            int playing = 0, free = -1, oldest = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                bool busy = _voiceEnd[i] > now;
                if (busy && _voiceSound[i] == (int)id) playing++;
                if (!busy && free < 0) free = i;
                if (_voiceEnd[i] < _voiceEnd[oldest]) oldest = i;
            }
            if (playing >= entry.maxVoices) return false;
            int v = free >= 0 ? free : oldest;

            var clip = entry.clips[entry.clips.Length == 1 ? 0 : (int)(Next() % (uint)entry.clips.Length)];
            if (clip == null) return false;
            var source = _voices[v];
            source.outputAudioMixerGroup = entry.bus == AudioBus.Voice ? _config.voice : entry.bus == AudioBus.Music ? _config.music : _config.sfx;
            float variation = _config.pitchVariation;
            source.pitch = pitch * (1f + Random(-variation, variation));
            source.volume = entry.volume * volumeScale;
            source.clip = clip;
            source.Play();
            _voiceSound[v] = (int)id;
            _voiceEnd[v] = now + clip.length / Mathf.Max(0.1f, source.pitch);
            _lastPlayed[(int)id] = now;
            return true;
        }

        /// <param name="activity">Part de commerces ouverts et visibles (0..1).</param>
        /// <param name="night">0 = jour, 1 = nuit.</param>
        public void Tick(float dt, float activity, float night)
        {
            if (_config == null) return;
            float ambience = _config.ambienceByActivity.Evaluate(Mathf.Clamp01(activity));
            if (_ambience != null) _ambience.volume = Mathf.MoveTowards(_ambience.volume, ambience * Volume(SoundId.CrowdLoop), dt * 0.5f);
            if (_grill != null) _grill.volume = Mathf.MoveTowards(_grill.volume, activity * 0.6f * Volume(SoundId.Grill), dt * 0.5f);

            _nightBlend = Mathf.MoveTowards(_nightBlend, night, dt / Mathf.Max(0.1f, _config.musicCrossfadeSeconds));
            if (_musicDay != null) _musicDay.volume = (1f - _nightBlend) * Volume(SoundId.MusicDay);
            if (_musicNight != null) _musicNight.volume = _nightBlend * Volume(SoundId.MusicNight);
            else if (_musicDay != null) _musicDay.pitch = 1f - 0.04f * _nightBlend;

            _hornTimer -= dt;
            if (_hornTimer <= 0f)
            {
                Play(SoundId.Horn, 1f, 0.5f + 0.5f * activity);
                _hornTimer = Random(_config.hornInterval.x, _config.hornInterval.y);
            }

            int playing = 0;
            float now = Time.unscaledTime;
            for (int i = 0; i < _voiceEnd.Length; i++) if (_voiceEnd[i] > now) playing++;
            PlayingVoices = playing;
        }

        private float Volume(SoundId id)
        {
            var e = _entries[(int)id];
            return e != null ? e.volume : 0f;
        }

        public void SetMuted(bool muted) => AudioListener.pause = muted;

        private uint Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return _random;
        }

        private float Random(float min, float max) => min + (max - min) * ((Next() & 0xFFFFFF) / 16777215f);
    }
}
