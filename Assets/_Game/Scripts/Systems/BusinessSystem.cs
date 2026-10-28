using System;
using Gargote.Core;

namespace Gargote.Systems
{
    public enum BuyMode
    {
        One = 0,
        Ten = 1,
        Hundred = 2,
        Max = 3,
        NextMilestone = 4,
    }

    public readonly struct BuyQuote
    {
        public readonly long Count;
        public readonly BigDouble Cost;
        public readonly bool Affordable;

        public BuyQuote(long count, BigDouble cost, bool affordable)
        {
            Count = count;
            Cost = cost;
            Affordable = affordable;
        }
    }

    /// <summary>Commerces d'une ville : cycles, taps, achats de niveaux, gérants, paliers.</summary>
    public sealed class BusinessSystem
    {
        private readonly BusinessState[] _states;
        private readonly EconomySystem _economy;
        private readonly EconomyConfig _config;

        public event Action<int> Unlocked;
        public event Action<int, long> LevelChanged;
        /// <summary>(index du commerce, nombre de paliers atteints).</summary>
        public event Action<int, int> MilestoneReached;
        public event Action<int> ManagerHired;
        public event Action<int, BigDouble> CycleCompleted;
        /// <summary>Nombre de paliers globaux atteints (tous les commerces au niveau requis).</summary>
        public event Action<int> GlobalMilestoneReached;

        private double _globalMultiplier = 1d;

        public int GlobalMilestonesReached { get; private set; }
        /// <summary>Multiplicateur de revenus des paliers globaux (appliqué à tous les commerces).</summary>
        public double GlobalMilestoneMultiplier => _globalMultiplier;

        public BusinessSystem(CityDefinition city, EconomyConfig config, EconomySystem economy)
        {
            _config = config;
            _economy = economy;
            _states = new BusinessState[city.Businesses.Length];
            for (int i = 0; i < _states.Length; i++)
                _states[i] = new BusinessState(city.Businesses[i], i, config);
        }

        public int Count => _states.Length;
        public BusinessState this[int index] => _states[index];

        public int IndexOf(string businessId)
        {
            for (int i = 0; i < _states.Length; i++)
                if (_states[i].Definition.Id == businessId) return i;
            return -1;
        }

        /// <summary>Fait avancer les cycles. Zéro allocation.</summary>
        public void Tick(double deltaSeconds, double hourOfDay)
        {
            if (deltaSeconds <= 0d) return;
            for (int i = 0; i < _states.Length; i++)
            {
                var s = _states[i];
                if (!s.IsRunning) continue;

                s.Progress += deltaSeconds;
                if (s.Progress < s.CycleSeconds) continue;

                long completions;
                if (s.HasManager)
                {
                    completions = (long)(s.Progress / s.CycleSeconds);
                    s.Progress -= completions * s.CycleSeconds;
                }
                else
                {
                    completions = 1;
                    s.Progress = 0d;
                    s.IsRunning = false;
                }

                double multiplier = completions * _economy.GlobalRevenueMultiplier * _globalMultiplier *
                                    DayNight.Multiplier(s.Definition.Affinity, hourOfDay, _config.DayNight);
                BigDouble earned = s.RevenuePerCycle * multiplier;
                _economy.Earn(earned);
                CycleCompleted?.Invoke(i, earned);
            }
        }

        /// <summary>Tap du joueur : lance un cycle sur un commerce débloqué, sans gérant, à l'arrêt.</summary>
        public bool Tap(int index)
        {
            var s = _states[index];
            if (s.Level <= 0 || s.HasManager || s.IsRunning) return false;
            s.IsRunning = true;
            s.Progress = 0d;
            return true;
        }

        public BuyQuote Quote(int index, BuyMode mode)
        {
            var s = _states[index];
            var def = s.Definition;
            long count;
            switch (mode)
            {
                case BuyMode.Ten: count = 10; break;
                case BuyMode.Hundred: count = 100; break;
                case BuyMode.Max:
                    count = EconomyMath.MaxAffordable(def.BaseCost, def.Growth, s.Level, _economy.Money);
                    if (count == 0) count = 1;
                    break;
                case BuyMode.NextMilestone:
                    long next = EconomyMath.NextMilestone(s.Level, _config.Milestones);
                    count = next > 0 ? next - s.Level : 1;
                    break;
                default: count = 1; break;
            }
            return QuoteLevels(index, count);
        }

        public BuyQuote QuoteLevels(int index, long count)
        {
            var s = _states[index];
            BigDouble cost = EconomyMath.CostForLevels(s.Definition.BaseCost, s.Definition.Growth, s.Level, count);
            return new BuyQuote(count, cost, _economy.CanAfford(cost));
        }

