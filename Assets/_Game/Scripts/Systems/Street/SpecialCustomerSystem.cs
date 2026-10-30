using System;
using Gargote.Core;

namespace Gargote.Systems.Street
{
    /// <summary>
    /// Clients spéciaux qui traversent la rue (Fonctionnaire pressé, Touriste perdu…). Un seul à la fois.
    /// Le bonus est calculé et crédité ici ; la vue se contente d'afficher le passant et de transmettre le tap.
    /// </summary>
    public sealed class SpecialCustomerSystem
    {
        private readonly SpecialCustomerRules _rules;
        private readonly Func<BusinessSystem> _businesses;
        private readonly EconomySystem _economy;
        private readonly Random _random;
        private readonly double _totalWeight;
        private double _untilNextSpawn;

        /// <summary>Index du client en train de traverser, -1 sinon.</summary>
        public int ActiveIndex { get; private set; } = -1;
        /// <summary>Temps de traversée restant du client actif.</summary>
        public double ActiveRemainingSeconds { get; private set; }
        public double UntilNextSpawnSeconds => _untilNextSpawn;
        public SpecialCustomerDefinition Active => ActiveIndex >= 0 ? _rules.Customers[ActiveIndex] : null;

        /// <summary>Un client spécial commence à traverser (index dans les règles).</summary>
        public event Action<int> Spawned;
        /// <summary>Client tapé : (index, bonus crédité).</summary>
        public event Action<int, BigDouble> Rewarded;
        /// <summary>Client sorti de l'écran sans être tapé.</summary>
        public event Action<int> Missed;

        /// <param name="businesses">Fournit le BusinessSystem courant (il change avec la ville).</param>
        public SpecialCustomerSystem(SpecialCustomerRules rules, EconomySystem economy, Func<BusinessSystem> businesses, int seed)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _rules.Validate();
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _businesses = businesses ?? throw new ArgumentNullException(nameof(businesses));
            _random = new Random(seed);
            foreach (var c in _rules.Customers) _totalWeight += c.Weight;
            _untilNextSpawn = NextInterval();
        }

        public int UnlockedBusinesses()
        {
            var b = _businesses();
            int n = 0;
            for (int i = 0; i < b.Count; i++) if (b[i].IsUnlocked) n++;
            return n;
        }

        /// <summary>Bonus si ce client était tapé maintenant.</summary>
        public BigDouble BonusFor(int index)
        {
            var def = _rules.Customers[index];
            BigDouble fromIncome = _businesses().TotalIncomePerSecond() * def.BonusIncomeSeconds;
            return BigDouble.Max(BigDouble.FromDouble(def.MinBonus), fromIncome);
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds <= 0d || _rules.Customers.Length == 0) return;
            if (ActiveIndex >= 0)
            {
                ActiveRemainingSeconds -= deltaSeconds;
                if (ActiveRemainingSeconds > 0d) return;
                int missed = ActiveIndex;
                ActiveIndex = -1;
                ActiveRemainingSeconds = 0d;
                _untilNextSpawn = NextInterval();
                Missed?.Invoke(missed);
                return;
            }

            if (UnlockedBusinesses() < _rules.MinUnlockedBusinesses) return;
            _untilNextSpawn -= deltaSeconds;
            if (_untilNextSpawn > 0d) return;
            Spawn(PickIndex());
        }

        /// <summary>Force un client (menu debug). Faux si un client traverse déjà.</summary>
        public bool ForceSpawn(int index)
        {
            if (ActiveIndex >= 0 || index < 0 || index >= _rules.Customers.Length) return false;
            Spawn(index);
            return true;
        }

        /// <summary>Tap du joueur sur le client actif : crédite le bonus une seule fois.</summary>
        public bool TryTap(out BigDouble bonus)
        {
            bonus = BigDouble.Zero;
            if (ActiveIndex < 0) return false;
            int index = ActiveIndex;
            bonus = BonusFor(index);
            _economy.Earn(bonus);
            ActiveIndex = -1;
            ActiveRemainingSeconds = 0d;
            _untilNextSpawn = NextInterval();
            Rewarded?.Invoke(index, bonus);
            return true;
        }

        private void Spawn(int index)
        {
            ActiveIndex = index;
            ActiveRemainingSeconds = _rules.Customers[index].CrossingSeconds;
            Spawned?.Invoke(index);
        }

        private int PickIndex()
        {
            double roll = _random.NextDouble() * _totalWeight;
            for (int i = 0; i < _rules.Customers.Length; i++)
            {
                roll -= _rules.Customers[i].Weight;
                if (roll < 0d) return i;
            }
            return _rules.Customers.Length - 1;
        }

        private double NextInterval() =>
            _rules.MinIntervalSeconds + _random.NextDouble() * (_rules.MaxIntervalSeconds - _rules.MinIntervalSeconds);
    }
}
