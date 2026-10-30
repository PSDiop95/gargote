using System;

namespace Gargote.Services.Ads
{
    public enum AdResult
    {
        /// <summary>Callback de récompense reçu : seul cas où la récompense est accordée.</summary>
        Rewarded = 0,
        Skipped = 1,
        Failed = 2,
        Unavailable = 3,
    }

    /// <summary>Pub rewarded/interstitiel. Implémentation réelle (LevelPlay) en M5 ; mock en M2.</summary>
    public interface IAdsService
    {
        bool IsRewardedReady(string placement);
        /// <summary>Affiche une rewarded ; <paramref name="onCompleted"/> est appelé une seule fois.</summary>
        void ShowRewarded(string placement, Action<AdResult> onCompleted);
    }

    /// <summary>Mock éditeur / tests : récompense immédiate (ou résultat forcé), aucune requête réseau.</summary>
    public sealed class MockAdsService : IAdsService
    {
        public bool Available = true;
        public AdResult NextResult = AdResult.Rewarded;
        public int ShownCount { get; private set; }

        public bool IsRewardedReady(string placement) => Available;

        public void ShowRewarded(string placement, Action<AdResult> onCompleted)
        {
            if (!Available)
            {
                onCompleted?.Invoke(AdResult.Unavailable);
                return;
            }
            ShownCount++;
            onCompleted?.Invoke(NextResult);
        }
    }
}
