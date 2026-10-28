using System;
using System.Collections.Generic;
using System.Globalization;
using Gargote.Core;
using Gargote.Systems;

namespace Gargote.Simulation
{
    /// <summary>
    /// Simulation de profils de joueurs sur N jours (§4.5), en C# pur : lançable en EditMode,
    /// en batchmode via un menu Editor, ou depuis un exécutable .NET.
    /// Le joueur simulé tape les commerces non gérés, achète selon sa politique (meilleur rendement,
    /// le moins cher ou au hasard), embauche les gérants dès que possible et prestige quand les étoiles
    /// à gagner doublent son bonus ou quand il stagne.
    /// </summary>
    public sealed class EconomySimulator
    {
        private const int MaxPurchasesPerDecision = 500;
        private const double SnapshotEveryActiveMinutes = 5;

        private readonly GameDefinitions _definitions;
        private readonly SimulationSettings _settings;

        public EconomySimulator(GameDefinitions definitions, SimulationSettings settings)
        {
            _definitions = definitions;
            _settings = settings;
        }

        public SimulationReport Run()
        {
            var report = new SimulationReport();
            foreach (var profile in _settings.Profiles)
                report.Profiles.Add(RunProfile(profile));
            EvaluateAdGaps(report);
            return report;
        }

        public ProfileResult RunProfile(PlayerProfile profile)
        {
            var result = new ProfileResult { Profile = profile, AdsPerDay = new int[_settings.Days] };
            var session = new GameSession(_definitions);
            var economy = session.Economy;
            var config = _definitions.Economy;
            var random = new Random(profile.Seed);

            double elapsed = 0d;       // secondes réelles depuis le jour 1 à 00:00
            double active = 0d;        // secondes de jeu actif
            double lastObjective = 0d; // temps actif du dernier objectif atteint
            string lastObjectiveName = "start";
            int adsTotal = 0;
            int managers = 0;
            var state = new DecisionState();

            void Record(string kind, string detail, bool isObjective)
            {
                result.Events.Add(new SimulationEvent
                {
                    Profile = profile.Name, Kind = kind, Detail = detail,
                    ElapsedHours = elapsed / 3600d, ActiveMinutes = active / 60d,
                });
                if (!isObjective) return;
                double gap = (active - lastObjective) / 60d;
                if (gap > result.LongestWallMinutes)
                {
                    result.LongestWallMinutes = gap;
                    result.LongestWallAfter = lastObjectiveName;
                    result.LongestWallAtActiveMinutes = lastObjective / 60d;
                }
                lastObjective = active;
                lastObjectiveName = kind + ":" + detail;
            }

            void Subscribe(BusinessSystem b)
            {
                b.Unlocked += i => Record("unlock", b[i].Definition.Id, true);
                b.ManagerHired += i => { managers++; Record("manager", b[i].Definition.Id, true); };
                b.MilestoneReached += (i, count) =>
                    Record("milestone", b[i].Definition.Id + "@" + config.Milestones[count - 1].ToString(CultureInfo.InvariantCulture), true);
                b.UpgradeBought += i => Record("upgrade", b.Upgrade(i).Id, true);
                b.GlobalMilestoneReached += count =>
                    Record("global_milestone", "all@" + config.GlobalMilestones[count - 1].ToString(CultureInfo.InvariantCulture), true);
            }

            Subscribe(session.Businesses);
            session.CityChanged += Subscribe;

            int count = session.Businesses.Count;
            var idleSince = new double[count];
            double sessionGapHours = (profile.SleepHour - profile.WakeHour) / Math.Max(1, profile.SessionsPerDay);
            double lastSessionEnd = -1d;
            double nextSnapshotActive = 0d;

            void Snapshot(int day, int index)
            {
                long levels = 0;
                for (int i = 0; i < session.Businesses.Count; i++) levels += session.Businesses[i].Level;
                result.Snapshots.Add(new SimulationSnapshot
                {
                    Profile = profile.Name, Day = day + 1, Session = index + 1,
                    ElapsedHours = elapsed / 3600d, ActiveMinutes = active / 60d,
                    Money = economy.Money, CityLifetimeEarnings = economy.CityLifetimeEarnings,
                    TotalLifetimeEarnings = economy.TotalLifetimeEarnings,
                    IncomePerSecond = session.Businesses.TotalIncomePerSecond(),
                    Stars = session.Prestige.TotalStars, StarMultiplier = session.Prestige.RevenueMultiplier,
                    GlobalMilestoneMultiplier = session.Businesses.GlobalMilestoneMultiplier,
                    WelcomeMultiplier = economy.WelcomeMultiplier,
                    CashUpgrades = CountOwnedUpgrades(session.Businesses),
                    CityIndex = session.CityIndex,
                    Prestiges = session.Prestige.PrestigeCount, AdsWatched = adsTotal,
                    ManagersHired = managers, TotalLevels = levels,
                });
            }

            for (int day = 0; day < _settings.Days; day++)
            {
                int boostAdsToday = 0;
                for (int s = 0; s < profile.SessionsPerDay; s++)
                {
                    double start = day * 86400d + (profile.WakeHour + s * sessionGapHours) * 3600d;
                    elapsed = start;

                    // Retour : gains hors ligne (+ x2 rewarded, valeur décroissante au-delà du quota), puis boost x2.
                    if (lastSessionEnd >= 0d)
                    {
                        double away = start - lastSessionEnd;
                        var offline = session.ComputeOffline(away, config.Offline.BaseCapSeconds);
                        double multiplier = 1d;
                        if (offline.HasEarnings && profile.WatchesAds && away >= _settings.OfflineAdMinAwayMinutes * 60d)
                        {
                            multiplier = session.RewardOfflineAd(day);
                            adsTotal++;
                            result.AdsPerDay[day]++;
                        }
                        session.ClaimOffline(offline, multiplier);
                    }
                    if (profile.WatchesAds && boostAdsToday < _settings.MaxBoostAdsPerDay &&
                        session.Businesses.TotalIncomePerSecond() > BigDouble.Zero &&
                        economy.AdBoostRemainingSeconds <= _settings.AdBoostRewatchBelowHours * 3600d &&
                        economy.CanAddAdBoost)
                    {
                        if (random.NextDouble() >= profile.BoostSkipChance && session.RewardBoostAd(day))
                        {
                            boostAdsToday++;
                            adsTotal++;
                            result.AdsPerDay[day]++;
                        }
                    }

                    double minutes = day == 0 && s == 0 && profile.FirstSessionMinutes > 0
                        ? profile.FirstSessionMinutes
                        : profile.SessionMinutes;
                    long steps = (long)Math.Round(minutes * 60d / _settings.TickSeconds);
                    double sinceDecision = profile.DecisionIntervalSeconds; // décision immédiate en début de session
                    for (int i = 0; i < count; i++) idleSince[i] = elapsed;

                    for (long step = 0; step < steps; step++)
                    {
                        double dt = _settings.TickSeconds;
                        double hour = DayNight.HourOfDay(elapsed, config.DayNight);
                        session.Tick(dt, hour);
                        elapsed += dt;
                        active += dt;
                        sinceDecision += dt;

                        var businesses = session.Businesses;
                        for (int i = 0; i < businesses.Count; i++)
                        {
                            var b = businesses[i];
                            if (b.IsRunning || b.HasManager || b.Level == 0) { idleSince[i] = elapsed; continue; }
                            if (elapsed - idleSince[i] >= profile.TapReactionSeconds) businesses.Tap(i);
                        }

                        if (sinceDecision >= profile.DecisionIntervalSeconds)
                        {
                            sinceDecision = 0d;
                            Decide(session, profile, random, Record, (active - lastObjective) / 60d, active / 60d, state, result);
                        }

                        if (active / 60d >= nextSnapshotActive)
                        {
                            Snapshot(day, s);
                            nextSnapshotActive += SnapshotEveryActiveMinutes;
                        }
                    }
                    lastSessionEnd = elapsed;
                    Snapshot(day, s);
                }
            }

            // Mur final : temps actif depuis le dernier objectif.
            double tail = (active - lastObjective) / 60d;
            if (tail > result.LongestWallMinutes)
            {
                result.LongestWallMinutes = tail;
                result.LongestWallAfter = lastObjectiveName;
                result.LongestWallAtActiveMinutes = lastObjective / 60d;
            }

            Evaluate(result);
            return result;
        }

