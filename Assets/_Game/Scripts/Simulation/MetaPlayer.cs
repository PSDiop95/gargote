using System;
using System.Collections.Generic;
using Gargote.Core;
using Gargote.Systems;
using Gargote.Systems.Meta;

namespace Gargote.Simulation
{
    /// <summary>Relevé quotidien de la méta (fin de journée).</summary>
    public sealed class MetaDaySample
    {
        public int Day;
        public long Cauris;
        public long CaurisEarnedToday;
        public long CaurisSpentToday;
        public int ManagersOwned;
        public int ManagersAvailable;
        /// <summary>Gérants à collectionner (hors gérants de base embauchés en FCFA) possédés / disponibles.</summary>
        public int CollectibleOwned;
        public int CollectibleAvailable;
        public int Epic;
        public int Legendary;
        public int PlayerLevel;
        public int ChestsOpened;
        public int Recipes;
        public int AchievementTiers;
        public int MetaAds;
    }

    /// <summary>
    /// Joueur simulé pour la méta (M3), branché sur la même <see cref="MetaSession"/> que le jeu :
    /// au retour, connexion, roue, coffres gratuits ; pendant la session, missions, défis, succès, boosts de gérants
    /// (joueur actif), montées de niveau des gérants, coffres Or achetés en cauris. Les rewarded méta passent après
    /// celles du cœur (x2 hors ligne, boost) dans la limite quotidienne.
    /// </summary>
    internal sealed class MetaPlayer
    {
        private static readonly DateTime Epoch = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        private readonly ManualClock _manual;
        private readonly GameSession _session;
        private double _pendingTick;
        private double _sinceDecision;
        private long _earnedAtDayStart, _spentAtDayStart;
        private int _metaAdsToday;

        public MetaSession Meta { get; }
        public GameClock Clock { get; }
        public double? FirstEpicDay { get; private set; }
        public double? FirstLegendaryDay { get; private set; }
        public readonly List<MetaDaySample> Days = new List<MetaDaySample>();
        public int MetaAdsTotal { get; private set; }

        public MetaPlayer(GameSession session, MetaDefinitions defs, int seed)
        {
            _session = session;
            _manual = new ManualClock(Epoch, 0d);
            Clock = new GameClock(_manual);
            Meta = new MetaSession(session, defs, Clock, (ulong)(uint)seed * 2654435761UL + 17UL) { SpecialsAvailable = false };
        }

        public double Day(double elapsed) => elapsed / 86400d + 1d;

        public void SetTime(double elapsed)
        {
            _manual.UtcNow = Epoch.AddSeconds(elapsed);
            _manual.MonotonicSeconds = elapsed;
        }

        /// <summary>Absence validée (gains hors ligne déjà calculés par le cœur).</summary>
        public void Away(double seconds, double elapsedNow)
        {
            SetTime(elapsedNow);
            Clock.AdvanceOffline(seconds);
        }

        /// <summary>Temps de jeu actif ; la méta avance par pas d'une seconde (coût de simulation borné).</summary>
        public void Played(double dt, double elapsed, PlayerProfile profile, Func<bool> tryAd)
        {
            SetTime(elapsed);
            Clock.AdvancePlayed(dt);
            _pendingTick += dt;
            _sinceDecision += dt;
            if (_pendingTick >= 1d)
            {
                Meta.Tick(_pendingTick);
                _pendingTick = 0d;
            }
            if (_sinceDecision >= 10d)
            {
                _sinceDecision = 0d;
                Decide(profile, tryAd, elapsed);
            }
        }

        /// <summary>Sources et puits du registre, par monnaie et raison (« cauris:mission »…).</summary>
        public void CopyLedger(List<(string, string, long, long)> into)
        {
            var rows = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
            foreach (var kv in Meta.Ledger.EarnedTotals) Row(rows, kv.Key)[0] += kv.Value;
            foreach (var kv in Meta.Ledger.SpentTotals) Row(rows, kv.Key)[1] += kv.Value;
            foreach (var kv in rows)
            {
                int sep = kv.Key.IndexOf(':');
                into.Add((sep > 0 ? kv.Key.Substring(0, sep) : "", sep > 0 ? kv.Key.Substring(sep + 1) : kv.Key, kv.Value[0], kv.Value[1]));
            }
        }

        private static long[] Row(SortedDictionary<string, long[]> rows, string key)
        {
            if (!rows.TryGetValue(key, out var r)) rows[key] = r = new long[2];
            return r;
        }

        public void StartDay(int day)
        {
            _earnedAtDayStart = Meta.Ledger.TotalEarned(Currency.Cauris);
            _spentAtDayStart = Meta.Ledger.TotalSpent(Currency.Cauris);
            _metaAdsToday = 0;
        }

