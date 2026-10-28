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
        private readonly CashUpgradeDefinition[] _upgrades;
        private readonly bool[] _upgradeOwned;
        private double _cashGlobalMultiplier = 1d;
        /// <summary>Multiplicateur appliqué à tous les commerces : paliers globaux × upgrades FCFA « tous ».</summary>
        private double _allMultiplier = 1d;

        /// <summary>Upgrade FCFA acheté (index).</summary>
        public event Action<int> UpgradeBought;

        public int GlobalMilestonesReached { get; private set; }
        /// <summary>Multiplicateur de revenus des paliers globaux (appliqué à tous les commerces).</summary>
        public double GlobalMilestoneMultiplier => _globalMultiplier;
        /// <summary>Multiplicateur des upgrades FCFA qui visent tous les commerces.</summary>
        public double CashGlobalMultiplier => _cashGlobalMultiplier;
        /// <summary>Tout ce qui multiplie tous les commerces de la ville (paliers globaux et upgrades « tous »).</summary>
        public double AllBusinessesMultiplier => _allMultiplier;

        public BusinessSystem(CityDefinition city, EconomyConfig config, EconomySystem economy)
        {
            _config = config;
            _economy = economy;
            _states = new BusinessState[city.Businesses.Length];
            for (int i = 0; i < _states.Length; i++)
                _states[i] = new BusinessState(city.Businesses[i], i, config);
            _upgrades = city.Upgrades ?? Array.Empty<CashUpgradeDefinition>();
            _upgradeOwned = new bool[_upgrades.Length];
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

                double multiplier = completions * _economy.GlobalRevenueMultiplier * _allMultiplier *
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
            _allMultiplier = _globalMultiplier * _cashGlobalMultiplier;
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
            return total * (_economy.PermanentMultiplier * _allMultiplier);
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
            return total * (_economy.GlobalRevenueMultiplier * _allMultiplier);
        }

        internal void ResetAll(bool keepManagers = false)
        {
            for (int i = 0; i < _states.Length; i++) _states[i].Reset(_config, keepManagers);
            for (int i = 0; i < _upgradeOwned.Length; i++) _upgradeOwned[i] = false;
            _cashGlobalMultiplier = 1d;
            _allMultiplier = _globalMultiplier * _cashGlobalMultiplier;
            RecomputeGlobalMilestones(false);
        }

        // --- Upgrades FCFA (§6) ---

        public int UpgradeCount => _upgrades.Length;
        public CashUpgradeDefinition Upgrade(int index) => _upgrades[index];
        public bool OwnsUpgrade(int index) => _upgradeOwned[index];

        /// <summary>Un upgrade de commerce n'est proposé qu'une fois ce commerce débloqué.</summary>
        public bool IsUpgradeAvailable(int index)
        {
            if (_upgradeOwned[index]) return false;
            int target = _upgrades[index].BusinessIndex;
            return target < 0 || _states[target].Level > 0;
        }

        public BigDouble UpgradeCost(int index) => BigDouble.FromDouble(_upgrades[index].Cost);

        public bool BuyUpgrade(int index)
        {
            if (!IsUpgradeAvailable(index) || !_economy.TrySpend(UpgradeCost(index))) return false;
            ApplyUpgrade(index);
            UpgradeBought?.Invoke(index);
            return true;
        }

        private void ApplyUpgrade(int index)
        {
            var u = _upgrades[index];
            _upgradeOwned[index] = true;
            if (u.BusinessIndex < 0)
            {
                _cashGlobalMultiplier *= u.RevenueMultiplier;
                _allMultiplier = _globalMultiplier * _cashGlobalMultiplier;
            }
            else
            {
                var s = _states[u.BusinessIndex];
                s.LocalRevenueMultiplier *= u.RevenueMultiplier;
                s.Recompute(_config);
            }
        }

        /// <summary>Upgrades possédés (pour « racheter comme avant »).</summary>
        public bool[] UpgradesSnapshot() => (bool[])_upgradeOwned.Clone();

        /// <summary>Rachète, du moins cher au plus cher, les upgrades marqués dans <paramref name="owned"/>.</summary>
        public bool RebuyUpgrades(bool[] owned)
        {
            if (owned == null) return false;
            bool any = false;
            // Les upgrades sont définis par coût croissant : l'ordre des index suffit.
            for (int u = 0; u < Math.Min(owned.Length, _upgrades.Length); u++)
                if (owned[u] && IsUpgradeAvailable(u) && _economy.CanAfford(UpgradeCost(u))) any |= BuyUpgrade(u);
            return any;
        }

        public bool IsBelowUpgrades(bool[] owned)
        {
            if (owned == null) return false;
            for (int u = 0; u < Math.Min(owned.Length, _upgrades.Length); u++)
                if (owned[u] && !_upgradeOwned[u]) return true;
            return false;
        }

        internal string[] OwnedUpgradeIds()
        {
            int n = 0;
            for (int i = 0; i < _upgradeOwned.Length; i++) if (_upgradeOwned[i]) n++;
            var ids = new string[n];
            for (int i = 0, k = 0; i < _upgradeOwned.Length; i++) if (_upgradeOwned[i]) ids[k++] = _upgrades[i].Id;
            return ids;
        }

        internal void RestoreUpgrades(string[] ownedIds)
        {
            if (ownedIds == null) return;
            for (int i = 0; i < _upgrades.Length; i++)
                if (!_upgradeOwned[i] && Array.IndexOf(ownedIds, _upgrades[i].Id) >= 0) ApplyUpgrade(i);
        }

        /// <summary>Niveaux actuels (pour « racheter comme avant »).</summary>
        public long[] LevelsSnapshot()
        {
            var levels = new long[_states.Length];
            for (int i = 0; i < levels.Length; i++) levels[i] = _states[i].Level;
            return levels;
        }

        /// <summary>
        /// Achète, commerce par commerce, autant de niveaux que possible sans dépasser <paramref name="targetLevel"/>.
        /// Zéro allocation. Renvoie vrai si au moins un niveau a été acheté.
        /// </summary>
        public bool AutoBuyToLevel(long targetLevel)
        {
            bool any = false;
            for (int i = 0; i < _states.Length; i++)
                any |= BuyTowards(i, targetLevel);
            return any;
        }

        /// <summary>
        /// « Racheter comme avant » : remonte chaque commerce vers <paramref name="levels"/>, en commençant par les
        /// commerces les plus rentables (les derniers), dans la limite de l'argent disponible.
        /// </summary>
        public bool RebuyTowards(long[] levels)
        {
            if (levels == null) return false;
            bool any = false;
            for (int i = Math.Min(levels.Length, _states.Length) - 1; i >= 0; i--)
                any |= BuyTowards(i, levels[i]);
            return any;
        }

        public bool IsBelow(long[] levels)
        {
            if (levels == null) return false;
            for (int i = 0; i < Math.Min(levels.Length, _states.Length); i++)
                if (_states[i].Level < levels[i]) return true;
            return false;
        }

        private bool BuyTowards(int index, long target)
        {
            var s = _states[index];
            if (s.Level >= target) return false;
            long n = EconomyMath.MaxAffordable(s.Definition.BaseCost, s.Definition.Growth, s.Level, _economy.Money);
            if (n <= 0) return false;
            return BuyLevels(index, Math.Min(n, target - s.Level));
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