        private sealed class DecisionState
        {
            public bool PrestigeAnnounced;
            public bool GoalAnnounced;
            public bool Recovering;
            public bool RecoveryWithComfort;
            public double RecoveryStartMinutes;
            public BigDouble RecoveryTarget;
        }

        private static bool OwnsAllUpgrades(PrestigeSystem prestige)
        {
            for (int i = 0; i < prestige.UpgradeCount; i++)
                if (!prestige.Owns(i)) return false;
            return prestige.UpgradeCount > 0;
        }

        private void Decide(GameSession session, PlayerProfile profile, Random random,
            Action<string, string, bool> record, double stalledMinutes, double activeMinutes, DecisionState state,
            ProfileResult result)
        {
            var businesses = session.Businesses;
            var economy = session.Economy;
            var prestige = session.Prestige;

            // 0. Upgrades d'étoiles de confort, dans l'ordre, dès qu'ils sont abordables.
            // Le joueur n'en achète un que s'il coûte au plus UpgradeMaxShareOfStars de ses étoiles disponibles.
            for (int i = 0; i < prestige.UpgradeCount; i++)
                if (!prestige.Owns(i) && prestige.Upgrade(i).CostStars <= prestige.AvailableStars * _settings.UpgradeMaxShareOfStars &&
                    session.BuyStarUpgrade(i)) record("star_upgrade", prestige.Upgrade(i).Id, true);

            // 1. Gérants en priorité (automatisation = moins de taps, gains hors ligne), puis « racheter comme avant ».
            HireManagers(businesses, economy);
            if (session.CanRebuyPrevious) session.RebuyPrevious();

            // 2. Achats selon la politique du joueur.
            for (int n = 0; n < MaxPurchasesPerDecision; n++)
            {
                bool bought;
                switch (profile.Purchases)
                {
                    case PurchasePolicy.Cheapest: bought = BuyCheapest(businesses, economy); break;
                    case PurchasePolicy.Random: bought = BuyRandom(businesses, economy, random); break;
                    default:
                        var best = PurchaseAdvisor.FindBestLevels(businesses, economy, _definitions.Economy);
                        bought = economy.CanAfford(best.Cost) && PurchaseAdvisor.Apply(businesses, economy, best);
                        break;
                }
                if (!bought) break;
                HireManagers(businesses, economy);
            }

            // Temps de remontée après une relance : revenu ≥ part cible du revenu d'avant.
            if (state.Recovering && businesses.TotalIncomePerSecond() >= state.RecoveryTarget)
            {
                double minutes = activeMinutes - state.RecoveryStartMinutes;
                (state.RecoveryWithComfort ? result.ComfortRecoveries : result.EarlyRecoveries).Add(minutes);
                record("recovery", minutes.ToString("0.##", CultureInfo.InvariantCulture) + (state.RecoveryWithComfort ? " comfort" : " early"), false);
                state.Recovering = false;
            }

            // 3. Objectif de ville / prestige.
            if (!state.GoalAnnounced && session.IsCityGoalReached)
            {
                state.GoalAnnounced = true;
                record("city_goal", session.City.Id, true);
            }
            if (session.IsCityGoalReached && session.HasNextCity)
            {
                long gained = session.StarsToGain;
                double multiplierBefore = prestige.RevenueMultiplier;
                session.PerformPrestige(true);
                result.TravelRatios.Add(prestige.RevenueMultiplier / multiplierBefore);
                var welcome = _definitions.Economy.Welcome;
                if (economy.WelcomeMultiplier == welcome.Multiplier && economy.WelcomeRemainingSeconds == welcome.DurationSeconds)
                    result.WelcomeApplied++;
                state.Recovering = false;
                state.GoalAnnounced = false;
                state.PrestigeAnnounced = false;
                record("city_enter", session.City.Id, true);
                record("prestige", "+" + gained.ToString(CultureInfo.InvariantCulture) + " stars (new city)", true);
                return;
            }

            if (!session.CanPrestige) return;
            long toGain = session.StarsToGain;
            if (!state.PrestigeAnnounced)
            {
                state.PrestigeAnnounced = true;
                record("prestige_possible", "+" + toGain.ToString(CultureInfo.InvariantCulture) + " stars", true);
            }
            long owned = session.Prestige.TotalStars;
            bool doubles = toGain >= Math.Max(1L, (long)Math.Ceiling(owned * _settings.PrestigeWhenGainRatio));
            bool stalled = stalledMinutes >= _settings.PrestigeWhenStalledMinutes &&
                           toGain >= Math.Max(1L, (long)Math.Ceiling(owned * _settings.StalledGainRatio));
            if (doubles || stalled)
            {
                if (state.Recovering) // relance avant d'avoir remonté : comptée comme non remontée
                {
                    (state.RecoveryWithComfort ? result.ComfortRecoveries : result.EarlyRecoveries).Add(double.PositiveInfinity);
                    record("recovery", "never" + (state.RecoveryWithComfort ? " comfort" : " early"), false);
                }
                double multiplierBefore = prestige.RevenueMultiplier;
                BigDouble incomeBefore = businesses.TotalIncomePerSecond();
                session.PerformPrestige(false);
                // Remontée mesurée seulement pour une relance qui vaut le coup (gain de multiplicateur suffisant).
                state.Recovering = prestige.RevenueMultiplier >= multiplierBefore * (1d + _settings.Targets.RecoveryMinMultiplierGain);
                state.RecoveryWithComfort = OwnsAllUpgrades(prestige);
                state.RecoveryStartMinutes = activeMinutes;
                state.RecoveryTarget = incomeBefore * _settings.Targets.RecoveryShare;
                state.PrestigeAnnounced = false;
                record("prestige", "+" + toGain.ToString(CultureInfo.InvariantCulture) + " stars (replay " + session.City.Id + ")", true);
            }
        }

