using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    public readonly struct ChestCard
    {
        public readonly int ManagerIndex;
        public readonly Rarity Rarity;
        /// <summary>Première carte de ce gérant : il rejoint la collection.</summary>
        public readonly bool IsNew;
        /// <summary>Gérant déjà au niveau max : la carte a été convertie en cauris.</summary>
        public readonly long ConvertedCauris;

        public ChestCard(int managerIndex, Rarity rarity, bool isNew, long convertedCauris)
        {
            ManagerIndex = managerIndex;
            Rarity = rarity;
            IsNew = isNew;
            ConvertedCauris = convertedCauris;
        }
    }

    public sealed class ChestResult
    {
        public string ChestId;
        public readonly List<ChestCard> Cards = new List<ChestCard>(16);
        public long Cauris;
        public bool Boost;
        /// <summary>La pitié a forcé une carte (rareté garantie).</summary>
        public bool PityTriggered;

        public Rarity BestRarity
        {
            get
            {
                var best = Rarity.Common;
                foreach (var c in Cards) if (c.Rarity > best) best = c.Rarity;
                return best;
            }
        }
    }

    /// <summary>
    /// Coffres éthiques (§7, conformes aux stores) : probabilités publiées, tirage déterministe et seedé (état sauvegardé,
    /// insensible à l'heure), pitié (rareté garantie au plus tard tous les N coffres, compteur visible), doublons au
    /// niveau max convertis en cauris. Coffre gratuit toutes les 4 h (2 en réserve), compté sur le temps de confiance.
    /// </summary>
    public sealed class ChestSystem
    {
        private readonly MetaDefinitions _defs;
        private readonly Ledger _ledger;
        private readonly ManagerCollection _managers;
        private readonly DeterministicRandom _random;
        private readonly Func<string, bool> _cityUnlocked;
        private readonly Dictionary<string, int> _sincePity = new Dictionary<string, int>();
        private readonly List<int> _candidates = new List<int>(32);

        public int FreeStored { get; private set; }
        /// <summary>Secondes accumulées vers le prochain coffre gratuit.</summary>
        public double FreeProgress { get; private set; }
        public int RewardedDayKey { get; private set; } = int.MinValue;
        public int RewardedToday { get; private set; }
        public long Opened { get; private set; }
        public bool FirstFreeClaimed { get; private set; }

        public event Action<ChestResult> ChestOpened;
        public event Action FreeChestReady;

        /// <param name="cityUnlocked">Vrai si les gérants de cette ville peuvent sortir des coffres (villes visitées + en cours).</param>
        public ChestSystem(MetaDefinitions defs, Ledger ledger, ManagerCollection managers, DeterministicRandom random,
            Func<string, bool> cityUnlocked)
        {
            _defs = defs;
            _ledger = ledger;
            _managers = managers;
            _random = random;
            _cityUnlocked = cityUnlocked ?? (_ => true);
            FreeStored = 1; // un premier coffre gratuit attend le joueur quand la fonctionnalité se débloque
        }

        public DeterministicRandom Random => _random;
        public ChestRules Rules => _defs.ChestRules;

        /// <summary>Coffres sans la rareté garantie depuis la dernière (pour « Épique garanti dans N coffres »).</summary>
        public int SincePity(string chestId)
        {
            if (_sincePity.TryGetValue(chestId, out int n)) return n;
            var def = _defs.Chest(chestId);
            return def != null && def.FirstGuaranteed && def.PityEvery > 0 ? def.PityEvery - 1 : 0;
        }

        /// <summary>Coffres restants avant la garantie (inclus), ou −1 si ce coffre n'a pas de pitié.</summary>
        public int ChestsUntilGuaranteed(string chestId)
        {
            var def = _defs.Chest(chestId);
            if (def == null || def.PityEvery <= 0) return -1;
            return Math.Max(1, def.PityEvery - SincePity(chestId));
        }

        public double SecondsToNextFree => FreeStored >= Rules.FreeMaxStored ? 0d : Math.Max(0d, Rules.FreeEverySeconds - FreeProgress);

        /// <summary>Temps de confiance écoulé (jeu et absence validée).</summary>
        public void Tick(double trustedSeconds)
        {
            if (trustedSeconds <= 0d || FreeStored >= Rules.FreeMaxStored) return;
            FreeProgress += trustedSeconds;
            bool ready = false;
            while (FreeProgress >= Rules.FreeEverySeconds && FreeStored < Rules.FreeMaxStored)
            {
                FreeProgress -= Rules.FreeEverySeconds;
                FreeStored++;
                ready = true;
            }
            if (FreeStored >= Rules.FreeMaxStored) FreeProgress = 0d;
            if (ready) FreeChestReady?.Invoke();
        }

        public ChestResult ClaimFree()
        {
            if (FreeStored <= 0) return null;
            FreeStored--;
            FirstFreeClaimed = true;
            return Open(Rules.FreeChestId, "chest_free");
        }

        public bool CanClaimRewarded(int dayKey) => (dayKey != RewardedDayKey ? 0 : RewardedToday) < Rules.RewardedPerDay;

        /// <summary>Coffre rewarded : à appeler sur le callback de récompense uniquement.</summary>
        public ChestResult ClaimRewarded(int dayKey)
        {
            if (!CanClaimRewarded(dayKey)) return null;
            if (dayKey != RewardedDayKey) { RewardedDayKey = dayKey; RewardedToday = 0; }
            RewardedToday++;
            return Open(Rules.RewardedChestId, "chest_rewarded");
        }

        public bool CanBuy(string chestId)
        {
            var def = _defs.Chest(chestId);
            return def != null && def.CostCauris > 0 && _ledger.CanAfford(Currency.Cauris, def.CostCauris);
        }

        public ChestResult Buy(string chestId)
        {
            var def = _defs.Chest(chestId);
            if (def == null || def.CostCauris <= 0 || !_ledger.TryDebit(Currency.Cauris, def.CostCauris, "chest_" + chestId)) return null;
            return Open(chestId, "chest_shop");
        }

        /// <summary>Ouvre un coffre gagné (mission, connexion, succès, roue, niveau). <paramref name="reason"/> = source du registre.</summary>
        public ChestResult Open(string chestId, string reason)
        {
            var def = _defs.Chest(chestId);
            if (def == null) return null;
            var result = new ChestResult { ChestId = chestId };
            bool pity = def.PityEvery > 0;
            int since = SincePity(chestId);
            bool gotPity = false;

            for (int c = 0; c < def.Cards; c++)
            {
                var rarity = RollRarity(def);
                // Dernière carte du coffre qui atteint la limite de pitié : rareté garantie.
                if (pity && !gotPity && c == def.Cards - 1 && since + 1 >= def.PityEvery && rarity < def.PityRarity)
                {
                    rarity = def.PityRarity;
                    result.PityTriggered = true;
                }
                int manager = PickManager(ref rarity);
                if (manager < 0) continue;
                if (rarity >= def.PityRarity) gotPity = true;
                bool isNew = !_managers.Owns(manager);
                long converted = _managers.AddCards(manager, 1, reason);
                result.Cards.Add(new ChestCard(manager, rarity, isNew, converted));
                result.Cauris += converted;
            }
            if (pity) _sincePity[chestId] = gotPity ? 0 : since + 1;

            long cauris = _random.Range(def.CaurisMin, def.CaurisMax);
            _ledger.Credit(Currency.Cauris, cauris, reason);
            result.Cauris += cauris;
            result.Boost = def.BoostChance > 0d && _random.NextDouble() < def.BoostChance;
            Opened++;
            ChestOpened?.Invoke(result);
            return result;
        }

        private Rarity RollRarity(ChestDefinition def)
        {
            int r = _random.Weighted(def.RarityWeights);
            return r < 0 ? Rarity.Common : (Rarity)r;
        }

        /// <summary>
        /// Gérant d'une rareté parmi les villes débloquées, en évitant les gérants au niveau max (pas de doublon inutile).
        /// Si aucune carte de cette rareté n'existe encore, on descend d'une rareté.
        /// </summary>
        private int PickManager(ref Rarity rarity)
        {
            for (int r = (int)rarity; r >= 0; r--)
            {
                Collect((Rarity)r, true);
                if (_candidates.Count == 0) Collect((Rarity)r, false);
                if (_candidates.Count == 0) continue;
                rarity = (Rarity)r;
                return _candidates[_random.Next(_candidates.Count)];
            }
            return -1;
        }

        private void Collect(Rarity rarity, bool skipMaxed)
        {
            _candidates.Clear();
            for (int i = 0; i < _managers.Count; i++)
            {
                var d = _managers.Definition(i);
                if (d.Rarity != rarity || !_cityUnlocked(d.CityId)) continue;
                if (skipMaxed && _managers.IsMaxed(i)) continue;
                _candidates.Add(i);
            }
        }

        /// <summary>Probabilité affichée de chaque rareté, pour une carte (bouton « i »).</summary>
        public double[] RarityProbabilities(string chestId)
        {
            var def = _defs.Chest(chestId);
            var p = new double[4];
            if (def == null) return p;
            double total = 0d;
            foreach (double w in def.RarityWeights) total += Math.Max(0d, w);
            for (int i = 0; i < 4; i++) p[i] = total > 0d ? Math.Max(0d, def.RarityWeights[i]) / total : 0d;
            return p;
        }

        /// <summary>Probabilité d'au moins une carte de rareté ≥ <paramref name="rarity"/> dans un coffre (hors pitié).</summary>
        public double ChanceAtLeastOne(string chestId, Rarity rarity)
        {
            var def = _defs.Chest(chestId);
            if (def == null) return 0d;
            var p = RarityProbabilities(chestId);
            double atLeast = 0d;
            for (int i = (int)rarity; i < 4; i++) atLeast += p[i];
            return 1d - Math.Pow(1d - atLeast, def.Cards);
        }

        internal ChestsSaveData Save()
        {
            var data = new ChestsSaveData
            {
                FreeStored = FreeStored, FreeProgress = FreeProgress, RewardedDayKey = RewardedDayKey,
                RewardedToday = RewardedToday, Opened = Opened, FirstFreeClaimed = FirstFreeClaimed,
                RandomState = _random.State.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PityIds = new string[_sincePity.Count], PityCounts = new int[_sincePity.Count],
            };
            int k = 0;
            foreach (var kv in _sincePity) { data.PityIds[k] = kv.Key; data.PityCounts[k] = kv.Value; k++; }
            return data;
        }

        internal void Restore(ChestsSaveData data)
        {
            _sincePity.Clear();
            if (data == null) return;
            FreeStored = Math.Max(0, Math.Min(Rules.FreeMaxStored, data.FreeStored));
            FreeProgress = Math.Max(0d, Math.Min(Rules.FreeEverySeconds, data.FreeProgress));
            RewardedDayKey = data.RewardedDayKey;
            RewardedToday = Math.Max(0, data.RewardedToday);
            Opened = Math.Max(0L, data.Opened);
            FirstFreeClaimed = data.FirstFreeClaimed;
            if (ulong.TryParse(data.RandomState, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong state))
                _random.State = state;
            if (data.PityIds != null && data.PityCounts != null)
                for (int i = 0; i < Math.Min(data.PityIds.Length, data.PityCounts.Length); i++)
                    if (!string.IsNullOrEmpty(data.PityIds[i])) _sincePity[data.PityIds[i]] = Math.Max(0, data.PityCounts[i]);
        }
    }
}
