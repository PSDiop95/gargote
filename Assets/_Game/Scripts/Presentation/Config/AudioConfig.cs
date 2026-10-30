using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    [CreateAssetMenu(menuName = "Gargote/Audio/Audio Config", fileName = "AudioConfig")]
    public sealed class AudioConfig : ScriptableObject
    {
        public AudioMixer mixer;
        public AudioMixerGroup master;
        public AudioMixerGroup music;
        public AudioMixerGroup sfx;
        public AudioMixerGroup voice;
        public SoundBank bank;
        [Tooltip("Sources de SFX préallouées (pool).")]
        public int sfxVoices = 14;
        [Range(0f, 0.2f)] public float pitchVariation = 0.05f;
        [Tooltip("Volume de l'ambiance de rue selon la part de commerces actifs (0..1).")]
        public AnimationCurve ambienceByActivity = AnimationCurve.Linear(0f, 0.15f, 1f, 0.8f);
        public float musicCrossfadeSeconds = 4f;
        public Vector2 hornInterval = new Vector2(12f, 30f);
        [Range(-80f, 20f)] public float defaultMasterDb = 0f;
        [Range(-80f, 20f)] public float defaultMusicDb = -8f;
        [Range(-80f, 20f)] public float defaultSfxDb = 0f;
        [Range(-80f, 20f)] public float defaultVoiceDb = 0f;
    }
}
