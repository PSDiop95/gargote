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
        /// <summary>Bonus de bienvenue restant dans la ville en cours (secondes).</summary>
        public double WelcomeRemainingSeconds { get; private set; }
        public double WelcomeMultiplier => WelcomeRemainingSeconds > 0d ? _config.Welcome.Multiplier : 1d;
        /// <summary>Multiplicateur permanent de la méta (gérants légendaires, album complet), conservé partout.</summary>
        public double MetaMultiplier { get; internal set; } = 1d;
        /// <summary>Boost x2 temporaire gagné en jeu (coffres, roue), distinct du boost rewarded.</summary>
        public double TimedBoostRemainingSeconds { get; private set; }
        public double TimedBoostMultiplier => TimedBoostRemainingSeconds > 0d ? _timedBoostMultiplier : 1d;
        private double _timedBoostMultiplier = 1d;
        /// <summary>Boosts temporaires seuls (pub, bienvenue, boost gagné) : à retirer pour un revenu « de fond ».</summary>
        public double TemporaryMultiplier => AdBoostMultiplier * WelcomeMultiplier * TimedBoostMultiplier;
        public double GlobalRevenueMultiplier => PermanentMultiplier * MetaMultiplier * TemporaryMultiplier;

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
            WelcomeRemainingSeconds = Math.Max(0d, WelcomeRemainingSeconds - seconds);
            TimedBoostRemainingSeconds = Math.Max(0d, TimedBoostRemainingSeconds - seconds);
        }

        /// <summary>Ajoute un boost temporaire gagné (la force ne se cumule pas ; la durée s'additionne jusqu'au plafond).</summary>
        internal void AddTimedBoost(double multiplier, double seconds, double maxStackSeconds)
        {
            if (multiplier <= 1d || seconds <= 0d) return;
            _timedBoostMultiplier = multiplier;
            TimedBoostRemainingSeconds = Math.Min(Math.Max(seconds, maxStackSeconds), TimedBoostRemainingSeconds + seconds);
        }

        internal void RestoreTimedBoost(double multiplier, double remaining)
        {
            _timedBoostMultiplier = multiplier > 1d ? multiplier : 1d;
            TimedBoostRemainingSeconds = Math.Max(0d, remaining);
        }

        internal void StartWelcomeBonus() => WelcomeRemainingSeconds = Math.Max(0d, _config.Welcome.DurationSeconds);

        internal void ResetRun(double startingMoney) => ResetRun(BigDouble.FromDouble(startingMoney));

        internal void ResetRun(BigDouble startingMoney)
        {
            Money = startingMoney;
            RunEarnings = BigDouble.Zero;
            MoneyChanged?.Invoke(Money);
        }

        internal void ResetCity(double startingMoney)
        {
            ResetRun(startingMoney);
            CityLifetimeEarnings = BigDouble.Zero;
        }

        internal void Restore(BigDouble money, BigDouble run, BigDouble city, BigDouble total, double boostRemaining,
            double welcomeRemaining)
        {
            WelcomeRemainingSeconds = Math.Max(0d, Math.Min(welcomeRemaining, _config.Welcome.DurationSeconds));
            Money = money;
            RunEarnings = run;
            CityLifetimeEarnings = city;
            TotalLifetimeEarnings = total;
            AdBoostRemainingSeconds = Math.Max(0d, Math.Min(boostRemaining, _config.AdBoost.MaxStackSeconds));
            MoneyChanged?.Invoke(Money);
        }
    }
}
