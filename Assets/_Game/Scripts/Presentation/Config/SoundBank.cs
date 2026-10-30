using System;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    public enum SoundId
    {
        Tap = 0, Bill = 1, Cash = 2, Buy = 3, Milestone = 4, Unlock = 5, Shutter = 6, Grill = 7, Fry = 8,
        CrowdLoop = 9, Horn = 10, Collect = 11, Special = 12, Manager = 13, MusicDay = 14, MusicNight = 15, Button = 16,
    }

    public enum AudioBus { Sfx = 0, Music = 1, Voice = 2, Ambience = 3 }

    [Serializable]
    public sealed class SoundEntry
    {
        public SoundId id;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        public AudioBus bus = AudioBus.Sfx;
        [Tooltip("Intervalle minimal entre deux lectures de ce son.")]
        public float minInterval = 0.03f;
        [Tooltip("Lectures simultanées maximales de ce son.")]
        public int maxVoices = 3;
    }

    /// <summary>Correspondance unique son → fichiers : remplacer ici les placeholders en M4.</summary>
    [CreateAssetMenu(menuName = "Gargote/Audio/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        public SoundEntry[] entries = Array.Empty<SoundEntry>();

        public SoundEntry Find(SoundId id)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].id == id) return entries[i];
            return null;
        }
    }
}
