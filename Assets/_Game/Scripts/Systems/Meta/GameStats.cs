using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>Statistiques d'un commerce (toutes relances confondues).</summary>
    public sealed class BusinessStats
    {
        public double Customers;
        public long MaxLevel;
        public int MaxMilestones;
        public bool FirstServed;
    }

    /// <summary>
    /// Compteurs du joueur, alimentés par les événements des systèmes. Lus par les missions, les succès et l'album.
    /// Le chemin chaud (clients servis à chaque cycle) n'alloue rien : tableaux indexés par commerce de la ville.
    /// </summary>
    public sealed class GameStats
    {
        private const int GlobalCount = 30;
        private readonly double[] _global = new double[GlobalCount];
        private readonly Dictionary<string, BusinessStats> _byBusiness = new Dictionary<string, BusinessStats>();
        private BusinessStats[] _current = Array.Empty<BusinessStats>();

        /// <summary>Premier service d'un commerce dans la ville en cours (index).</summary>
        public event Action<int> FirstServed;
        /// <summary>Niveau du meilleur gérant possédé pour un commerce (fourni par la collection).</summary>
        public Func<string, double> ManagerLevelOf;

        public double Get(StatId stat) => (int)stat < GlobalCount ? _global[(int)stat] : 0d;

        public double Get(StatId stat, string businessId)
        {
            if ((int)stat < 100) return Get(stat);
            if (stat == StatId.BusinessManagerLevel) return businessId != null && ManagerLevelOf != null ? ManagerLevelOf(businessId) : 0d;
            if (businessId == null || !_byBusiness.TryGetValue(businessId, out var b)) return 0d;
            switch (stat)
            {
                case StatId.BusinessLevel: return b.MaxLevel;
                case StatId.BusinessCustomers: return b.Customers;
                case StatId.BusinessMilestones: return b.MaxMilestones;
                case StatId.BusinessManagerLevel: return ManagerLevelOf != null ? ManagerLevelOf(businessId) : 0d;
                default: return 0d;
            }
        }

        public BusinessStats For(string businessId)
        {
            if (!_byBusiness.TryGetValue(businessId, out var b))
            {
                b = new BusinessStats();
                _byBusiness[businessId] = b;
            }
            return b;
        }

        /// <summary>Commerce de la ville en cours (index).</summary>
        public BusinessStats Current(int index) => _current[index];

        internal void BindCity(BusinessSystem businesses)
        {
            _current = new BusinessStats[businesses.Count];
            for (int i = 0; i < _current.Length; i++) _current[i] = For(businesses[i].Definition.Id);
        }

        internal void Add(StatId stat, double amount)
        {
            if ((int)stat < GlobalCount && amount > 0d) _global[(int)stat] += amount;
        }

        /// <summary>Pour les statistiques « record » (meilleure série, niveau joueur, étoiles…).</summary>
        internal void Max(StatId stat, double value)
        {
            if ((int)stat < GlobalCount && value > _global[(int)stat]) _global[(int)stat] = value;
        }

        internal void Set(StatId stat, double value)
        {
            if ((int)stat < GlobalCount) _global[(int)stat] = value;
        }

        internal void OnServed(int index, long customers)
        {
            var b = _current[index];
            b.Customers += customers;
            _global[(int)StatId.Customers] += customers;
            if (b.FirstServed) return;
            b.FirstServed = true;
            FirstServed?.Invoke(index);
        }

        internal void OnLevel(int index, long level, int milestones)
        {
            var b = _current[index];
            if (level > b.MaxLevel) b.MaxLevel = level;
            if (milestones > b.MaxMilestones) b.MaxMilestones = milestones;
        }

        internal StatsSaveData Save()
        {
            var data = new StatsSaveData
            {
                Global = (double[])_global.Clone(),
                BusinessIds = new string[_byBusiness.Count],
                Customers = new double[_byBusiness.Count],
                MaxLevels = new long[_byBusiness.Count],
                MaxMilestones = new int[_byBusiness.Count],
                FirstServed = new bool[_byBusiness.Count],
            };
            int i = 0;
            foreach (var kv in _byBusiness)
            {
                data.BusinessIds[i] = kv.Key;
                data.Customers[i] = kv.Value.Customers;
                data.MaxLevels[i] = kv.Value.MaxLevel;
                data.MaxMilestones[i] = kv.Value.MaxMilestones;
                data.FirstServed[i] = kv.Value.FirstServed;
                i++;
            }
            return data;
        }

        internal void Restore(StatsSaveData data)
        {
            Array.Clear(_global, 0, _global.Length);
            _byBusiness.Clear();
            if (data == null) return;
            if (data.Global != null)
                for (int i = 0; i < Math.Min(GlobalCount, data.Global.Length); i++) _global[i] = Math.Max(0d, data.Global[i]);
            if (data.BusinessIds == null) return;
            for (int i = 0; i < data.BusinessIds.Length; i++)
            {
                if (string.IsNullOrEmpty(data.BusinessIds[i])) continue;
                var b = For(data.BusinessIds[i]);
                b.Customers = At(data.Customers, i);
                b.MaxLevel = data.MaxLevels != null && i < data.MaxLevels.Length ? data.MaxLevels[i] : 0;
                b.MaxMilestones = data.MaxMilestones != null && i < data.MaxMilestones.Length ? data.MaxMilestones[i] : 0;
                b.FirstServed = data.FirstServed != null && i < data.FirstServed.Length && data.FirstServed[i];
            }
        }

        private static double At(double[] a, int i) => a != null && i < a.Length ? Math.Max(0d, a[i]) : 0d;
    }
}
