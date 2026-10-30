using System;
using UnityEngine;

namespace Gargote.Presentation
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    [Serializable]
    public sealed class QualityTierSettings
    {
        public QualityTier tier;
        public bool shadows = true;
        public float shadowDistance = 45f;
        [Tooltip("1, 2 (max mobile)")] public int msaa = 2;
        [Range(0.5f, 1f)] public float renderScale = 1f;
        public int maxCustomers = 20;
        [Range(0.1f, 1f)] public float particleScale = 1f;
        public int targetFrameRate = 60;
    }

    /// <summary>Qualité automatique Bas / Moyen / Haut selon RAM et GPU.</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Quality Config", fileName = "QualityConfig")]
    public sealed class QualityConfig : ScriptableObject
    {
        [Tooltip("Pipeline URP de référence (profil mobile), cloné au lancement quelle que soit la plateforme.")]
        public UnityEngine.Rendering.RenderPipelineAsset pipeline;
        public QualityTierSettings[] tiers = Array.Empty<QualityTierSettings>();
        [Tooltip("RAM (Mo) en dessous de laquelle on passe en Bas.")]
        public int lowMemoryMb = 3000;
        [Tooltip("RAM (Mo) à partir de laquelle on passe en Haut (si le GPU suit).")]
        public int highMemoryMb = 6000;
        public int highGraphicsMemoryMb = 2048;
        [Tooltip("Forcer un palier (-1 = automatique).")]
        public int forceTier = -1;

        public QualityTierSettings Get(QualityTier tier)
        {
            foreach (var t in tiers) if (t.tier == tier) return t;
            return tiers.Length > 0 ? tiers[0] : new QualityTierSettings();
        }

        public QualityTier Detect(int systemMemoryMb, int graphicsMemoryMb)
        {
            if (forceTier >= 0) return (QualityTier)forceTier;
            if (systemMemoryMb < lowMemoryMb) return QualityTier.Low;
            if (systemMemoryMb >= highMemoryMb && graphicsMemoryMb >= highGraphicsMemoryMb) return QualityTier.High;
            return QualityTier.Medium;
        }
    }
}