        private static int CountOwnedUpgrades(BusinessSystem businesses)
        {
            int n = 0;
            for (int u = 0; u < businesses.UpgradeCount; u++) if (businesses.OwnsUpgrade(u)) n++;
            return n;
        }

        private static void HireManagers(BusinessSystem businesses, EconomySystem economy)
        {
            for (int i = 0; i < businesses.Count; i++)
                if (businesses.CanHireManager(i) && economy.CanAfford(businesses.ManagerCost(i)))
                    businesses.HireManager(i);
        }

        private static bool BuyCheapest(BusinessSystem businesses, EconomySystem economy)
        {
            int best = -1, bestUpgrade = -1;
            BigDouble bestCost = BigDouble.Zero;
            for (int i = 0; i < businesses.Count; i++)
            {
                var q = businesses.Quote(i, BuyMode.One);
                if (best < 0 || q.Cost < bestCost) { best = i; bestCost = q.Cost; }
            }
            for (int u = 0; u < businesses.UpgradeCount; u++)
            {
                if (!businesses.IsUpgradeAvailable(u)) continue;
                var c = businesses.UpgradeCost(u);
                if (c < bestCost) { bestUpgrade = u; bestCost = c; }
            }
            if (!economy.CanAfford(bestCost)) return false;
            return bestUpgrade >= 0 ? businesses.BuyUpgrade(bestUpgrade) : businesses.BuyLevels(best, 1);
        }