        public bool Buy(int index, BuyMode mode) => BuyLevels(index, Quote(index, mode));

        public bool BuyLevels(int index, long count) => BuyLevels(index, QuoteLevels(index, count));

        private bool BuyLevels(int index, BuyQuote quote)
        {
            if (quote.Count <= 0 || !_economy.TrySpend(quote.Cost)) return false;

            var s = _states[index];
            bool wasLocked = s.Level == 0;
            int milestonesBefore = s.MilestonesReached;

            s.Level += quote.Count;
            s.Recompute(_config);
            if (s.HasManager && !s.IsRunning) s.IsRunning = true;

            if (wasLocked) Unlocked?.Invoke(index);
            LevelChanged?.Invoke(index, s.Level);
            if (s.MilestonesReached > milestonesBefore) MilestoneReached?.Invoke(index, s.MilestonesReached);
            RecomputeGlobalMilestones(true);
            return true;
        }

        /// <summary>Niveau du commerce le moins avancé (base des paliers globaux).</summary>
        public long MinLevel()
        {
            long min = long.MaxValue;
            for (int i = 0; i < _states.Length; i++)
                if (_states[i].Level < min) min = _states[i].Level;
            return min == long.MaxValue ? 0 : min;
        }

        /// <summary>Prochain palier global, ou −1.</summary>
        public long NextGlobalMilestone() => EconomyMath.NextMilestone(MinLevel(), _config.GlobalMilestones);

        private void RecomputeGlobalMilestones(bool raiseEvents)
        {
            int reached = EconomyMath.MilestonesReached(MinLevel(), _config.GlobalMilestones);
            if (reached == GlobalMilestonesReached) return;
            bool increased = reached > GlobalMilestonesReached;
            GlobalMilestonesReached = reached;
            _globalMultiplier = Math.Pow(_config.GlobalMilestoneMultiplier, reached);
            if (raiseEvents && increased) GlobalMilestoneReached?.Invoke(reached);
        }

        public BigDouble ManagerCost(int index) => BigDouble.FromDouble(_states[index].Definition.ManagerCost);

        public bool CanHireManager(int index)
        {
            var s = _states[index];
            return s.Level > 0 && !s.HasManager;
        }

        public bool HireManager(int index)
        {
            var s = _states[index];
            if (!CanHireManager(index) || !_economy.TrySpend(ManagerCost(index))) return false;
            s.HasManager = true;
            s.IsRunning = true;
            ManagerHired?.Invoke(index);
            return true;
        }

        /// <summary>
        /// Revenu par seconde des commerces gérés, jour/nuit moyenné, multiplicateur permanent inclus,
        /// boost publicitaire exclu (géré séparément par le calcul hors ligne).
        /// </summary>
        public BigDouble ManagedIncomePerSecond()
        {
            BigDouble total = BigDouble.Zero;
            for (int i = 0; i < _states.Length; i++)
            {
                var s = _states[i];
                if (!s.HasManager || s.Level <= 0) continue;
                total += s.RevenuePerSecond * DayNight.AverageMultiplier(s.Definition.Affinity, _config.DayNight);
            }
            return total * (_economy.PermanentMultiplier * _globalMultiplier);
        }

        /// <summary>Revenu par seconde de tous les commerces débloqués (joueur actif), multiplicateurs globaux inclus.</summary>
        public BigDouble TotalIncomePerSecond()
        {
            BigDouble total = BigDouble.Zero;
            for (int i = 0; i < _states.Length; i++)
            {
                var s = _states[i];
                if (s.Level <= 0) continue;
                total += s.RevenuePerSecond * DayNight.AverageMultiplier(s.Definition.Affinity, _config.DayNight);
            }
            return total * (_economy.GlobalRevenueMultiplier * _globalMultiplier);
        }

        internal void ResetAll()
        {
            for (int i = 0; i < _states.Length; i++) _states[i].Reset(_config);
            RecomputeGlobalMilestones(false);
        }

        internal void Restore(int index, long level, bool hasManager, bool isRunning, double progress)
        {
            var s = _states[index];
            s.Level = Math.Max(0L, level);
            s.HasManager = hasManager && s.Level > 0;
            s.IsRunning = s.Level > 0 && (isRunning || s.HasManager);
            s.Progress = Math.Max(0d, progress);
            s.Recompute(_config);
            if (s.Progress >= s.CycleSeconds) s.Progress = s.HasManager ? 0d : s.CycleSeconds;
            RecomputeGlobalMilestones(false);
        }
    }
}
