using System;
using System.Collections.Generic;

namespace Gargote.Systems.Meta
{
    /// <summary>Monnaies et objets de la méta, tous tenus par le <see cref="Ledger"/>.</summary>
    public enum Currency
    {
        Cauris = 0,
        /// <summary>Jetons (défis du jour, puis événements en live ops).</summary>
        Tokens = 1,
        /// <summary>Cartes d'un gérant (compte = <see cref="LedgerEntry.Item"/>).</summary>
        Cards = 2,
        Xp = 3,
    }

    /// <summary>Un mouvement du registre : qui, combien, d'où (source) ou vers quoi (puits).</summary>
    public readonly struct LedgerEntry
    {
        public readonly Currency Currency;
        /// <summary>Objet concerné (id de gérant pour les cartes), vide sinon.</summary>
        public readonly string Item;
        /// <summary>Positif = gain (source), négatif = dépense (puits).</summary>
        public readonly long Amount;
        /// <summary>Id stable de la source ou du puits (« mission », « chest_gold », « boost_accelerate »…).</summary>
        public readonly string Reason;
        public readonly long BalanceAfter;
        /// <summary>Secondes de confiance (<see cref="Core.GameClock.TrustedSeconds"/>) au moment du mouvement.</summary>
        public readonly double AtSeconds;

        public LedgerEntry(Currency currency, string item, long amount, string reason, long balanceAfter, double atSeconds)
        {
            Currency = currency;
            Item = item;
            Amount = amount;
            Reason = reason;
            BalanceAfter = balanceAfter;
            AtSeconds = atSeconds;
        }
    }

    /// <summary>
    /// Registre unique : tout gain et toute dépense de cauris, jetons, cartes et XP passe ici, avec sa source ou son
    /// puits. Aucun solde ne peut devenir négatif (une dépense impossible est refusée et n'est pas tracée).
    /// Les totaux par (monnaie, raison) alimenteront les analytics (M5) ; l'historique récent sert au débogage.
    /// </summary>
    public sealed class Ledger
    {
        public const int HistoryCapacity = 256;

        private readonly long[] _balances = new long[4];
        private readonly Dictionary<string, long> _cards = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _earnedByReason = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _spentByReason = new Dictionary<string, long>();
        private readonly LedgerEntry[] _history = new LedgerEntry[HistoryCapacity];
        private readonly long[] _totalEarned = new long[4];
        private readonly long[] _totalSpent = new long[4];
        private int _historyStart, _historyCount;
        private Func<double> _now = () => 0d;

        /// <summary>Levé après chaque mouvement accepté.</summary>
        public event Action<LedgerEntry> Moved;

        public long MovementCount { get; private set; }

        internal void SetTimeSource(Func<double> now) => _now = now ?? (() => 0d);

        public long Balance(Currency currency) => currency == Currency.Cards ? TotalCards() : _balances[(int)currency];
        public long Cards(string managerId) => managerId != null && _cards.TryGetValue(managerId, out long n) ? n : 0L;
        public long TotalEarned(Currency currency) => _totalEarned[(int)currency];
        public long TotalSpent(Currency currency) => _totalSpent[(int)currency];

        public long EarnedBy(Currency currency, string reason) => _earnedByReason.TryGetValue(Key(currency, reason), out long n) ? n : 0L;
        public long SpentOn(Currency currency, string reason) => _spentByReason.TryGetValue(Key(currency, reason), out long n) ? n : 0L;

        public bool CanAfford(Currency currency, long amount, string item = null) =>
            amount >= 0 && (currency == Currency.Cards ? Cards(item) : _balances[(int)currency]) >= amount;

        /// <summary>Gain (source). Montant ≤ 0 ignoré.</summary>
        public void Credit(Currency currency, long amount, string reason, string item = null)
        {
            if (amount <= 0) return;
            Validate(currency, reason, item);
            long after = Apply(currency, item, amount);
            _totalEarned[(int)currency] += amount;
            Add(_earnedByReason, Key(currency, reason), amount);
            Record(new LedgerEntry(currency, item ?? string.Empty, amount, reason, after, _now()));
        }

        /// <summary>Dépense (puits). Faux, sans effet, si le solde est insuffisant.</summary>
        public bool TryDebit(Currency currency, long amount, string reason, string item = null)
        {
            if (amount < 0) return false;
            Validate(currency, reason, item);
            if (!CanAfford(currency, amount, item)) return false;
            if (amount == 0) return true;
            long after = Apply(currency, item, -amount);
            _totalSpent[(int)currency] += amount;
            Add(_spentByReason, Key(currency, reason), amount);
            Record(new LedgerEntry(currency, item ?? string.Empty, -amount, reason, after, _now()));
            return true;
        }

