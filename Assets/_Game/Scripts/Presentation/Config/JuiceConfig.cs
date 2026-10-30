using System;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    /// <summary>Juice : billets, popups, confettis, durées et intensités. Option « effets réduits » incluse.</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Juice Config", fileName = "JuiceConfig")]
    public sealed class JuiceConfig : ScriptableObject
    {
        [Header("Billets")]
        public int minBills = 2;
        public int maxBills = 5;
        public double minBurstIntervalSeconds = 0.35;
        public double minGlobalBurstIntervalSeconds = 0.08;
        public float billFlightDuration = 0.65f;
        public float billArcHeight = 180f;
        public float billStagger = 0.05f;
        public float billSize = 46f;
        public Color billColor = new Color(0.45f, 0.75f, 0.45f);
        public Color bigBillColor = new Color(0.95f, 0.80f, 0.25f);

        [Header("Popups « +1,2K »")]
        public float popupDuration = 0.9f;
        public float popupRise = 90f;
        public float popupFontSize = 44f;

        [Header("Pile de billets (cosmétique)")]
        public int pileMaxBills = 12;
        public float pileAutoCollectSeconds = 3f;

        [Header("Compteur")]
        public float counterRollSeconds = 0.35f;
        public float counterPulseScale = 1.12f;

        [Header("Squash & stretch")]
        public float tapPunch = 0.18f;
        public float buyPunch = 0.12f;
        public float punchDuration = 0.25f;
        public float buttonPunch = 0.1f;

        [Header("Palier / déblocage / gérant")]
        public int confettiCount = 60;
        public int dustCount = 18;
        public int starCount = 14;
        public int sparkCount = 6;
        public float shakeStrength = 0.15f;
        public float shakeDuration = 0.25f;
        public float bannerDuration = 1.2f;
        public float shutterOpenDuration = 0.8f;
        public float handHintDelay = 3f;
        public Vector2 managerLineInterval = new Vector2(20f, 60f);
        public float bubbleDuration = 3.5f;

        [Header("Son d'achat")]
        public float buySoundMinInterval = 0.05f;
        public float buyPitchStep = 0.04f;
        public float buyPitchMax = 1.5f;
        public float buyComboWindow = 0.6f;

        [Header("Effets réduits")]
        public bool reducedEffects;
        [Tooltip("Tweens préalloués (PrimeTween) : au-delà, le pool grandit et alloue.")]
        public int tweenCapacity = 400;
        [Tooltip("Particules divisées par ce facteur en effets réduits.")]
        public int reducedParticleDivider = 3;

        [Header("Vibration")]
        public bool haptics = true;

        public MoneyFlowRules ToMoneyRules() => new MoneyFlowRules
        {
            MinBills = minBills, MaxBills = maxBills,
            MinBurstIntervalSeconds = reducedEffects ? minBurstIntervalSeconds * 2 : minBurstIntervalSeconds,
            MinGlobalBurstIntervalSeconds = minGlobalBurstIntervalSeconds,
        };

        public int Particles(int count) => reducedEffects ? Mathf.Max(1, count / Mathf.Max(1, reducedParticleDivider)) : count;
    }
}