        private static bool BuyRandom(BusinessSystem businesses, EconomySystem economy, Random random)
        {
            int affordable = 0;
            for (int i = 0; i < businesses.Count; i++)
                if (businesses.Quote(i, BuyMode.One).Affordable) affordable++;
            for (int u = 0; u < businesses.UpgradeCount; u++)
                if (businesses.IsUpgradeAvailable(u) && economy.CanAfford(businesses.UpgradeCost(u))) affordable++;
            if (affordable == 0) return false;
            int pick = random.Next(affordable);
            for (int i = 0; i < businesses.Count; i++)
            {
                if (!businesses.Quote(i, BuyMode.One).Affordable) continue;
                if (pick-- == 0) return businesses.BuyLevels(i, 1);
            }
            for (int u = 0; u < businesses.UpgradeCount; u++)
            {
                if (!businesses.IsUpgradeAvailable(u) || !economy.CanAfford(businesses.UpgradeCost(u))) continue;
                if (pick-- == 0) return businesses.BuyUpgrade(u);
            }
            return false;
        }

        private static string Fmt(double v, string unit) => v.ToString("0.##", CultureInfo.InvariantCulture) + unit;

        private void Evaluate(ProfileResult r)
        {
            var t = _settings.Targets;
            string name = r.Profile.Name;

            void Check(string metric, double? value, string unit, double min, double max, bool enforce = true, string target = null)
            {
                r.Checks.Add(new TargetCheck
                {
                    Profile = name,
                    Metric = metric,
                    Value = value.HasValue ? Fmt(value.Value, unit) : "never",
                    Target = !enforce ? "info" : target ?? (min > 0 ? Fmt(min, "") + "-" : "<= ") + Fmt(max, unit),
                    Pass = !enforce || value.HasValue && value.Value >= min && value.Value <= max,
                });
            }

            // Les cibles en jours (§4.5) visent le joueur de référence (régulier) ; les autres profils sont
            // reportés à titre d'information, un joueur accro avançant naturellement plus vite.
            bool reference = r.Profile.IsReference;
            // Le joueur sous-optimal n'est jugé que sur ses murs : ses temps de déblocage sont indiqués pour info.
            bool optimal = r.Profile.Purchases == PurchasePolicy.Optimal;

            var firstManager = r.FirstEvent("manager");
            Check("first_manager_active_minutes", firstManager?.ActiveMinutes, " min", 0, t.FirstManagerMaxMinutes, optimal);

            var businesses = _definitions.Cities[0].Businesses;
            if (t.FourthBusinessIndex < businesses.Length)
            {
                var unlock = r.FirstEvent("unlock", businesses[t.FourthBusinessIndex].Id);
                Check("business4_active_minutes", unlock?.ActiveMinutes, " min", 0, t.FourthBusinessMaxMinutes, optimal);
            }
            if (t.SpacedUnlockIndex + 1 < businesses.Length)
            {
                var a = r.FirstEvent("unlock", businesses[t.SpacedUnlockIndex].Id);
                var b = r.FirstEvent("unlock", businesses[t.SpacedUnlockIndex + 1].Id);
                double? gap = a != null && b != null ? b.ActiveMinutes - a.ActiveMinutes : (double?)null;
                Check(businesses[t.SpacedUnlockIndex].Id + "_to_" + businesses[t.SpacedUnlockIndex + 1].Id + "_minutes",
                    gap, " min", t.MinUnlockSpacingMinutes, double.MaxValue, optimal, ">= " + Fmt(t.MinUnlockSpacingMinutes, " min"));
            }

            // Cibles en jours : bornes strictes sur la valeur exacte (jour 1,0 = début de la première journée),
            // sans arrondi ni journée calendaire de tolérance.
            var possible = r.FirstEvent("prestige_possible");
            Check("first_prestige_possible_day", possible?.Day, " d", t.FirstPrestigeMinDay, t.FirstPrestigeMaxDay, reference);

            var cityEnters = r.Events.FindAll(e => e.Kind == "city_enter");
            for (int c = 0; c < t.CityMinDays.Length; c++)
            {
                double? day = c < cityEnters.Count ? cityEnters[c].Day : (double?)null;
                Check("city" + (c + 2).ToString(CultureInfo.InvariantCulture) + "_day", day, " d",
                    t.CityMinDays[c], t.CityMaxDays[c], reference);
            }

            // Prestiges libres par ville (le prestige reste fréquent à l'intérieur d'une ville).
            int cityIndex = 0, replays = 0;
            foreach (var e in r.Events)
            {
                if (e.Kind == "city_enter")
                {
                    Check("replays_in_city" + (cityIndex + 1).ToString(CultureInfo.InvariantCulture), replays, "", 0, 0, false);
                    cityIndex++;
                    replays = 0;
                }
                else if (e.Kind == "prestige" && e.Detail.Contains("replay")) replays++;
            }

            var starSnapshot = r.SnapshotAtDay(t.StarMultiplierDay);
            Check("star_multiplier_day" + t.StarMultiplierDay.ToString(CultureInfo.InvariantCulture),
                starSnapshot?.StarMultiplier, "x", 0, t.MaxStarMultiplier, reference);

            Check("longest_wall_active_minutes", r.LongestWallMinutes, " min", 0, t.MaxWallMinutes);

            // Arrivée dans une nouvelle ville : plancher du multiplicateur et bonus de bienvenue.
            if (r.TravelRatios.Count > 0)
            {
                double min = double.MaxValue;
                foreach (double ratio in r.TravelRatios) if (ratio < min) min = ratio;
                Check("travel_multiplier_ratio_min", min + 1e-9, "", t.MinTravelFloorRatio, double.MaxValue, true,
                    ">= " + Fmt(t.MinTravelFloorRatio, ""));
                var welcome = _definitions.Economy.Welcome;
                r.Checks.Add(new TargetCheck
                {
                    Profile = name, Metric = "welcome_bonus_on_arrival",
                    Value = r.WelcomeApplied.ToString(CultureInfo.InvariantCulture) + "/" + r.TravelRatios.Count.ToString(CultureInfo.InvariantCulture),
                    Target = "x" + Fmt(welcome.Multiplier, "") + " " + Fmt(welcome.DurationSeconds / 60d, " min") + " each arrival",
                    Pass = r.WelcomeApplied == r.TravelRatios.Count && welcome.Multiplier > 1 && welcome.DurationSeconds > 0,
                });
            }

            // Temps de remontée après une relance (upgrades de confort achetés) : jugé pour les joueurs optimaux.
            if (r.ComfortRecoveries.Count > 0)
            {
                var sorted = new List<double>(r.ComfortRecoveries);
                sorted.Sort();
                double worst = sorted[sorted.Count - 1];
                Check("prestige_recovery_max_minutes", double.IsInfinity(worst) ? (double?)null : worst, " min",
                    0, t.MaxRecoveryMinutes, optimal);
                Check("prestige_recovery_median_minutes", sorted[sorted.Count / 2], " min", 0, 0, false);
                Check("prestige_recoveries_measured", sorted.Count, "", 0, 0, false);
            }
            else
            {
                Check("prestige_recovery_max_minutes", null, " min", 0, t.MaxRecoveryMinutes, optimal && r.Profile.IsReference);
            }

            if (r.Profile.WatchesAds)
            {
                double sum = 0;
                foreach (int a in r.AdsPerDay) sum += a;
                // KPI §12 : au moins 4 par joueur actif et par jour. Le haut de fourchette (8) est indicatif :
                // au-delà, les rewarded perdent de la valeur (RewardedConfig) au lieu d'être interdites.
                Check("rewarded_per_day", sum / Math.Max(1, r.AdsPerDay.Length), "", t.MinAdsPerDay, double.MaxValue, true,
                    ">= " + Fmt(t.MinAdsPerDay, "") + " (indicatif <= " + Fmt(t.MaxAdsPerDay, "") + ")");
            }
        }

