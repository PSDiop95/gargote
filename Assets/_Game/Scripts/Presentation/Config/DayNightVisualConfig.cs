using System;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    /// <summary>Rendu du cycle jour/nuit (le rythme économique reste dans EconomyConfig.DayNight).</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Day Night Visual Config", fileName = "DayNightVisualConfig")]
    public sealed class DayNightVisualConfig : ScriptableObject
    {
        [Tooltip("Couleur du soleil sur 24 h (0 = minuit, 0,5 = midi).")]
        public Gradient sunColor = new Gradient();
        public AnimationCurve sunIntensity = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 0.15f);
        [Tooltip("Hauteur du soleil (degrés) sur 24 h.")]
        public AnimationCurve sunPitch = AnimationCurve.Linear(0f, 20f, 1f, 20f);
        public float sunYaw = -35f;
        public Gradient ambient = new Gradient();
        public Gradient sky = new Gradient();
        [Tooltip("Intensité des lampes émissives (0 jour, 1 nuit) sur 24 h.")]
        public AnimationCurve lampIntensity = AnimationCurve.Constant(0f, 1f, 0f);
        public float lampEmissionBoost = 2.2f;
    }
}
