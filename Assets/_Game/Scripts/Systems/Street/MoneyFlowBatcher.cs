using System;
using Gargote.Core;

namespace Gargote.Systems.Street
{
    /// <summary>Envol de billets décidé par le batcher : nombre de billets affichés et cycles regroupés.</summary>
    public readonly struct BillBurst
    {
        public readonly int BusinessIndex;
        /// <summary>Billets à faire voler (2-5), ou 1 gros billet si regroupement.</summary>
        public readonly int Bills;
        /// <summary>Cycles représentés par cet envol (« x12 » si &gt; 1).</summary>
        public readonly long Cycles;
        /// <summary>Montant total déjà crédité par l'économie (affichage seulement).</summary>
        public readonly BigDouble Amount;

        public BillBurst(int businessIndex, int bills, long cycles, BigDouble amount)
        {
            BusinessIndex = businessIndex;
            Bills = bills;
            Cycles = cycles;
            Amount = amount;
        }

        public bool IsGrouped => Cycles > 1;
    }

    /// <summary>
    /// Regroupe les fins de cycle en envols de billets lisibles. Purement cosmétique : l'argent est déjà crédité
    /// par <see cref="BusinessSystem"/> au moment du cycle. Tableaux préalloués, aucune allocation après construction.
    /// </summary>
    public sealed class MoneyFlowBatcher
    {
        private readonly MoneyFlowRules _rules;
        private readonly long[] _pendingCycles;
        private readonly BigDouble[] _pendingAmount;
        private readonly double[] _lastBurst;
        private double _lastGlobalBurst = double.NegativeInfinity;

        public MoneyFlowBatcher(int businessCount, MoneyFlowRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _pendingCycles = new long[businessCount];
            _pendingAmount = new BigDouble[businessCount];
            _lastBurst = new double[businessCount];
            for (int i = 0; i < businessCount; i++) _lastBurst[i] = double.NegativeInfinity;
        }

        public int Count => _pendingCycles.Length;
        public long PendingCycles(int index) => _pendingCycles[index];

        /// <summary>Billets pour un gain : 2 à 5 selon la taille relative au revenu total par seconde.</summary>
        public static int BillsFor(BigDouble earned, BigDouble totalIncomePerSecond, MoneyFlowRules rules)
        {
            int min = Math.Max(1, rules.MinBills), max = Math.Max(min, rules.MaxBills);
            if (earned <= BigDouble.Zero) return min;
            if (totalIncomePerSecond <= BigDouble.Zero) return max;
            // Secondes de revenu représentées par ce gain : 1 s ou moins → min, chaque doublement → +1 billet.
            double seconds = (earned / totalIncomePerSecond).ToDouble();
            if (double.IsNaN(seconds) || seconds <= 1d) return min;
            int extra = double.IsInfinity(seconds) ? max : (int)Math.Floor(Math.Log(seconds, 2d));
            return Math.Min(max, min + extra);
        }

        /// <summary>Enregistre une fin de cycle (event CycleCompleted de l'économie).</summary>
        public void Add(int index, BigDouble earned, long cycles = 1)
        {
            _pendingCycles[index] += Math.Max(1, cycles);
            _pendingAmount[index] += earned;
        }

        /// <summary>Oublie ce qui est en attente (commerce passé hors écran, changement de ville).</summary>
        public void Clear(int index)
        {
            _pendingCycles[index] = 0;
            _pendingAmount[index] = BigDouble.Zero;
        }

        /// <summary>
        /// Émet au plus un envol pour ce commerce si les intervalles le permettent.
        /// Un seul cycle en attente → 2-5 billets ; plusieurs → 1 gros billet « xN ».
        /// </summary>
        public bool TryEmit(int index, double now, BigDouble totalIncomePerSecond, out BillBurst burst)
        {
            burst = default;
            long cycles = _pendingCycles[index];
            if (cycles <= 0) return false;
            if (now - _lastBurst[index] < _rules.MinBurstIntervalSeconds) return false;
            if (now - _lastGlobalBurst < _rules.MinGlobalBurstIntervalSeconds) return false;

            BigDouble amount = _pendingAmount[index];
            int bills = cycles == 1 ? BillsFor(amount, totalIncomePerSecond, _rules) : 1;
            burst = new BillBurst(index, bills, cycles, amount);
            Clear(index);
            _lastBurst[index] = now;
            _lastGlobalBurst = now;
            return true;
        }
    }
}