        /// <summary>
        /// Écart pub / sans pub : pour chaque paire « X_pub » / « X_sans_pub », le joueur sans pub ne doit pas être
        /// plus de MaxAdGapRatio fois plus lent (arrivée en ville 2, prestiges au jour du multiplicateur).
        /// </summary>
        private void EvaluateAdGaps(SimulationReport report)
        {
            const string withAds = "_pub";
            const string withoutAds = "_sans_pub";
            var t = _settings.Targets;
            foreach (var noAds in report.Profiles)
            {
                string n = noAds.Profile.Name;
                if (!n.EndsWith(withoutAds, StringComparison.Ordinal)) continue;
                var ads = report.Profiles.Find(p => p.Profile.Name == n.Substring(0, n.Length - withoutAds.Length) + withAds);
                if (ads == null) continue;

                var cityAds = ads.FirstEvent("city_enter");
                var cityNoAds = noAds.FirstEvent("city_enter");
                double? cityRatio = cityAds != null && cityNoAds != null ? cityNoAds.Day / cityAds.Day : (double?)null;
                report.AdGaps.Add(new AdGap { Pair = n.Substring(0, n.Length - withoutAds.Length), City2Ratio = cityRatio });
                noAds.Checks.Add(new TargetCheck
                {
                    Profile = n, Metric = "ad_gap_city2_ratio",
                    Value = cityRatio.HasValue ? Fmt(cityRatio.Value, "x") : "never",
                    Target = "<= " + Fmt(t.MaxAdGapRatio, "x"),
                    Pass = cityRatio.HasValue && cityRatio.Value <= t.MaxAdGapRatio,
                });

                var sa = ads.SnapshotAtDay(t.StarMultiplierDay);
                var sn = noAds.SnapshotAtDay(t.StarMultiplierDay);
                double? prestigeRatio = sa != null && sn != null && sn.Prestiges > 0 ? (double)sa.Prestiges / sn.Prestiges : (double?)null;
                report.AdGaps[report.AdGaps.Count - 1].PrestigeRatio = prestigeRatio;
                noAds.Checks.Add(new TargetCheck
                {
                    Profile = n, Metric = "ad_gap_prestiges_day" + t.StarMultiplierDay.ToString(CultureInfo.InvariantCulture),
                    Value = prestigeRatio.HasValue ? Fmt(prestigeRatio.Value, "x") : "never",
                    Target = "<= " + Fmt(t.MaxAdGapRatio, "x"),
                    Pass = prestigeRatio.HasValue && prestigeRatio.Value <= t.MaxAdGapRatio,
                });
            }
        }

