using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>État du jeu dont les missions ont besoin pour rester faisables (implémenté par <see cref="MetaSession"/>).</summary>
    public interface IMissionContext
    {
        GameSession Session { get; }
        GameStats Stats { get; }
        ManagerCollection Managers { get; }
        bool IsUnlocked(Feature feature);
        /// <summary>Les clients spéciaux peuvent-ils apparaître (rue présente) ?</summary>
        bool SpecialsAvailable { get; }
    }

    /// <summary>Mission (ou défi du jour) tirée d'un modèle et figée à sa création.</summary>
    public sealed class MissionInstance
    {
        public string TemplateId;
        public MissionKind Kind;
        /// <summary>Commerce visé (vide si global).</summary>
        public string BusinessId = "";
        /// <summary>Objectif : niveau absolu (ReachLevel) ou quantité à gagner depuis la création.</summary>
        public double Target;
        /// <summary>Valeur de la statistique à la création (missions « à partir de maintenant »).</summary>
        public double Baseline;
        public long Xp, Cauris, Tokens;
        public string ChestId = "";
        public bool Claimed;

        public double Progress01(IMissionContext ctx) => Target <= 0d ? 1d : Math.Min(1d, Current(ctx) / Target);
        public bool IsComplete(IMissionContext ctx) => Current(ctx) >= Target - 1e-9;

        /// <summary>Avancement brut (même unité que <see cref="Target"/>).</summary>
        public double Current(IMissionContext ctx)
        {
            var stats = ctx.Stats;
            var businesses = ctx.Session.Businesses;
            int b = string.IsNullOrEmpty(BusinessId) ? -1 : businesses.IndexOf(BusinessId);
            switch (Kind)
            {
                case MissionKind.ReachLevel: return b >= 0 ? businesses[b].Level : 0d;
                case MissionKind.HireManager: return b >= 0 && businesses[b].HasManager ? 1d : 0d;
                case MissionKind.ServeCustomers: return stats.Get(StatId.Customers) - Baseline;
                case MissionKind.ReachMilestone: return stats.Get(StatId.Milestones) - Baseline;
                case MissionKind.UseManagerBoost: return stats.Get(StatId.ManagerBoosts) - Baseline;
                case MissionKind.TapSpecials: return stats.Get(StatId.SpecialsTapped) - Baseline;
                case MissionKind.BuyUpgrades: return stats.Get(StatId.CashUpgrades) - Baseline;
                case MissionKind.OpenChests: return stats.Get(StatId.ChestsOpened) - Baseline;
                case MissionKind.EarnSecondsOfIncome: return stats.Get(StatId.LifetimeEarnings) - Baseline;
                case MissionKind.CompleteMissions: return stats.Get(StatId.MissionsCompleted) - Baseline;
                default: return 0d;
            }
        }
    }

    /// <summary>
    /// Tableau de missions : N emplacements tirés d'un pool pondéré, jamais deux du même type, jamais impossibles dans
    /// l'état actuel (chaque modèle vérifie ses prérequis et s'adapte à la progression).
    /// </summary>
    public sealed class MissionBoard
    {
        private readonly MissionDefinition[] _pool;
        private readonly int _slots;
        private readonly DeterministicRandom _random;
        private readonly MissionInstance[] _active;
        private readonly double[] _refillAt;
        private readonly double[] _weights;
        private readonly bool _challenge;

        public MissionBoard(MissionDefinition[] pool, int slots, DeterministicRandom random, bool challenge)
        {
            _pool = pool ?? Array.Empty<MissionDefinition>();
            _slots = Math.Max(0, slots);
            _random = random;
            _challenge = challenge;
            _active = new MissionInstance[_slots];
            _refillAt = new double[_slots];
            _weights = new double[_pool.Length];
        }

        public int Slots => _slots;
        public MissionInstance this[int slot] => _active[slot];
        public DeterministicRandom Random => _random;

        /// <summary>Remplit les emplacements vides dont le rechargement est terminé (si un modèle est faisable).</summary>
        public bool Fill(IMissionContext ctx, double trustedSeconds = double.MaxValue)
        {
            bool any = false;
            for (int s = 0; s < _slots; s++)
            {
                if (_active[s] != null || trustedSeconds < _refillAt[s]) continue;
                _active[s] = Create(ctx, s);
                any |= _active[s] != null;
            }
            return any;
        }

        /// <summary>Secondes de confiance auxquelles l'emplacement se recharge (0 = disponible).</summary>
        public double RefillAt(int slot) => _refillAt[slot];

        /// <summary>Mission réclamée : l'emplacement reste vide jusqu'à <paramref name="refillAt"/>.</summary>
        public void Clear(int slot, double refillAt)
        {
            _active[slot] = null;
            _refillAt[slot] = refillAt;
        }

        /// <summary>
        /// Remplace la mission d'un emplacement par une autre de type différent. Si aucun autre modèle n'est faisable,
        /// l'ancienne mission reste en place et le remplacement est refusé (faux) : un échange ne vide jamais un emplacement.
        /// </summary>
        public bool Replace(IMissionContext ctx, int slot)
        {
            var old = _active[slot];
            _active[slot] = null;
            var next = Create(ctx, slot, old?.Kind);
            _active[slot] = next ?? old;
            return next != null;
        }

        /// <summary>Tout renouveler (défis du jour à minuit).</summary>
        public void Reset(IMissionContext ctx)
        {
            for (int s = 0; s < _slots; s++) { _active[s] = null; _refillAt[s] = 0d; }
            Fill(ctx);
        }

        /// <summary>Une mission devenue impossible (ex. ville changée) est remplacée sans coût.</summary>
        public void Revalidate(IMissionContext ctx)
        {
            for (int s = 0; s < _slots; s++)
            {
                var m = _active[s];
                if (m == null || m.Claimed) continue;
                if (!StillFeasible(ctx, m)) Replace(ctx, s);
            }
        }

        private static bool StillFeasible(IMissionContext ctx, MissionInstance m)
        {
            var businesses = ctx.Session.Businesses;
            if (string.IsNullOrEmpty(m.BusinessId)) return true;
            int b = businesses.IndexOf(m.BusinessId);
            if (b < 0 || !businesses[b].IsUnlocked) return false;
            if (m.Kind == MissionKind.ReachLevel && m.Target > businesses[b].Level + 2000) return false;
            return true;
        }

        private MissionInstance Create(IMissionContext ctx, int slot, MissionKind? exclude = null)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                var t = _pool[i];
                bool used = exclude.HasValue && t.Kind == exclude.Value;
                for (int s = 0; s < _slots && !used; s++)
                    if (s != slot && _active[s] != null && _active[s].Kind == t.Kind) used = true;
                _weights[i] = used || !IsFeasible(ctx, t) ? 0d : t.Weight;
            }
            int pick = _random.Weighted(_weights);
            return pick < 0 ? null : Instantiate(ctx, _pool[pick]);
        }

        /// <summary>Le modèle peut-il produire une mission atteignable maintenant ?</summary>
        public bool IsFeasible(IMissionContext ctx, MissionDefinition t)
        {
            var businesses = ctx.Session.Businesses;
            switch (t.Kind)
            {
                case MissionKind.ReachLevel:
                case MissionKind.ReachMilestone:
                    return PickBusiness(ctx, t.Kind, false) >= 0;
                case MissionKind.HireManager:
                    return PickBusiness(ctx, t.Kind, false) >= 0;
                case MissionKind.ServeCustomers:
                case MissionKind.EarnSecondsOfIncome:
                    return businesses.TotalIncomePerSecond() > BigDouble.Zero;
                case MissionKind.UseManagerBoost:
                    return PickBusiness(ctx, t.Kind, false) >= 0;
                case MissionKind.TapSpecials:
                    return ctx.SpecialsAvailable;
                case MissionKind.BuyUpgrades:
                    for (int u = 0; u < businesses.UpgradeCount; u++) if (businesses.IsUpgradeAvailable(u)) return true;
                    return false;
                case MissionKind.OpenChests:
                    return ctx.IsUnlocked(Feature.Chests);
                case MissionKind.CompleteMissions:
                    return ctx.IsUnlocked(Feature.Missions);
                default:
                    return false;
            }
        }

        /// <summary>Commerce compatible avec le type (au hasard parmi les candidats si <paramref name="random"/>).</summary>
        private int PickBusiness(IMissionContext ctx, MissionKind kind, bool random)
        {
            var businesses = ctx.Session.Businesses;
            var milestones = ctx.Session.EconomyConfig.Milestones;
            int count = 0, chosen = -1;
            for (int i = 0; i < businesses.Count; i++)
            {
                var s = businesses[i];
                if (!s.IsUnlocked) continue;
                bool ok;
                switch (kind)
                {
                    case MissionKind.ReachLevel:
                    case MissionKind.ReachMilestone: ok = EconomyMath.NextMilestone(s.Level, milestones) > 0; break;
                    case MissionKind.HireManager: ok = !s.HasManager; break;
                    case MissionKind.UseManagerBoost: ok = ctx.Managers.Equipped(s.Definition.Id) >= 0; break;
                    default: ok = true; break;
                }
                if (!ok) continue;
                count++;
                // Tirage réservoir : un candidat uniforme sans liste intermédiaire.
                if (!random) return i;
                if (_random.Next(count) == 0) chosen = i;
            }
            return chosen;
        }

        private MissionInstance Instantiate(IMissionContext ctx, MissionDefinition t)
        {
            var m = new MissionInstance
            {
                TemplateId = t.Id, Kind = t.Kind, Xp = t.Xp, Cauris = t.Cauris, Tokens = t.Tokens, ChestId = t.ChestId ?? "",
            };
            var stats = ctx.Stats;
            var session = ctx.Session;
            var businesses = session.Businesses;
            switch (t.Kind)
            {
                case MissionKind.ReachLevel:
                {
                    int b = PickBusiness(ctx, t.Kind, true);
                    m.BusinessId = businesses[b].Definition.Id;
                    // Niveau visé = N paliers plus loin (le palier suivant pour une mission, deux pour un défi).
                    long level = businesses[b].Level;
                    long target = level;
                    for (int k = 0; k < Math.Max(1, (int)t.Amount); k++)
                    {
                        long next = EconomyMath.NextMilestone(target, session.EconomyConfig.Milestones);
                        if (next < 0) break;
                        target = next;
                    }
                    m.Target = target;
                    break;
                }
                case MissionKind.HireManager:
                    m.BusinessId = businesses[PickBusiness(ctx, t.Kind, true)].Definition.Id;
                    m.Target = 1;
                    break;
                case MissionKind.ReachMilestone:
                    m.Baseline = stats.Get(StatId.Milestones);
                    m.Target = Math.Max(1d, Math.Round(t.Amount));
                    break;
                case MissionKind.ServeCustomers:
                    m.Baseline = stats.Get(StatId.Customers);
                    m.Target = NiceRound(Math.Max(20d, CustomersPerSecond(businesses) * t.Amount));
                    break;
                case MissionKind.EarnSecondsOfIncome:
                    m.Baseline = stats.Get(StatId.LifetimeEarnings);
                    m.Target = NiceRound((businesses.TotalIncomePerSecond() / Math.Max(1d, session.Economy.TemporaryMultiplier)).ToDouble() * t.Amount);
                    break;
                case MissionKind.UseManagerBoost:
                    m.Baseline = stats.Get(StatId.ManagerBoosts);
                    m.Target = Math.Max(1d, Math.Round(t.Amount));
                    break;
                case MissionKind.TapSpecials:
                    m.Baseline = stats.Get(StatId.SpecialsTapped);
                    m.Target = Math.Max(1d, Math.Round(t.Amount));
                    break;
                case MissionKind.BuyUpgrades:
                    m.Baseline = stats.Get(StatId.CashUpgrades);
                    m.Target = Math.Max(1d, Math.Round(t.Amount));
                    break;
                case MissionKind.OpenChests:
                    m.Baseline = stats.Get(StatId.ChestsOpened);
                    m.Target = Math.Max(1d, Math.Round(t.Amount));
                    break;
                case MissionKind.CompleteMissions:
                    m.Baseline = stats.Get(StatId.MissionsCompleted);
                    m.Target = Math.Max(1d, Math.Round(t.Amount));
                    break;
            }
            return m;
        }

        /// <summary>Clients servis par seconde (commerces gérés ou débloqués, au rythme actuel).</summary>
        private static double CustomersPerSecond(BusinessSystem businesses)
        {
            double rate = 0d;
            for (int i = 0; i < businesses.Count; i++)
                if (businesses[i].IsUnlocked) rate += 1d / Math.Max(0.1d, businesses[i].CycleSeconds);
            return rate;
        }

        /// <summary>Arrondi lisible (2 chiffres significatifs) : 1 834 → 1 800.</summary>
        public static double NiceRound(double value)
        {
            if (value <= 0d || double.IsNaN(value) || double.IsInfinity(value)) return 1d;
            double magnitude = Math.Pow(10d, Math.Floor(Math.Log10(value)) - 1d);
            return Math.Max(1d, Math.Round(value / magnitude) * magnitude);
        }

        internal MissionSaveData[] Save()
        {
            var list = new MissionSaveData[_slots];
            for (int s = 0; s < _slots; s++)
            {
                var m = _active[s];
                if (m == null) continue;
                list[s] = new MissionSaveData
                {
                    TemplateId = m.TemplateId, Kind = (int)m.Kind, BusinessId = m.BusinessId, Target = m.Target,
                    Baseline = m.Baseline, Xp = m.Xp, Cauris = m.Cauris, Tokens = m.Tokens, ChestId = m.ChestId, Claimed = m.Claimed,
                };
            }
            return list;
        }

        internal double[] SaveRefill() => (double[])_refillAt.Clone();

        internal void RestoreRefill(double[] refillAt)
        {
            for (int s = 0; s < _slots; s++) _refillAt[s] = refillAt != null && s < refillAt.Length ? Math.Max(0d, refillAt[s]) : 0d;
        }

        internal void Restore(MissionSaveData[] data)
        {
            for (int s = 0; s < _slots; s++) _active[s] = null;
            if (data == null) return;
            for (int s = 0; s < Math.Min(_slots, data.Length); s++)
            {
                var d = data[s];
                if (d == null || Array.FindIndex(_pool, t => t.Id == d.TemplateId) < 0) continue; // modèle retiré : nouvelle mission
                _active[s] = new MissionInstance
                {
                    TemplateId = d.TemplateId, Kind = (MissionKind)d.Kind, BusinessId = d.BusinessId ?? "", Target = d.Target,
                    Baseline = d.Baseline, Xp = d.Xp, Cauris = d.Cauris, Tokens = d.Tokens, ChestId = d.ChestId ?? "", Claimed = d.Claimed,
                };
            }
        }
    }
}
