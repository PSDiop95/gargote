using System;
using Gargote.Core;

namespace Gargote.Systems
{
    /// <summary>
    /// Étoiles Teranga : bonus permanent, réclamées en relançant la ville ou en changeant de ville.
    /// Les étoiles sont rattachées à la ville où elles ont été gagnées ; celles des villes précédentes
    /// ne comptent qu'en partie (<see cref="PrestigeConfig.CityCarryOver"/>). Un plancher protège le
    /// multiplicateur à l'arrivée dans une nouvelle ville. Les étoiles peuvent être dépensées en upgrades de confort.
    /// </summary>
    public sealed class PrestigeSystem
    {
        private readonly PrestigeConfig _config;
        private readonly bool[] _owned;

        public long TotalStars { get; private set; }
        /// <summary>Étoiles gagnées dans la ville en cours.</summary>
        public long CurrentCityStars { get; private set; }
        public long PreviousCitiesStars => TotalStars - CurrentCityStars;
        /// <summary>Étoiles dépensées en upgrades (elles ne comptent plus dans le multiplicateur).</summary>
        public long StarsSpent { get; private set; }
        public long AvailableStars => Math.Max(0L, TotalStars - StarsSpent);
        /// <summary>Étoiles déjà réclamées à partir des gains de la ville en cours.</summary>
        public long ClaimedForCurrentCity { get; private set; }
        public int PrestigeCount { get; private set; }
        /// <summary>Seuil d'étoiles de la ville en cours (fixé par <see cref="GameSession"/>).</summary>
        public double CityThreshold { get; internal set; }
        /// <summary>Plancher du multiplicateur dans la ville en cours (fixé au voyage).</summary>
        public double MultiplierFloor { get; private set; } = 1d;

        public event Action<int> StarUpgradeBought;

        public PrestigeSystem(PrestigeConfig config)
        {
            _config = config;
            _owned = new bool[config.StarUpgrades.Length];
            CityThreshold = config.Threshold;
        }

        public double EffectiveStars =>
            PrestigeCalculator.EffectiveStars(CurrentCityStars, PreviousCitiesStars, StarsSpent, _config);

        /// <summary>Multiplicateur sans plancher (étoiles seules).</summary>
        public double StarOnlyMultiplier => 1d + EffectiveStars * _config.BonusPerStar;

        public double RevenueMultiplier => Math.Max(StarOnlyMultiplier, MultiplierFloor);

        public long StarsToGain(BigDouble cityLifetimeEarnings) =>
            PrestigeCalculator.StarsToGain(cityLifetimeEarnings, ClaimedForCurrentCity, _config, CityThreshold);

        public bool CanPrestige(BigDouble cityLifetimeEarnings) =>
            StarsToGain(cityLifetimeEarnings) >= Math.Max(1L, _config.MinStarsToPrestige);

        /// <summary>Multiplicateur après une relance de la ville, pour l'écran « +68 % sur tout ».</summary>
        public double MultiplierAfter(BigDouble cityLifetimeEarnings) =>
            Math.Max(MultiplierFloor, 1d + PrestigeCalculator.EffectiveStars(CurrentCityStars + StarsToGain(cityLifetimeEarnings),
                PreviousCitiesStars, StarsSpent, _config) * _config.BonusPerStar);

        // --- Upgrades d'étoiles ---

        public int UpgradeCount => _owned.Length;
        public StarUpgradeDefinition Upgrade(int index) => _config.StarUpgrades[index];
        public bool Owns(int index) => _owned[index];

        public bool Has(StarUpgradeEffect effect)
        {
            for (int i = 0; i < _owned.Length; i++)
                if (_owned[i] && _config.StarUpgrades[i].Effect == effect) return true;
            return false;
        }

        public bool CanBuyUpgrade(int index) => !_owned[index] && AvailableStars >= _config.StarUpgrades[index].CostStars;

        public bool TryBuyUpgrade(int index)
        {
            if (!CanBuyUpgrade(index)) return false;
            StarsSpent += _config.StarUpgrades[index].CostStars;
            _owned[index] = true;
            StarUpgradeBought?.Invoke(index);
            return true;
        }

        internal long Claim(BigDouble cityLifetimeEarnings, bool movingToNewCity)
        {
            long gained = StarsToGain(cityLifetimeEarnings);
            double before = RevenueMultiplier;
            TotalStars += gained;
            PrestigeCount++;
            if (movingToNewCity)
            {
                // Les étoiles de la ville quittée (dernière réclamation comprise) passent dans la reprise partielle,
                // mais le multiplicateur ne tombe pas sous le plancher calculé juste avant le voyage.
                CurrentCityStars = 0;
                ClaimedForCurrentCity = 0;
                MultiplierFloor = Math.Max(1d, before * _config.TravelMultiplierFloorRatio);
            }
            else
            {
                CurrentCityStars += gained;
                ClaimedForCurrentCity += gained;
            }
            return gained;
        }

        internal void Restore(long totalStars, long currentCityStars, long claimedForCity, int prestigeCount,
            long starsSpent, string[] ownedUpgrades, double multiplierFloor)
        {
            TotalStars = Math.Max(0L, totalStars);
            CurrentCityStars = Math.Max(0L, Math.Min(currentCityStars, TotalStars));
            ClaimedForCurrentCity = Math.Max(0L, claimedForCity);
            PrestigeCount = Math.Max(0, prestigeCount);
            StarsSpent = Math.Max(0L, Math.Min(starsSpent, TotalStars));
            MultiplierFloor = Math.Max(1d, multiplierFloor);
            for (int i = 0; i < _owned.Length; i++)
                _owned[i] = ownedUpgrades != null && Array.IndexOf(ownedUpgrades, _config.StarUpgrades[i].Id) >= 0;
        }

        internal string[] OwnedUpgradeIds()
        {
            int n = 0;
            for (int i = 0; i < _owned.Length; i++) if (_owned[i]) n++;
            var ids = new string[n];
            for (int i = 0, k = 0; i < _owned.Length; i++) if (_owned[i]) ids[k++] = _config.StarUpgrades[i].Id;
            return ids;
        }
    }
}