        public void EndDay(int day)
        {
            int available = 0, collectible = 0, collected = 0;
            for (int i = 0; i < Meta.Managers.Count; i++)
            {
                var def = Meta.Managers.Definition(i);
                if (!IsCityOpen(def.CityId)) continue;
                available++;
                if (def.IsBase) continue;
                collectible++;
                if (Meta.Managers.Owns(i)) collected++;
            }
            Days.Add(new MetaDaySample
            {
                Day = day + 1,
                Cauris = Meta.Cauris,
                CaurisEarnedToday = Meta.Ledger.TotalEarned(Currency.Cauris) - _earnedAtDayStart,
                CaurisSpentToday = Meta.Ledger.TotalSpent(Currency.Cauris) - _spentAtDayStart,
                ManagersOwned = Meta.Managers.OwnedCount,
                ManagersAvailable = available,
                CollectibleOwned = collected,
                CollectibleAvailable = collectible,
                Epic = Meta.Managers.OwnedOfRarity(Rarity.Epic),
                Legendary = Meta.Managers.OwnedOfRarity(Rarity.Legendary),
                PlayerLevel = Meta.Level.Level,
                ChestsOpened = (int)Meta.Stats.Get(StatId.ChestsOpened),
                Recipes = Meta.Album.Count,
                AchievementTiers = Meta.Achievements.TotalClaimed,
                MetaAds = _metaAdsToday,
            });
        }

        private bool IsCityOpen(string cityId)
        {
            if (_session.City.Id == cityId) return true;
            foreach (var v in _session.VisitedCities) if (v.CityId == cityId) return true;
            return false;
        }

        private bool Ad(PlayerProfile profile, Func<bool> tryAd)
        {
            if (!profile.WatchesAds || !tryAd()) return false;
            _metaAdsToday++;
            MetaAdsTotal++;
            return true;
        }

        /// <summary>Début de session : rituels quotidiens.</summary>
        public void OnSessionStart(PlayerProfile profile, Func<bool> tryAd, double elapsed)
        {
            SetTime(elapsed);
            Meta.Tick(0d);
            Meta.ClaimLogin();
            if (Meta.CanSpinFree) Meta.SpinWheel(false);
            while (Meta.CanClaimFreeChest) Meta.ClaimFreeChest();
            if (Meta.CanClaimRewardedChest && Ad(profile, tryAd)) Meta.ClaimRewardedChest();
            while (Meta.CanSpinRewarded && Ad(profile, tryAd)) Meta.SpinWheel(true);
            Decide(profile, tryAd, elapsed);
        }

        private void Decide(PlayerProfile profile, Func<bool> tryAd, double elapsed)
        {
            var meta = Meta;
            for (int s = 0; s < meta.Missions.Slots; s++)
                if (meta.CanClaimMission(s)) meta.ClaimMission(s, Ad(profile, tryAd));
            for (int s = 0; s < meta.Challenges.Slots; s++)
                if (meta.CanClaimChallenge(s)) meta.ClaimChallenge(s);
            for (int i = 0; i < meta.Achievements.Count; i++)
                while (meta.Achievements.IsClaimable(i) && meta.ClaimAchievement(i) > 0) { }

            // Boosts actifs : le joueur actif les lance dès qu'ils sont prêts (le joueur idle n'y perd rien d'autre).
            var businesses = _session.Businesses;
            for (int b = 0; b < businesses.Count; b++)
                if (businesses[b].IsUnlocked) meta.ActivateManagerBoost(businesses[b].Definition.Id);

            // Gérants : montée de niveau si le coût FCFA reste raisonnable (≤ 20 % de la trésorerie).
            for (int i = 0; i < meta.Managers.Count; i++)
                if (meta.CanLevelUpManager(i) && meta.ManagerLevelCost(i) <= _session.Economy.Money * 0.2d) meta.LevelUpManager(i);

            // Cauris : coffre Or dès qu'il est abordable (et que la boutique est ouverte).
            if (meta.IsUnlocked(Feature.Shop))
                while (meta.Chests.CanBuy("gold")) meta.BuyChest("gold");

            while (meta.NextReveal() != null) { }
            double day = Day(elapsed);
            if (FirstEpicDay == null && meta.Managers.OwnedOfRarity(Rarity.Epic) > 0) FirstEpicDay = day;
            if (FirstLegendaryDay == null && meta.Managers.OwnedOfRarity(Rarity.Legendary) > 0) FirstLegendaryDay = day;
        }
    }
}
