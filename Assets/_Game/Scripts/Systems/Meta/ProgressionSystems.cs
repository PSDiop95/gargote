using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>
    /// Niveau joueur (XP) : dévoile les fonctionnalités une par une (onboarding naturel) et récompense chaque niveau
    /// (cauris, coffre tous les N niveaux). L'XP n'est jamais retirée.
    /// </summary>
    public sealed class PlayerLevelSystem
    {
        private readonly PlayerLevelConfig _config;

        public long Xp { get; private set; }
        public int Level { get; private set; } = 1;
        /// <summary>Dernier niveau dont la récompense a été donnée.</summary>
        public int RewardedLevel { get; private set; } = 1;

        /// <summary>(ancien niveau, nouveau niveau).</summary>
        public event Action<int, int> LeveledUp;
        public event Action<Feature> FeatureUnlocked;

        public PlayerLevelSystem(PlayerLevelConfig config) { _config = config; }

        public PlayerLevelConfig Config => _config;
        public int MaxLevel => _config.XpToReach.Length;
        public bool IsMax => Level >= MaxLevel;
        public long XpForLevel(int level) => _config.XpToReach[Math.Max(0, Math.Min(MaxLevel, level) - 1)];
        public long XpIntoLevel => Xp - XpForLevel(Level);
        public long XpToNext => IsMax ? 0 : XpForLevel(Level + 1) - XpForLevel(Level);
        public double Progress01 => IsMax ? 1d : (double)XpIntoLevel / Math.Max(1L, XpToNext);

        public bool IsUnlocked(Feature feature) => Level >= _config.UnlockLevel(feature);
        public long XpFor(XpSource source) => (int)source < _config.Xp.Length ? _config.Xp[(int)source] : 0;

        public void Add(XpSource source, double times = 1d)
        {
            long amount = (long)Math.Round(XpFor(source) * times);
            AddRaw(amount);
        }

        public void AddRaw(long amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            int before = Level;
            while (Level < MaxLevel && Xp >= XpForLevel(Level + 1)) Level++;
            if (Level == before) return;
            LeveledUp?.Invoke(before, Level);
            for (int f = 0; f < _config.FeatureLevels.Length; f++)
            {
                int at = _config.FeatureLevels[f];
                if (at > before && at <= Level) FeatureUnlocked?.Invoke((Feature)f);
            }
        }

        /// <summary>Récompenses des niveaux atteints et pas encore payés (appelé par la méta, qui crédite).</summary>
        internal int TakePendingRewardLevels()
        {
            int pending = Level - RewardedLevel;
            RewardedLevel = Level;
            return pending;
        }

        public bool LevelGivesChest(int level) => _config.ChestEvery > 0 && level % _config.ChestEvery == 0;

        internal void Restore(long xp, int rewardedLevel)
        {
            Xp = Math.Max(0L, xp);
            Level = 1;
            while (Level < MaxLevel && Xp >= XpForLevel(Level + 1)) Level++;
            RewardedLevel = Math.Max(1, Math.Min(Level, rewardedLevel));
        }
    }

    /// <summary>
    /// Album de recettes : une carte par plat, débloquée au premier service du commerce (et à son niveau requis).
    /// Album d'une ville complet = bonus permanent (<see cref="MetaDefinitions.AlbumCityBonusMultiplier"/>).
    /// </summary>
    public sealed class AlbumSystem
    {
        private readonly MetaDefinitions _defs;
        private readonly Dictionary<string, string> _cityOfBusiness;
        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private readonly HashSet<string> _completedCities = new HashSet<string>();
        private readonly HashSet<string> _seen = new HashSet<string>();

        public event Action<RecipeDefinition> RecipeUnlocked;
        public event Action<string> CityCompleted;

        public AlbumSystem(MetaDefinitions defs, GameDefinitions game)
        {
            _defs = defs;
            _cityOfBusiness = new Dictionary<string, string>();
            foreach (var city in game.Cities)
                foreach (var b in city.Businesses) _cityOfBusiness[b.Id] = city.Id;
        }

        public int Count => _unlocked.Count;
        public int Total => _defs.Recipes.Length;
        public bool IsUnlocked(string recipeId) => _unlocked.Contains(recipeId);
        public bool IsSeen(string recipeId) => _seen.Contains(recipeId);
        public void MarkSeen(string recipeId) { if (_unlocked.Contains(recipeId)) _seen.Add(recipeId); }
        public int UnseenCount { get { int n = 0; foreach (var id in _unlocked) if (!_seen.Contains(id)) n++; return n; } }
        public bool IsCityComplete(string cityId) => _completedCities.Contains(cityId);
        public string CityOf(RecipeDefinition r) => _cityOfBusiness.TryGetValue(r.BusinessId, out var c) ? c : "";

        public int CountInCity(string cityId, bool unlockedOnly)
        {
            int n = 0;
            foreach (var r in _defs.Recipes)
                if (CityOf(r) == cityId && (!unlockedOnly || _unlocked.Contains(r.Id))) n++;
            return n;
        }

        /// <summary>Multiplicateur permanent des albums complets (produit sur les villes).</summary>
        public double Multiplier
        {
            get
            {
                double m = 1d;
                for (int i = 0; i < _completedCities.Count; i++) m *= _defs.AlbumCityBonusMultiplier;
                return m;
            }
        }

        /// <summary>Le commerce a servi (au moins une fois) et atteint <paramref name="level"/> : débloque ses plats.</summary>
        public void OnBusinessProgress(string businessId, long level)
        {
            foreach (var r in _defs.Recipes)
            {
                if (r.BusinessId != businessId || level < r.UnlockLevel || !_unlocked.Add(r.Id)) continue;
                RecipeUnlocked?.Invoke(r);
                CheckCity(CityOf(r));
            }
        }

        private void CheckCity(string cityId)
        {
            if (string.IsNullOrEmpty(cityId) || _completedCities.Contains(cityId)) return;
            if (CountInCity(cityId, true) < CountInCity(cityId, false)) return;
            _completedCities.Add(cityId);
            CityCompleted?.Invoke(cityId);
        }

        internal string[] SaveUnlocked() { var a = new string[_unlocked.Count]; _unlocked.CopyTo(a); return a; }
        internal string[] SaveSeen() { var a = new string[_seen.Count]; _seen.CopyTo(a); return a; }

        internal void Restore(string[] unlocked, string[] seen)
        {
            _unlocked.Clear();
            _seen.Clear();
            _completedCities.Clear();
            if (unlocked != null)
                foreach (var id in unlocked)
                    foreach (var r in _defs.Recipes)
                        if (r.Id == id) _unlocked.Add(id);
            if (seen != null) foreach (var id in seen) if (_unlocked.Contains(id)) _seen.Add(id);
            // Les albums complets se recalculent (contenu ajouté plus tard = album à compléter de nouveau, bonus conservé
            // seulement s'il reste complet : jamais de bonus pour un album partiel).
            foreach (var city in _cityOfBusiness.Values)
                if (!_completedCities.Contains(city) && CountInCity(city, false) > 0 && CountInCity(city, true) == CountInCity(city, false))
                    _completedCities.Add(city);
        }
    }

    /// <summary>
    /// Succès à paliers (bronze / argent / or) lus sur <see cref="GameStats"/>. Un palier atteint est réclamable
    /// (point rouge) ; la réclamation crédite les cauris (fait par la méta). Rien n'est jamais perdu.
    /// </summary>
    public sealed class AchievementSystem
    {
        private readonly AchievementDefinition[] _defs;
        private readonly GameStats _stats;
        private readonly int[] _claimed;
        private int _scan;

        public event Action<int, int> TierReached;

        public AchievementSystem(AchievementDefinition[] defs, GameStats stats)
        {
            _defs = defs ?? Array.Empty<AchievementDefinition>();
            _stats = stats;
            _claimed = new int[_defs.Length];
            _reachedNotified = new int[_defs.Length];
        }

        private readonly int[] _reachedNotified;

        public int Count => _defs.Length;
        public AchievementDefinition Definition(int i) => _defs[i];
        public int ClaimedTiers(int i) => _claimed[i];
        public double Value(int i) => _stats.Get(_defs[i].Stat, _defs[i].BusinessId);

        public int ReachedTiers(int i)
        {
            double v = Value(i);
            var tiers = _defs[i].Tiers;
            int n = 0;
            while (n < tiers.Length && v >= tiers[n] - 1e-9) n++;
            return n;
        }

        public bool IsClaimable(int i) => ReachedTiers(i) > _claimed[i];
        public int TotalTiers { get { int n = 0; foreach (var d in _defs) n += d.Tiers.Length; return n; } }
        public int TotalClaimed { get { int n = 0; foreach (int c in _claimed) n += c; return n; } }

        public int ClaimableCount
        {
            get { int n = 0; for (int i = 0; i < _defs.Length; i++) if (IsClaimable(i)) n++; return n; }
        }

        /// <summary>Réclame le palier suivant : cauris du palier, ou 0 si rien à réclamer.</summary>
        public long Claim(int i)
        {
            if (!IsClaimable(i)) return 0;
            long cauris = _defs[i].CaurisRewards[_claimed[i]];
            _claimed[i]++;
            return cauris;
        }

        /// <summary>Balaye une petite partie des succès par appel (coût borné par frame) et notifie les paliers neufs.</summary>
        public void Scan(int budget)
        {
            if (_defs.Length == 0) return;
            for (int k = 0; k < Math.Min(budget, _defs.Length); k++)
            {
                _scan = (_scan + 1) % _defs.Length;
                int reached = ReachedTiers(_scan);
                if (reached <= _reachedNotified[_scan]) continue;
                _reachedNotified[_scan] = reached;
                TierReached?.Invoke(_scan, reached);
            }
        }

        internal void Save(out string[] ids, out int[] claimed)
        {
            ids = new string[_defs.Length];
            claimed = (int[])_claimed.Clone();
            for (int i = 0; i < _defs.Length; i++) ids[i] = _defs[i].Id;
        }

        internal void Restore(string[] ids, int[] claimed)
        {
            Array.Clear(_claimed, 0, _claimed.Length);
            if (ids != null && claimed != null)
                for (int k = 0; k < Math.Min(ids.Length, claimed.Length); k++)
                    for (int i = 0; i < _defs.Length; i++)
                        if (_defs[i].Id == ids[k]) _claimed[i] = Math.Max(0, Math.Min(_defs[i].Tiers.Length, claimed[k]));
            // Déjà atteints au chargement : pas de nouvelle annonce, seulement le point rouge.
            for (int i = 0; i < _defs.Length; i++) _reachedNotified[i] = ReachedTiers(i);
        }
    }
}