        /// <summary>Historique récent, du plus ancien au plus récent.</summary>
        public void CopyHistory(List<LedgerEntry> into)
        {
            into.Clear();
            for (int i = 0; i < _historyCount; i++) into.Add(_history[(_historyStart + i) % HistoryCapacity]);
        }

        public IEnumerable<KeyValuePair<string, long>> EarnedTotals => _earnedByReason;
        public IEnumerable<KeyValuePair<string, long>> SpentTotals => _spentByReason;

        private long TotalCards()
        {
            long n = 0;
            foreach (var kv in _cards) n += kv.Value;
            return n;
        }

        private long Apply(Currency currency, string item, long delta)
        {
            if (currency == Currency.Cards)
            {
                long next = Cards(item) + delta;
                _cards[item] = next;
                return next;
            }
            _balances[(int)currency] += delta;
            return _balances[(int)currency];
        }

        private static void Validate(Currency currency, string reason, string item)
        {
            if (string.IsNullOrEmpty(reason)) throw new ArgumentException("Every ledger movement needs a source or sink id.");
            if (currency == Currency.Cards && string.IsNullOrEmpty(item)) throw new ArgumentException("Card movements need a manager id.");
        }

        private void Record(LedgerEntry entry)
        {
            int index = (_historyStart + _historyCount) % HistoryCapacity;
            _history[index] = entry;
            if (_historyCount < HistoryCapacity) _historyCount++;
            else _historyStart = (_historyStart + 1) % HistoryCapacity;
            MovementCount++;
            Moved?.Invoke(entry);
        }

        private static string Key(Currency currency, string reason) => ((int)currency).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + reason;

        private static void Add(Dictionary<string, long> map, string key, long amount)
        {
            map.TryGetValue(key, out long current);
            map[key] = current + amount;
        }

        // --- Sauvegarde ---

        internal LedgerSaveData Save()
        {
            var data = new LedgerSaveData
            {
                Balances = (long[])_balances.Clone(),
                TotalEarned = (long[])_totalEarned.Clone(),
                TotalSpent = (long[])_totalSpent.Clone(),
                CardIds = new string[_cards.Count],
                CardCounts = new long[_cards.Count],
                EarnedKeys = new string[_earnedByReason.Count],
                EarnedValues = new long[_earnedByReason.Count],
                SpentKeys = new string[_spentByReason.Count],
                SpentValues = new long[_spentByReason.Count],
                MovementCount = MovementCount,
            };
            int i = 0;
            foreach (var kv in _cards) { data.CardIds[i] = kv.Key; data.CardCounts[i] = kv.Value; i++; }
            i = 0;
            foreach (var kv in _earnedByReason) { data.EarnedKeys[i] = kv.Key; data.EarnedValues[i] = kv.Value; i++; }
            i = 0;
            foreach (var kv in _spentByReason) { data.SpentKeys[i] = kv.Key; data.SpentValues[i] = kv.Value; i++; }
            return data;
        }

        internal void Restore(LedgerSaveData data)
        {
            Array.Clear(_balances, 0, _balances.Length);
            Array.Clear(_totalEarned, 0, _totalEarned.Length);
            Array.Clear(_totalSpent, 0, _totalSpent.Length);
            _cards.Clear();
            _earnedByReason.Clear();
            _spentByReason.Clear();
            _historyCount = 0;
            _historyStart = 0;
            if (data == null) return;
            // Un solde négatif (sauvegarde altérée) est ramené à zéro : le registre ne porte jamais de dette.
            CopyNonNegative(data.Balances, _balances);
            CopyNonNegative(data.TotalEarned, _totalEarned);
            CopyNonNegative(data.TotalSpent, _totalSpent);
            if (data.CardIds != null && data.CardCounts != null)
                for (int i = 0; i < Math.Min(data.CardIds.Length, data.CardCounts.Length); i++)
                    if (!string.IsNullOrEmpty(data.CardIds[i])) _cards[data.CardIds[i]] = Math.Max(0L, data.CardCounts[i]);
            RestoreMap(data.EarnedKeys, data.EarnedValues, _earnedByReason);
            RestoreMap(data.SpentKeys, data.SpentValues, _spentByReason);
            MovementCount = Math.Max(0L, data.MovementCount);
        }

        private static void CopyNonNegative(long[] from, long[] to)
        {
            if (from == null) return;
            for (int i = 0; i < Math.Min(from.Length, to.Length); i++) to[i] = Math.Max(0L, from[i]);
        }

        private static void RestoreMap(string[] keys, long[] values, Dictionary<string, long> map)
        {
            if (keys == null || values == null) return;
            for (int i = 0; i < Math.Min(keys.Length, values.Length); i++)
                if (!string.IsNullOrEmpty(keys[i])) map[keys[i]] = Math.Max(0L, values[i]);
        }
    }
}
