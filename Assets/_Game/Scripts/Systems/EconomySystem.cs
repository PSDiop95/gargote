using System;
using Gargote.Core;

namespace Gargote.Systems
{
    /// <summary>Portefeuille FCFA de la ville en cours, gains cumulés et multiplicateurs globaux.</summary>
    public sealed class EconomySystem
    {
        private readonly EconomyConfig _config;

        public BigDouble Money { get; private set; }
        /// <summary>Gains depuis le dernier prestige.</summary>
        public BigDouble RunEarnings { get; private set; }
        /// <summary>Gains cumulés dans cette ville, prestiges compris (base des étoiles et de l'objectif).</summary>
        public BigDouble CityLifetimeEarnings { get; private set; }
        /// <summary>Gains cumulés toutes villes confondues (succès, partage).</summary>
        public BigDouble TotalLifetimeEarnings { get; private set; }

        /// <summary>Multiplicateur permanent (étoiles Teranga).</summary>
        public double PermanentMultiplier { get; internal set; } = 1d;
        public double AdBoostRemainingSeconds { get; private set; }

        public bool IsAdBoostActive => AdBoostRemainingSeconds > 0d;
        public double AdBoostMultiplier => IsAdBoostActive ? _config.AdBoost.Multiplier : 1d;
        public double GlobalRevenueMultiplier => PermanentMultiplier * AdBoostMultiplier;

        public event Action<BigDouble> MoneyChanged;

        public EconomySystem(EconomyConfig config)
        {
            _config = config;
        }

        public void Earn(BigDouble amount)
        {
            if (amount <= BigDouble.Zero) return;
            Money += amount;
            RunEarnings += amount;
            CityLifetimeEarnings += amount;
            TotalLifetimeEarnings += amount;
            MoneyChanged?.Invoke(Money);
        }

        public bool CanAfford(BigDouble cost) => Money >= cost;

        public bool TrySpend(BigDouble cost)
        {
            if (cost < BigDouble.Zero || Money < cost) return false;
            Money -= cost;
            if (Money < BigDouble.Zero) Money = BigDouble.Zero;
            MoneyChanged?.Invoke(Money);
            return true;
        }

        /// <summary>Le plafond de cumul permet-il un nouveau boost ?</summary>
        public bool CanAddAdBoost => AdBoostRemainingSeconds < _config.AdBoost.MaxStackSeconds - 1e-6;

        /// <summary>
        /// Ajoute un boost x2 (rewarded), cumulable jusqu'au plafond (l'excédent est perdu).
        /// <paramref name="strength"/> = valeur de la rewarded (décroissante au-delà du quota quotidien).
        /// Faux si le plafond est déjà atteint.
        /// </summary>
        public bool TryAddAdBoost(double strength = 1d)
        {
            if (!CanAddAdBoost || strength <= 0d) return false;
            AdBoostRemainingSeconds = Math.Min(_config.AdBoost.MaxStackSeconds,
                AdBoostRemainingSeconds + _config.AdBoost.DurationSeconds * strength);
            return true;
        }

        public void ConsumeBoostTime(double seconds)
        {
            if (seconds <= 0d) return;
            AdBoostRemainingSeconds = Math.Max(0d, AdBoostRemainingSeconds - seconds);
        }

        internal void ResetRun(double startingMoney)
        {
            Money = BigDouble.FromDouble(startingMoney);
            RunEarnings = BigDouble.Zero;
            MoneyChanged?.Invoke(Money);
        }

        internal void ResetCity(double startingMoney)
        {
            ResetRun(startingMoney);
            CityLifetimeEarnings = BigDouble.Zero;
        }

        internal void Restore(BigDouble money, BigDouble run, BigDouble city, BigDouble total, double boostRemaining)
        {
            Money = money;
            RunEarnings = run;
            CityLifetimeEarnings = city;
            TotalLifetimeEarnings = total;
            AdBoostRemainingSeconds = Math.Max(0d, Math.Min(boostRemaining, _config.AdBoost.MaxStackSeconds));
            MoneyChanged?.Invoke(Money);
        }
    }
}
