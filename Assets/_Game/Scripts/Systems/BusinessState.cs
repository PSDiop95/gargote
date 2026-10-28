using Gargote.Core;

namespace Gargote.Systems
{
    /// <summary>État mutable d'un commerce. Seul <see cref="BusinessSystem"/> le modifie.</summary>
    public sealed class BusinessState
    {
        public readonly BusinessDefinition Definition;
        public readonly int Index;

        public long Level { get; internal set; }
        public bool HasManager { get; internal set; }
        public bool IsRunning { get; internal set; }
        /// <summary>Secondes écoulées dans le cycle en cours.</summary>
        public double Progress { get; internal set; }
        public int MilestonesReached { get; private set; }

        /// <summary>Multiplicateurs locaux (upgrades, gérants à rareté — M3). 1 par défaut.</summary>
        public double LocalRevenueMultiplier { get; internal set; } = 1d;
        public double LocalSpeedMultiplier { get; internal set; } = 1d;

        public double CycleSeconds { get; private set; }
        /// <summary>Revenu par cycle hors multiplicateurs globaux (étoiles, boosts, jour/nuit).</summary>
        public BigDouble RevenuePerCycle { get; private set; }

        public bool IsUnlocked => Level > 0;
        public double Progress01 => CycleSeconds > 0 ? System.Math.Min(1d, Progress / CycleSeconds) : 0d;
        public BigDouble RevenuePerSecond => Level > 0 ? RevenuePerCycle / CycleSeconds : BigDouble.Zero;

        internal BusinessState(BusinessDefinition definition, int index, EconomyConfig config)
        {
            Definition = definition;
            Index = index;
            Recompute(config);
        }

        internal void Recompute(EconomyConfig config)
        {
            RevenuePerCycle = EconomyMath.RevenuePerCycle(Definition, Level, config, LocalRevenueMultiplier,
                LocalSpeedMultiplier, out double cycle);
            CycleSeconds = cycle;
            MilestonesReached = EconomyMath.MilestonesReached(Level, config.Milestones);
            if (Progress > CycleSeconds && !HasManager) Progress = CycleSeconds;
        }

        internal void Reset(EconomyConfig config)
        {
            Level = 0;
            HasManager = false;
            IsRunning = false;
            Progress = 0d;
            Recompute(config);
        }
    }
}
