using System;
using System.Text;
using Gargote.Core;
using Gargote.Services.Ads;
using Gargote.Systems;
using Gargote.Systems.Meta;

namespace Gargote.Presentation
{
    /// <summary>Dépendances partagées par les écrans méta (aucun singleton : passé par le composition root).</summary>
    public sealed class MetaUiContext
    {
        public MetaSession Meta;
        public GameSession Session => Meta.Session;
        public HudConfig Hud;
        public StreetAssets Assets;
        public IAdsService Ads;
        public AudioDirector Audio;
        public Func<string, string> BusinessName;
        /// <summary>Message court dans la bande des toasts.</summary>
        public Action<string> Toast;
        /// <summary>Demande système d'autorisation des notifications (callback : accordée ?).</summary>
        public Action<Action<bool>> RequestNotificationPermission;

        /// <summary>
        /// Rewarded MOCK (M5 : LevelPlay). La récompense n'est donnée que sur le callback de récompense.
        /// </summary>
        public void Rewarded(string placement, Action onRewarded)
        {
            if (Ads == null || !Ads.IsRewardedReady(placement)) return;
            Ads.ShowRewarded(placement, result =>
            {
                if (result != AdResult.Rewarded) return;
                Meta.OnRewardedWatched();
                onRewarded?.Invoke();
            });
        }

        public bool AdReady(string placement) => Ads != null && Ads.IsRewardedReady(placement);
    }

    /// <summary>Textes composés des écrans méta (à l'ouverture uniquement).</summary>
    public static class MetaText
    {
        private static readonly StringBuilder Sb = new StringBuilder(128);

        public static string ManagerName(ManagerDefinition m) => Loc.Get("manager." + m.Id + ".name");
        public static string ChestName(string id) => Loc.Get("chest." + id);
        public static string CityName(string id) => Loc.Get("city." + id);

        public static string Reward(RewardBundle r, double multiplier = 1d)
        {
            if (r == null) return string.Empty;
            Sb.Clear();
            if (r.Cauris > 0) Append(new LocTemplate("reward.cauris").Format(Math.Round(r.Cauris * multiplier)));
            if (r.Tokens > 0) Append(new LocTemplate("reward.tokens").Format(Math.Round(r.Tokens * multiplier)));
            if (!string.IsNullOrEmpty(r.ChestId)) Append(ChestName(r.ChestId));
            if (r.Boost) Append(Loc.Get("reward.boost"));
            if (r.Cards > 0) Append(new LocTemplate("reward.cards").Format(r.Cards));
            return Sb.ToString();
        }

        public static string MissionReward(MissionInstance m)
        {
            Sb.Clear();
            if (m.Cauris > 0) Append(new LocTemplate("reward.cauris").Format(m.Cauris));
            if (m.Tokens > 0) Append(new LocTemplate("reward.tokens").Format(m.Tokens));
            if (!string.IsNullOrEmpty(m.ChestId)) Append(ChestName(m.ChestId));
            if (m.Xp > 0) Append(new LocTemplate("reward.xp").Format(m.Xp));
            return Sb.ToString();
        }

        private static void Append(string part)
        {
            if (Sb.Length > 0) Sb.Append("  ·  ");
            Sb.Append(part);
        }

        public static string Mission(MissionInstance m, Func<string, string> businessName)
        {
            string key = "mission." + Snake(m.Kind.ToString());
            var t = new LocTemplate(key);
            string business = string.IsNullOrEmpty(m.BusinessId) ? string.Empty : businessName(m.BusinessId);
            switch (m.Kind)
            {
                case MissionKind.ReachLevel: return t.Format(business, m.Target);
                case MissionKind.HireManager: return t.Format(business);
                case MissionKind.EarnSecondsOfIncome: return t.Format(NumberFormatter.Format(BigDouble.FromDouble(m.Target)));
                default: return t.Format(NumberFormatter.Format(BigDouble.FromDouble(m.Target)));
            }
        }

        public static string Achievement(AchievementDefinition a, int tier, Func<string, string> businessName)
        {
            var t = new LocTemplate("stat." + Snake(a.Stat.ToString()));
            double target = a.Tiers[Math.Min(tier, a.Tiers.Length - 1)];
            string business = string.IsNullOrEmpty(a.BusinessId) ? string.Empty : businessName(a.BusinessId);
            return t.Format(NumberFormatter.Format(BigDouble.FromDouble(target)), business);
        }

        /// <summary>« ReachLevel » → « reach_level » (clés de localisation).</summary>
        public static string Snake(string pascal)
        {
            var sb = new StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static string StarEffect(StarUpgradeDefinition u)
        {
            switch (u.Effect)
            {
                case StarUpgradeEffect.KeepManagers: return Loc.Get("star_effect.keep_managers");
                case StarUpgradeEffect.AutoBuyFirstMilestone: return Loc.Get("star_effect.auto_buy");
                case StarUpgradeEffect.RebuyPrevious: return Loc.Get("star_effect.rebuy");
                case StarUpgradeEffect.OfflineEarnings: return new LocTemplate("star_effect.offline_earnings").Format(MetaFormat.Percent(u.Value));
                case StarUpgradeEffect.OfflineCap: return new LocTemplate("star_effect.offline_cap").Format(MetaFormat.Duration(u.Value));
                case StarUpgradeEffect.GlobalSpeed: return new LocTemplate("star_effect.global_speed").Format(u.Value.ToString("0.##"));
                case StarUpgradeEffect.CostReduction: return new LocTemplate("star_effect.cost_reduction").Format(MetaFormat.Percent(1d - u.Value));
                default: return string.Empty;
            }
        }
    }
}
