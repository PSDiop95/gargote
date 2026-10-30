using System;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    /// <summary>Foule : budgets, mouvement procédural, tenues.</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Crowd Config", fileName = "CrowdConfig")]
    public sealed class CrowdConfig : ScriptableObject
    {
        [Header("Densité (voir CrowdDensity)")]
        public int maxTotal = 20;
        public int maxPerBusiness = 6;
        public int minPerUnlocked = 1;
        public double customersPerDecade = 1.25;
        [Tooltip("Service affiché minimal ; plus rapide = mode rush.")]
        public double minVisualServiceSeconds = 0.6;
        [Tooltip("Délai minimal entre deux arrivées pour un même commerce.")]
        public float spawnInterval = 0.8f;
        [Tooltip("Marge hors écran (unités monde) pour faire entrer et sortir les clients.")]
        public float offscreenMargin = 3f;

        [Header("Mouvement")]
        public float walkSpeed = 2.4f;
        public float rushWalkSpeed = 3.6f;
        public float bobHeight = 0.08f;
        public float bobFrequency = 7f;
        public float idleSwayDegrees = 6f;
        public float idleSwayFrequency = 1.6f;
        public float joyJumpHeight = 0.35f;
        public float joyJumpDuration = 0.35f;
        public float personScale = 0.9f;

        [Header("Tenues (placeholders wax)")]
        public Color[] outfits =
        {
            new Color(0.95f, 0.45f, 0.10f), new Color(0.10f, 0.55f, 0.75f), new Color(0.90f, 0.20f, 0.35f),
            new Color(0.20f, 0.65f, 0.30f), new Color(0.95f, 0.80f, 0.15f), new Color(0.55f, 0.30f, 0.70f),
        };
        public Color[] skinTones =
        {
            new Color(0.36f, 0.22f, 0.14f), new Color(0.45f, 0.29f, 0.18f), new Color(0.28f, 0.17f, 0.11f),
        };
        public Color employeeOutfit = new Color(0.95f, 0.95f, 0.92f);
        public Color managerOutfit = new Color(0.12f, 0.12f, 0.16f);
        public Color managerHat = new Color(0.85f, 0.15f, 0.15f);

        [Header("Employés et gérants")]
        public float gestureMinSeconds = 0.3f;
        public float gestureMaxSeconds = 2.5f;

        public CrowdRules ToRules() => new CrowdRules
        {
            MaxTotal = maxTotal, MaxPerBusiness = maxPerBusiness, MinPerUnlocked = minPerUnlocked,
            CustomersPerDecade = customersPerDecade, MinVisualServiceSeconds = minVisualServiceSeconds,
        };
    }
}