        /// <summary>
        /// Contenu de lancement = Dakar seule en M1. Pour mesurer les villes suivantes, on ajoute des villes
        /// « proxy » clonées de la dernière ville connue, chaque ville étant plus exigeante que la précédente
        /// (<see cref="ProxyCityScaling"/>, composé). Les vraies villes (M6) suivront la même courbe.
        /// </summary>
        public static GameDefinitions WithProxyCities(GameDefinitions source, int totalCities, ProxyCityScaling scaling = null)
        {
            scaling = scaling ?? new ProxyCityScaling();
            var cities = new List<CityDefinition>(source.Cities);
            var template = source.Cities[source.Cities.Length - 1];
            for (int i = cities.Count; i < totalCities; i++)
            {
                int step = i - source.Cities.Length + 1;
                double cost = Math.Pow(scaling.CostMultiplier, step);
                double revenue = Math.Pow(scaling.RevenueMultiplier, step);
                var businesses = new BusinessDefinition[template.Businesses.Length];
                for (int b = 0; b < businesses.Length; b++)
                {
                    var d = template.Businesses[b];
                    businesses[b] = new BusinessDefinition
                    {
                        Id = d.Id, Growth = d.Growth, BaseCycleSeconds = d.BaseCycleSeconds, Affinity = d.Affinity,
                        BaseCost = d.BaseCost * cost, ManagerCost = d.ManagerCost * cost, BaseRevenue = d.BaseRevenue * revenue,
                    };
                }
                var upgrades = new CashUpgradeDefinition[template.Upgrades.Length];
                for (int u = 0; u < upgrades.Length; u++)
                {
                    var d = template.Upgrades[u];
                    upgrades[u] = new CashUpgradeDefinition
                    {
                        Id = d.Id, BusinessIndex = d.BusinessIndex, RevenueMultiplier = d.RevenueMultiplier,
                        Cost = d.Cost * cost * Math.Pow(scaling.UpgradeCostMultiplier, step),
                    };
                }
                double baseThreshold = template.StarThreshold > 0 ? template.StarThreshold : source.Prestige.Threshold;
                cities.Add(new CityDefinition
                {
                    Id = "city" + (i + 1).ToString(CultureInfo.InvariantCulture) + "_proxy",
                    Businesses = businesses,
                    Upgrades = upgrades,
                    GoalLifetimeEarnings = template.GoalLifetimeEarnings * scaling.GoalFactor(step),
                    StartingMoney = template.StartingMoney * cost,
                    StarThreshold = baseThreshold * Math.Pow(scaling.StarThresholdMultiplier, step),
                });
            }
            return new GameDefinitions { Cities = cities.ToArray(), Economy = source.Economy, Prestige = source.Prestige };
        }
    }
}
