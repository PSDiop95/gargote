using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>
    /// Gérants à collectionner (§5) : possession et niveau (1 à 10, cartes en double + FCFA), gérant affecté à chaque
    /// commerce, bonus passifs (permanents, jamais remis à zéro) et boosts actifs avec recharge.
    /// <para>Les boosts actifs ne sont qu'un bonus du joueur actif : rien ne baisse si on ne les utilise pas, ils ne
    /// s'appliquent jamais aux gains hors ligne, et deux boosts ne se cumulent pas sur un même commerce.</para>
    /// </summary>
    public sealed class ManagerCollection
    {
        private readonly MetaDefinitions _defs;
        private readonly Ledger _ledger;
        private readonly int[] _levels;
        private readonly double[] _boostLeft;
        private readonly double[] _cooldownLeft;
        private readonly Dictionary<string, int> _equipped = new Dictionary<string, int>();

        public event Action<int> Acquired;
        public event Action<int, int> LeveledUp;
        public event Action<int> BoostStarted;
        public event Action<int> BoostEnded;
        /// <summary>Un bonus passif a changé (possession, niveau, affectation).</summary>
        public event Action PassivesChanged;

        public ManagerCollection(MetaDefinitions defs, Ledger ledger)
        {
            _defs = defs;
            _ledger = ledger;
            int n = defs.Managers.Length;
            _levels = new int[n];
            _boostLeft = new double[n];
            _cooldownLeft = new double[n];
        }

        public int Count => _levels.Length;
        public ManagerDefinition Definition(int index) => _defs.Managers[index];
        public int IndexOf(string id) => _defs.ManagerIndex(id);
        public bool Owns(int index) => _levels[index] > 0;
        public int Level(int index) => _levels[index];
        public int MaxLevel => _defs.ManagerLevels.MaxLevel;
        public bool IsMaxed(int index) => _levels[index] >= MaxLevel;
        public long Cards(int index) => _ledger.Cards(_defs.Managers[index].Id);
        public bool IsBoostActive(int index) => _boostLeft[index] > 0d;
        public double BoostRemaining(int index) => _boostLeft[index];
        public double CooldownRemaining(int index) => _cooldownLeft[index];
        public bool IsBoostReady(int index) => Owns(index) && _boostLeft[index] <= 0d && _cooldownLeft[index] <= 0d;

        /// <summary>Niveau du meilleur gérant possédé pour ce commerce (0 si aucun).</summary>
        public int BestLevelFor(string businessId)
        {
            int best = 0;
            for (int i = 0; i < _levels.Length; i++)
                if (_levels[i] > best && _defs.Managers[i].BusinessId == businessId) best = _levels[i];
            return best;
        }

        public int OwnedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _levels.Length; i++) if (_levels[i] > 0) n++;
                return n;
            }
        }

        public int OwnedOfRarity(Rarity rarity)
        {
            int n = 0;
            for (int i = 0; i < _levels.Length; i++) if (_levels[i] > 0 && _defs.Managers[i].Rarity == rarity) n++;
            return n;
        }

        public int TotalLevels
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _levels.Length; i++) n += _levels[i];
                return n;
            }
        }

        /// <summary>Gérant affecté à un commerce (−1 si aucun gérant possédé pour ce commerce).</summary>
        public int Equipped(string businessId) => businessId != null && _equipped.TryGetValue(businessId, out int i) ? i : -1;

        /// <summary>Affecte un gérant possédé à son commerce (le boost actif suit le gérant affecté).</summary>
        public bool Equip(int index)
        {
            if (!Owns(index)) return false;
            var def = _defs.Managers[index];
            int current = Equipped(def.BusinessId);
            if (current == index) return true;
            if (current >= 0 && _boostLeft[current] > 0d) return false; // on ne change pas pendant un boost
            _equipped[def.BusinessId] = index;
            PassivesChanged?.Invoke();
            return true;
        }

        /// <summary>Gérant de base embauché en FCFA : obtenu au niveau 1 s'il n'est pas déjà dans la collection.</summary>
        public void GrantBase(string businessId)
        {
            for (int i = 0; i < _defs.Managers.Length; i++)
            {
                var d = _defs.Managers[i];
                if (d.BusinessId == businessId && d.IsBase && _levels[i] == 0) Acquire(i);
            }
        }

        /// <summary>
        /// Cartes gagnées (coffre, roue) : la première carte d'un gérant l'ajoute à la collection ; les suivantes sont des
        /// doublons pour monter de niveau ; un doublon d'un gérant au niveau max devient des cauris (rien n'est perdu).
        /// Renvoie les cauris de conversion.
        /// </summary>
        public long AddCards(int index, int cards, string reason)
        {
            if (cards <= 0) return 0;
            var def = _defs.Managers[index];
            if (_levels[index] == 0)
            {
                Acquire(index);
                cards--;
            }
            if (cards <= 0) return 0;
            if (IsMaxed(index))
            {
                long cauris = cards * (long)_defs.ManagerLevels.DuplicateCaurisAtMax[(int)def.Rarity];
                _ledger.Credit(Currency.Cauris, cauris, "duplicate_max");
                return cauris;
            }
            _ledger.Credit(Currency.Cards, cards, reason, def.Id);
            return 0;
        }

        public int CardsToNext(int index) => IsMaxed(index) || !Owns(index) ? 0 : _defs.ManagerLevels.CardsToNext[_levels[index] - 1];

        public BigDouble FcfaToNext(int index, BigDouble incomePerSecond) =>
            IsMaxed(index) || !Owns(index) ? BigDouble.Zero : incomePerSecond * _defs.ManagerLevels.FcfaSecondsToNext[_levels[index] - 1];

        public bool CanLevelUp(int index, EconomySystem economy, BigDouble incomePerSecond) =>
            Owns(index) && !IsMaxed(index) && _ledger.CanAfford(Currency.Cards, CardsToNext(index), _defs.Managers[index].Id) &&
            economy.CanAfford(FcfaToNext(index, incomePerSecond));

        public bool LevelUp(int index, EconomySystem economy, BigDouble incomePerSecond)
        {
            if (!CanLevelUp(index, economy, incomePerSecond)) return false;
            var cost = FcfaToNext(index, incomePerSecond);
            if (!economy.TrySpend(cost)) return false;
            if (!_ledger.TryDebit(Currency.Cards, CardsToNext(index), "manager_level", _defs.Managers[index].Id)) return false;
            _levels[index]++;
            LeveledUp?.Invoke(index, _levels[index]);
            PassivesChanged?.Invoke();
            return true;
        }

        // --- Bonus passifs ---

        /// <summary>Multiplicateur de revenus d'un commerce : 1 + Σ (bonus par niveau × niveau) des gérants possédés.</summary>
        public double BusinessMultiplier(string businessId)
        {
            double bonus = 0d;
            for (int i = 0; i < _levels.Length; i++)
            {
                var d = _defs.Managers[i];
                if (_levels[i] > 0 && d.Passive == PassiveScope.Business && d.BusinessId == businessId) bonus += d.PassivePerLevel * _levels[i];
            }
            return 1d + bonus;
        }

        /// <summary>Multiplicateur de tous les revenus (gérants à bonus global).</summary>
        public double GlobalMultiplier
        {
            get
            {
                double bonus = 0d;
                for (int i = 0; i < _levels.Length; i++)
                {
                    var d = _defs.Managers[i];
                    if (_levels[i] > 0 && d.Passive == PassiveScope.Global) bonus += d.PassivePerLevel * _levels[i];
                }
                return 1d + bonus;
            }
        }

        // --- Boosts actifs ---

        /// <summary>Le boost du gérant affecté à ce commerce peut-il être lancé ?</summary>
        public bool CanBoost(string businessId)
        {
            int i = Equipped(businessId);
            return i >= 0 && IsBoostReady(i);
        }

        /// <summary>Lance le boost du gérant affecté (choix du joueur actif). Faux s'il est actif ou en recharge.</summary>
        public bool ActivateBoost(string businessId)
        {
            int i = Equipped(businessId);
            if (i < 0 || !IsBoostReady(i)) return false;
            var d = _defs.Managers[i];
            _boostLeft[i] = d.BoostSeconds;
            _cooldownLeft[i] = d.BoostCooldownSeconds;
            BoostStarted?.Invoke(i);
            return true;
        }

        /// <summary>Recharge terminée tout de suite (cauris ou rewarded) ; sans effet sur un boost en cours.</summary>
        public bool AccelerateCooldown(int index)
        {
            if (!Owns(index) || _boostLeft[index] > 0d || _cooldownLeft[index] <= 0d) return false;
            _cooldownLeft[index] = 0d;
            return true;
        }

        /// <summary>Temps de jeu actif écoulé (les boosts se consomment, les recharges avancent).</summary>
        public void Tick(double seconds)
        {
            if (seconds <= 0d) return;
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_cooldownLeft[i] > 0d) _cooldownLeft[i] = Math.Max(0d, _cooldownLeft[i] - seconds);
                if (_boostLeft[i] <= 0d) continue;
                _boostLeft[i] = Math.Max(0d, _boostLeft[i] - seconds);
                if (_boostLeft[i] <= 0d) BoostEnded?.Invoke(i);
            }
        }

        /// <summary>Boost global actif le plus fort (les boosts globaux ne se cumulent pas entre eux).</summary>
        public double GlobalBoostMultiplier
        {
            get
            {
                double best = 1d;
                for (int i = 0; i < _levels.Length; i++)
                    if (_boostLeft[i] > 0d && _defs.Managers[i].BoostGlobal && _defs.Managers[i].BoostMultiplier > best)
                        best = _defs.Managers[i].BoostMultiplier;
                return best;
            }
        }

        /// <summary>Boost d'un commerce : boost de son gérant affecté (s'il n'est pas global) × boost global.</summary>
        public double BoostMultiplier(string businessId)
        {
            double local = 1d;
            int i = Equipped(businessId);
            if (i >= 0 && _boostLeft[i] > 0d && !_defs.Managers[i].BoostGlobal) local = _defs.Managers[i].BoostMultiplier;
            return local * GlobalBoostMultiplier;
        }

        private void Acquire(int index)
        {
            _levels[index] = 1;
            var d = _defs.Managers[index];
            int current = Equipped(d.BusinessId);
            // Affectation automatique du meilleur gérant (rareté puis niveau) : le joueur idle profite de tout.
            if (current < 0 || (d.Rarity > _defs.Managers[current].Rarity && _boostLeft[current] <= 0d)) _equipped[d.BusinessId] = index;
            Acquired?.Invoke(index);
            PassivesChanged?.Invoke();
        }

        internal ManagersSaveData Save()
        {
            var data = new ManagersSaveData
            {
                Ids = new string[_levels.Length],
                Levels = (int[])_levels.Clone(),
                BoostLeft = (double[])_boostLeft.Clone(),
                CooldownLeft = (double[])_cooldownLeft.Clone(),
                EquippedBusinesses = new string[_equipped.Count],
                EquippedManagers = new string[_equipped.Count],
            };
            for (int i = 0; i < _levels.Length; i++) data.Ids[i] = _defs.Managers[i].Id;
            int k = 0;
            foreach (var kv in _equipped)
            {
                data.EquippedBusinesses[k] = kv.Key;
                data.EquippedManagers[k] = _defs.Managers[kv.Value].Id;
                k++;
            }
            return data;
        }

        internal void Restore(ManagersSaveData data)
        {
            Array.Clear(_levels, 0, _levels.Length);
            Array.Clear(_boostLeft, 0, _boostLeft.Length);
            Array.Clear(_cooldownLeft, 0, _cooldownLeft.Length);
            _equipped.Clear();
            if (data?.Ids == null) return;
            for (int k = 0; k < data.Ids.Length; k++)
            {
                int i = IndexOf(data.Ids[k]);
                if (i < 0) continue; // gérant retiré du contenu : ignoré
                _levels[i] = Clamp(data.Levels, k, 0, MaxLevel);
                _boostLeft[i] = Math.Min(_defs.Managers[i].BoostSeconds, Math.Max(0d, At(data.BoostLeft, k)));
                _cooldownLeft[i] = Math.Min(_defs.Managers[i].BoostCooldownSeconds, Math.Max(0d, At(data.CooldownLeft, k)));
            }
            if (data.EquippedBusinesses != null && data.EquippedManagers != null)
                for (int k = 0; k < Math.Min(data.EquippedBusinesses.Length, data.EquippedManagers.Length); k++)
                {
                    int i = IndexOf(data.EquippedManagers[k]);
                    if (i >= 0 && _levels[i] > 0 && _defs.Managers[i].BusinessId == data.EquippedBusinesses[k]) _equipped[data.EquippedBusinesses[k]] = i;
                }
            // Commerces sans affectation sauvegardée valide : meilleur gérant possédé (le choix du joueur est conservé).
            var chosen = new HashSet<string>(_equipped.Keys);
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] == 0) continue;
                var d = _defs.Managers[i];
                if (chosen.Contains(d.BusinessId)) continue;
                int current = Equipped(d.BusinessId);
                if (current < 0 || d.Rarity > _defs.Managers[current].Rarity) _equipped[d.BusinessId] = i;
            }
            PassivesChanged?.Invoke();
        }

        private static int Clamp(int[] a, int i, int min, int max) => a != null && i < a.Length ? Math.Max(min, Math.Min(max, a[i])) : min;
        private static double At(double[] a, int i) => a != null && i < a.Length ? a[i] : 0d;
    }
}
