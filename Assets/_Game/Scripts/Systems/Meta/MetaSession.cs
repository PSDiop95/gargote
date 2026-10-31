using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>
    /// Composition de la méta (M3) autour d'une <see cref="GameSession"/> : collection de gérants, coffres, missions,
    /// défis, connexion, roue, niveau joueur, album, succès, points rouges. Logique pure, sans Unity : la présentation
    /// et la simulation appellent les mêmes méthodes. Toute monnaie passe par le <see cref="Ledger"/> (source + puits).
    /// L'économie M1 n'est touchée qu'à travers des multiplicateurs explicites (passifs, albums, boosts actifs).
    /// </summary>
    public sealed class MetaSession : IMissionContext
    {
        private readonly MetaDefinitions _defs;
        private readonly GameClock _clock;
        private BusinessSystem _bound;
        private double _lastTrusted;
        private double _attentionTimer;
        private bool _passivesDirty = true;
        private readonly HashSet<string> _seenUpgrades = new HashSet<string>();
        private readonly HashSet<int> _introduced = new HashSet<int>();
        private readonly Queue<ChestResult> _reveals = new Queue<ChestResult>();
        private readonly List<int> _cardCandidates = new List<int>(32);

        public GameSession Session { get; }
        public MetaDefinitions Definitions => _defs;
        public GameClock Clock => _clock;
        public Ledger Ledger { get; } = new Ledger();
        public GameStats Stats { get; } = new GameStats();
        public ManagerCollection Managers { get; }
        public ChestSystem Chests { get; }
        public MissionBoard Missions { get; }
        public MissionBoard Challenges { get; }
        public DailySystem Daily { get; }
        public PlayerLevelSystem Level { get; }
        public AlbumSystem Album { get; }
        public AchievementSystem Achievements { get; }
        public AttentionRules Attention { get; }

        public int MissionReplaceDayKey { get; private set; } = int.MinValue;
        public int MissionFreeReplacesUsed { get; private set; }
        public int ChallengeDayKey { get; private set; } = int.MinValue;
        public bool ChallengeBonusClaimed { get; private set; }
        public double OfflineCapExtensionRemainingSeconds { get; private set; }
        public bool NotificationsEnabled { get; set; } = true;
        public bool NotificationPermissionAsked { get; private set; }
        /// <summary>La rue (et donc les clients spéciaux) est présente : faux en simulation sans clients spéciaux.</summary>
        public bool SpecialsAvailable { get; set; } = true;

        /// <summary>Un coffre vient d'être ouvert et attend sa révélation à l'écran.</summary>
        public event Action<ChestResult> ChestQueued;
        public event Action<Feature> FeatureIntroduced;
        public event Action<int, int> PlayerLeveledUp;
        public event Action<RecipeDefinition> RecipeUnlocked;
        public event Action<int, int> AchievementReached;
        public event Action MissionsChanged;
        /// <summary>Le premier coffre gratuit est réclamé : bon moment pour demander la permission des notifications.</summary>
        public event Action NotificationPermissionWanted;

        public MetaSession(GameSession session, MetaDefinitions defs, GameClock clock, ulong seed)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _defs = defs ?? throw new ArgumentNullException(nameof(defs));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            defs.Validate(session.Definitions);
            Ledger.SetTimeSource(() => _clock.TrustedSeconds);
            Managers = new ManagerCollection(defs, Ledger);
            Chests = new ChestSystem(defs, Ledger, Managers, new DeterministicRandom(DeterministicRandom.Derive(seed, 1)), IsCityOpen);
            Missions = new MissionBoard(defs.Missions, defs.MissionRules.ActiveSlots, new DeterministicRandom(DeterministicRandom.Derive(seed, 2)), false);
            Challenges = new MissionBoard(defs.Challenges, defs.MissionRules.ChallengeSlots, new DeterministicRandom(DeterministicRandom.Derive(seed, 3)), true);
            Daily = new DailySystem(defs, new DeterministicRandom(DeterministicRandom.Derive(seed, 4)));
            Level = new PlayerLevelSystem(defs.PlayerLevels);
            Album = new AlbumSystem(defs, session.Definitions);
            Achievements = new AchievementSystem(defs.Achievements, Stats);
            Attention = new AttentionRules(defs.MinSecondsBetweenReminders);
            if (defs.Cauris.StartingCauris > 0) Ledger.Credit(Currency.Cauris, defs.Cauris.StartingCauris, "starting");

            Managers.Acquired += OnManagerAcquired;
            Managers.LeveledUp += (i, level) => { Stats.Add(StatId.ManagerLevels, 1); Level.Add(XpSource.ManagerLevel); };
            Managers.BoostStarted += i => { Stats.Add(StatId.ManagerBoosts, 1); Level.Add(XpSource.ManagerBoost); };
            Managers.PassivesChanged += () => _passivesDirty = true;
            Chests.ChestOpened += OnChestOpened;
            Level.LeveledUp += OnPlayerLeveledUp;
            Level.FeatureUnlocked += f => { if (_introduced.Add((int)f)) FeatureIntroduced?.Invoke(f); };
            Album.RecipeUnlocked += r => { Stats.Add(StatId.Recipes, 1); Level.Add(XpSource.Recipe); RecipeUnlocked?.Invoke(r); };
            Album.CityCompleted += _ => _passivesDirty = true;
            Achievements.TierReached += (i, t) => AchievementReached?.Invoke(i, t);
            Ledger.Moved += e => { if (e.Currency == Currency.Cauris && e.Amount > 0) Stats.Add(StatId.CaurisEarned, e.Amount); };
            Stats.ManagerLevelOf = id => Managers.BestLevelFor(id);
            Stats.FirstServed += i => Album.OnBusinessProgress(_bound[i].Definition.Id, _bound[i].Level);

            session.CityChanged += Bind;
            session.Prestiged += OnPrestiged;
            session.OfflineClaimed += (amount, seconds) => Stats.Add(StatId.OfflineClaims, 1);
            Bind(session.Businesses);
            _lastTrusted = _clock.TrustedSeconds;
        }

        public bool IsUnlocked(Feature feature) => Level.IsUnlocked(feature);

        /// <summary>Client spécial servi (tap dans la rue).</summary>
        public void OnSpecialTapped()
        {
            Stats.Add(StatId.SpecialsTapped, 1);
            Level.Add(XpSource.Special);
        }

        /// <summary>Pub récompensée vue jusqu'au bout (appelé sur le callback de récompense uniquement).</summary>
        public void OnRewardedWatched() => Stats.Add(StatId.RewardedWatched, 1);
        public long Cauris => Ledger.Balance(Currency.Cauris);
        public long Tokens => Ledger.Balance(Currency.Tokens);
        public int DayKey => _clock.LocalDayKey;
        public bool TimeTrusted => !_clock.IsRolledBack;

        private bool IsCityOpen(string cityId)
        {
            if (cityId == Session.City.Id) return true;
            foreach (var v in Session.VisitedCities) if (v.CityId == cityId) return true;
            return false;
        }

        // ================================================================== Câblage
        private void Bind(BusinessSystem businesses)
        {
            if (_bound != null)
            {
                _bound.Served -= OnServed;
                _bound.LevelChanged -= OnLevelChanged;
                _bound.Unlocked -= OnUnlocked;
                _bound.ManagerHired -= OnManagerHired;
                _bound.MilestoneReached -= OnMilestone;
                _bound.GlobalMilestoneReached -= OnGlobalMilestone;
                _bound.UpgradeBought -= OnUpgradeBought;
                _bound.Tapped -= OnTapped;
            }
            _bound = businesses;
            businesses.Served += OnServed;
            businesses.LevelChanged += OnLevelChanged;
            businesses.Unlocked += OnUnlocked;
            businesses.ManagerHired += OnManagerHired;
            businesses.MilestoneReached += OnMilestone;
            businesses.GlobalMilestoneReached += OnGlobalMilestone;
            businesses.UpgradeBought += OnUpgradeBought;
            businesses.Tapped += OnTapped;
            Stats.BindCity(businesses);
            // Commerces déjà gérés (sauvegarde M2) : leurs gérants de base entrent dans la collection.
            for (int i = 0; i < businesses.Count; i++)
            {
                var s = businesses[i];
                if (s.HasManager) Managers.GrantBase(s.Definition.Id);
                Stats.OnLevel(i, s.Level, s.MilestonesReached);
            }
            _passivesDirty = true;
            if (IsUnlocked(Feature.Missions)) { Missions.Revalidate(this); Missions.Fill(this); }
            if (IsUnlocked(Feature.DailyChallenges)) Challenges.Revalidate(this);
            MissionsChanged?.Invoke();
        }

        private void OnServed(int index, long customers) => Stats.OnServed(index, customers);

        private void OnLevelChanged(int index, long level)
        {
            var s = _bound[index];
            Stats.OnLevel(index, level, s.MilestonesReached);
            Stats.Add(StatId.LevelsBought, 1);
            Level.Add(XpSource.BuyLevels);
            if (Stats.Current(index).FirstServed) Album.OnBusinessProgress(s.Definition.Id, level);
        }

        private void OnUnlocked(int index) { Stats.Add(StatId.BusinessesUnlocked, 1); Level.Add(XpSource.UnlockBusiness); }

        private void OnManagerHired(int index)
        {
            Stats.Add(StatId.ManagersHired, 1);
            Level.Add(XpSource.HireManager);
            Managers.GrantBase(_bound[index].Definition.Id);
        }

        private void OnMilestone(int index, int milestones)
        {
            Stats.OnLevel(index, _bound[index].Level, milestones);
            Stats.Add(StatId.Milestones, 1);
            Level.Add(XpSource.Milestone);
        }

        private void OnGlobalMilestone(int count) => Stats.Add(StatId.GlobalMilestones, 1);
        private void OnUpgradeBought(int index) { Stats.Add(StatId.CashUpgrades, 1); Level.Add(XpSource.CashUpgrade); }
        private void OnTapped(int index) => Stats.Add(StatId.Taps, 1);

        private void OnPrestiged(long stars, bool moved)
        {
            Stats.Add(StatId.Prestiges, 1);
            Stats.Max(StatId.Stars, Session.Prestige.TotalStars);
            Level.Add(moved ? XpSource.NewCity : XpSource.Prestige);
            if (moved) Stats.Set(StatId.CitiesVisited, Session.VisitedCities.Count + 1);
            Stats.BindCity(Session.Businesses);
            _passivesDirty = true;
            if (IsUnlocked(Feature.Missions)) { Missions.Revalidate(this); Missions.Fill(this); }
            MissionsChanged?.Invoke();
        }

        private void OnManagerAcquired(int index)
        {
            Stats.Set(StatId.ManagersOwned, Managers.OwnedCount);
            Stats.Set(StatId.RareOwned, Managers.OwnedOfRarity(Rarity.Rare));
            Stats.Set(StatId.EpicOwned, Managers.OwnedOfRarity(Rarity.Epic));
            Stats.Set(StatId.LegendaryOwned, Managers.OwnedOfRarity(Rarity.Legendary));
        }

        private void OnChestOpened(ChestResult result)
        {
            Stats.Add(StatId.ChestsOpened, 1);
            Level.Add(XpSource.Chest);
            if (result.Boost) AddChestBoost();
            _reveals.Enqueue(result);
            ChestQueued?.Invoke(result);
        }

        private void OnPlayerLeveledUp(int before, int after)
        {
            Stats.Max(StatId.PlayerLevel, after);
            int pending = Level.TakePendingRewardLevels();
            for (int l = after - pending + 1; l <= after; l++)
            {
                Ledger.Credit(Currency.Cauris, Level.Config.CaurisPerLevel, "player_level");
                if (Level.LevelGivesChest(l)) Chests.Open(Level.Config.ChestOnLevel, "player_level");
            }
            if (IsUnlocked(Feature.Missions)) Missions.Fill(this);
            if (IsUnlocked(Feature.DailyChallenges) && ChallengeDayKey == int.MinValue) RollChallenges(true);
            PlayerLeveledUp?.Invoke(before, after);
            MissionsChanged?.Invoke();
        }

        private void AddChestBoost()
        {
            var r = _defs.ChestRules;
            Session.Economy.AddTimedBoost(r.BoostMultiplier, r.BoostSeconds, r.BoostMaxStackSeconds);
        }

        // ================================================================== Boucle
        /// <summary>
        /// Temps de jeu actif écoulé (après <see cref="GameSession.Tick"/>). Le temps de confiance (coffres gratuits,
        /// extension hors ligne) est lu sur l'horloge, qui inclut l'absence validée.
        /// </summary>
        public void Tick(double deltaSeconds)
        {
            Managers.Tick(deltaSeconds);
            double trusted = _clock.TrustedSeconds;
            double trustedDelta = Math.Max(0d, trusted - _lastTrusted);
            _lastTrusted = trusted;
            Chests.Tick(trustedDelta);
            if (OfflineCapExtensionRemainingSeconds > 0d)
            {
                OfflineCapExtensionRemainingSeconds = Math.Max(0d, OfflineCapExtensionRemainingSeconds - trustedDelta);
                if (OfflineCapExtensionRemainingSeconds <= 0d) Session.ExtraOfflineCapSeconds = 0d;
            }
            if (_passivesDirty) ApplyPassives();
            ApplyBoosts();
            Stats.Set(StatId.LifetimeEarnings, Session.Economy.TotalLifetimeEarnings.ToDouble());
            if (IsUnlocked(Feature.DailyChallenges)) RollChallenges(false);
            if (IsUnlocked(Feature.Missions) && Missions.Fill(this, trusted)) MissionsChanged?.Invoke();
            Achievements.Scan(4);
            _attentionTimer -= deltaSeconds;
            if (_attentionTimer <= 0d)
            {
                _attentionTimer = 0.5d;
                UpdateAttention();
            }
        }

        /// <summary>Passifs des gérants possédés et albums complets : toujours actifs, y compris hors ligne.</summary>
        private void ApplyPassives()
        {
            _passivesDirty = false;
            var b = Session.Businesses;
            for (int i = 0; i < b.Count; i++) b.SetMetaRevenueMultiplier(i, Managers.BusinessMultiplier(b[i].Definition.Id));
            Session.Economy.MetaMultiplier = Managers.GlobalMultiplier * Album.Multiplier;
        }

        /// <summary>Boosts actifs (choix du joueur, jamais hors ligne).</summary>
        private void ApplyBoosts()
        {
            var b = Session.Businesses;
            for (int i = 0; i < b.Count; i++) b.SetBoostMultiplier(i, Managers.BoostMultiplier(b[i].Definition.Id));
        }

        private void RollChallenges(bool force)
        {
            int day = DayKey;
            if (!force && (day <= ChallengeDayKey || !TimeTrusted)) return; // horloge qui recule : on garde les défis
            ChallengeDayKey = day;
            ChallengeBonusClaimed = false;
            Challenges.Reset(this);
            MissionsChanged?.Invoke();
        }

        // ================================================================== Récompenses
        /// <summary>Crédite une récompense composite. Les coffres sont ouverts tout de suite et mis en file de révélation.</summary>
        public void Grant(RewardBundle reward, string reason, double multiplier = 1d)
        {
            if (reward == null) return;
            if (reward.Cauris > 0) Ledger.Credit(Currency.Cauris, (long)Math.Round(reward.Cauris * multiplier), reason);
            if (reward.Tokens > 0) Ledger.Credit(Currency.Tokens, (long)Math.Round(reward.Tokens * multiplier), reason);
            if (!string.IsNullOrEmpty(reward.ChestId)) Chests.Open(reward.ChestId, reason);
            if (reward.Boost) AddChestBoost();
            if (reward.Cards > 0) GrantCards(reward.Cards, reason);
        }

        /// <summary>Cartes de doublon pour les gérants possédés pas encore au max (sinon cauris de conversion).</summary>
        private void GrantCards(int cards, string reason)
        {
            _cardCandidates.Clear();
            for (int i = 0; i < Managers.Count; i++)
                if (Managers.Owns(i) && !Managers.IsMaxed(i)) _cardCandidates.Add(i);
            if (_cardCandidates.Count == 0)
            {
                Ledger.Credit(Currency.Cauris, cards * (long)_defs.ManagerLevels.DuplicateCaurisAtMax[0], reason);
                return;
            }
            int pick = _cardCandidates[Chests.Random.Next(_cardCandidates.Count)];
            Managers.AddCards(pick, cards, reason);
        }

        public bool HasPendingReveal => _reveals.Count > 0;
        public ChestResult NextReveal() => _reveals.Count > 0 ? _reveals.Dequeue() : null;

        // ================================================================== Missions
        public bool CanClaimMission(int slot)
        {
            var m = Missions[slot];
            return IsUnlocked(Feature.Missions) && m != null && !m.Claimed && m.IsComplete(this);
        }

        /// <summary>Réclame une mission ; <paramref name="doubled"/> = rewarded « Doubler » vue (callback de récompense).</summary>
        public bool ClaimMission(int slot, bool doubled = false)
        {
            if (!CanClaimMission(slot)) return false;
            var m = Missions[slot];
            m.Claimed = true;
            double mult = doubled ? 2d : 1d;
            if (m.Cauris > 0) Ledger.Credit(Currency.Cauris, (long)Math.Round(m.Cauris * mult), "mission");
            if (m.Tokens > 0) Ledger.Credit(Currency.Tokens, (long)Math.Round(m.Tokens * mult), "mission");
            if (!string.IsNullOrEmpty(m.ChestId)) Chests.Open(m.ChestId, "mission");
            Stats.Add(StatId.MissionsCompleted, 1);
            Level.AddRaw((long)Math.Round(m.Xp * mult));
            Level.Add(XpSource.Mission);
            Missions.Clear(slot, _clock.TrustedSeconds + _defs.MissionRules.RefillDelaySeconds);
            MissionsChanged?.Invoke();
            return true;
        }

        public bool HasFreeReplace => MissionReplaceDayKey != DayKey || MissionFreeReplacesUsed < _defs.MissionRules.FreeReplacesPerDay;

        /// <summary>Remplace une mission : gratuit 1×/jour, puis rewarded (<paramref name="viaRewarded"/>) ou cauris.</summary>
        public bool ReplaceMission(int slot, bool viaRewarded)
        {
            if (!IsUnlocked(Feature.Missions) || Missions[slot] == null || Missions[slot].Claimed) return false;
            int day = DayKey;
            if (MissionReplaceDayKey != day && day > MissionReplaceDayKey) { MissionReplaceDayKey = day; MissionFreeReplacesUsed = 0; }
            bool free = MissionFreeReplacesUsed < _defs.MissionRules.FreeReplacesPerDay && MissionReplaceDayKey == day;
            if (!free && !viaRewarded && !Ledger.CanAfford(Currency.Cauris, _defs.MissionRules.ReplaceCauris)) return false;
            // Rien n'est consommé si aucune autre mission n'est possible.
            if (!Missions.Replace(this, slot)) return false;
            if (free) MissionFreeReplacesUsed++;
            else if (!viaRewarded) Ledger.TryDebit(Currency.Cauris, _defs.MissionRules.ReplaceCauris, "mission_replace");
            MissionsChanged?.Invoke();
            return true;
        }

        // ================================================================== Défis du jour
        public bool CanClaimChallenge(int slot)
        {
            var m = Challenges[slot];
            return IsUnlocked(Feature.DailyChallenges) && m != null && !m.Claimed && m.IsComplete(this);
        }

        public bool ClaimChallenge(int slot)
        {
            if (!CanClaimChallenge(slot)) return false;
            var m = Challenges[slot];
            m.Claimed = true;
            if (m.Tokens > 0) Ledger.Credit(Currency.Tokens, m.Tokens, "challenge");
            if (m.Cauris > 0) Ledger.Credit(Currency.Cauris, m.Cauris, "challenge");
            if (!string.IsNullOrEmpty(m.ChestId)) Chests.Open(m.ChestId, "challenge");
            Stats.Add(StatId.ChallengesCompleted, 1);
            Level.AddRaw(m.Xp);
            Level.Add(XpSource.Challenge);
            if (!ChallengeBonusClaimed && AllChallengesClaimed)
            {
                ChallengeBonusClaimed = true;
                Chests.Open(_defs.MissionRules.ChallengeBonusChestId, "challenge_bonus");
            }
            MissionsChanged?.Invoke();
            return true;
        }

        public bool AllChallengesClaimed
        {
            get
            {
                bool any = false;
                for (int s = 0; s < Challenges.Slots; s++)
                {
                    var m = Challenges[s];
                    if (m == null) continue;
                    any = true;
                    if (!m.Claimed) return false;
                }
                return any;
            }
        }

        // ================================================================== Connexion, roue
        public bool CanClaimLogin => IsUnlocked(Feature.Login) && TimeTrusted && Daily.CanClaimLogin(DayKey, _clock.TrustedSeconds);

        public RewardBundle ClaimLogin()
        {
            if (!CanClaimLogin) return null;
            var reward = Daily.ClaimLogin(DayKey, _clock.TrustedSeconds, _clock.SecondsToLocalMidnight);
            if (reward == null) return null;
            Grant(reward, "login");
            Stats.Add(StatId.LoginDays, 1);
            Stats.Max(StatId.BestStreak, Daily.BestStreak);
            Level.Add(XpSource.Login);
            return reward;
        }

        /// <summary>Bouclier de série via rewarded (callback de récompense).</summary>
        public bool EarnStreakShield() => TimeTrusted && Daily.EarnShield(_clock.LocalWeekKey);

        public bool CanSpinFree => IsUnlocked(Feature.Wheel) && TimeTrusted && Daily.HasFreeSpin(DayKey);
        public bool CanSpinRewarded => IsUnlocked(Feature.Wheel) && TimeTrusted && Daily.HasRewardedSpin(DayKey);

        /// <summary>Tour de roue ; renvoie l'index du segment (−1 si refusé). Récompense créditée immédiatement.</summary>
        public int SpinWheel(bool rewarded)
        {
            if (rewarded ? !CanSpinRewarded : !CanSpinFree) return -1;
            int index = Daily.Spin(DayKey, rewarded);
            if (index < 0) return -1;
            Grant(_defs.Wheel.Segments[index].Reward, "wheel");
            Stats.Add(StatId.WheelSpins, 1);
            Level.Add(XpSource.Wheel);
            return index;
        }

        // ================================================================== Coffres
        public bool CanClaimFreeChest => IsUnlocked(Feature.Chests) && Chests.FreeStored > 0;

        public ChestResult ClaimFreeChest()
        {
            if (!CanClaimFreeChest) return null;
            bool first = !Chests.FirstFreeClaimed;
            var result = Chests.ClaimFree();
            if (first && _defs.Notifications.AskPermissionAfterFirstFreeChest && !NotificationPermissionAsked)
            {
                NotificationPermissionAsked = true;
                NotificationPermissionWanted?.Invoke();
            }
            return result;
        }

        public bool CanClaimRewardedChest => IsUnlocked(Feature.Chests) && TimeTrusted && Chests.CanClaimRewarded(DayKey);
        public ChestResult ClaimRewardedChest() => CanClaimRewardedChest ? Chests.ClaimRewarded(DayKey) : null;
        public ChestResult BuyChest(string chestId) => IsUnlocked(Feature.Shop) ? Chests.Buy(chestId) : null;

        // ================================================================== Succès
        public long ClaimAchievement(int index)
        {
            if (!IsUnlocked(Feature.Achievements)) return 0;
            long cauris = Achievements.Claim(index);
            if (cauris <= 0) return 0;
            Ledger.Credit(Currency.Cauris, cauris, "achievement");
            Level.Add(XpSource.Achievement);
            return cauris;
        }

        // ================================================================== Gérants
        /// <summary>Boost du gérant affecté à ce commerce (bouton dans la rue ou la fiche).</summary>
        public bool ActivateManagerBoost(string businessId) => Managers.ActivateBoost(businessId);

        /// <summary>Recharge accélérée : cauris, ou rewarded (<paramref name="viaRewarded"/>, callback de récompense).</summary>
        public bool AccelerateBoost(int managerIndex, bool viaRewarded)
        {
            if (Managers.IsBoostActive(managerIndex) || Managers.CooldownRemaining(managerIndex) <= 0d) return false;
            if (!viaRewarded && !Ledger.TryDebit(Currency.Cauris, _defs.ManagerLevels.AccelerateBoostCauris, "boost_accelerate")) return false;
            return Managers.AccelerateCooldown(managerIndex);
        }

        public bool CanLevelUpManager(int index) => Managers.CanLevelUp(index, Session.Economy, IncomeForCosts);
        public BigDouble ManagerLevelCost(int index) => Managers.FcfaToNext(index, IncomeForCosts);
        public bool LevelUpManager(int index) => Managers.LevelUp(index, Session.Economy, IncomeForCosts);

        /// <summary>Revenu de référence des coûts en FCFA : revenu géré sans boosts temporaires (stable, non exploitable).</summary>
        private BigDouble IncomeForCosts
        {
            get
            {
                var managed = Session.Businesses.ManagedIncomePerSecond();
                return managed / Math.Max(1d, Session.Economy.TemporaryMultiplier);
            }
        }

        // ================================================================== Hors ligne
        public bool CanExtendOfflineCap => OfflineCapExtensionRemainingSeconds <= 0d && Ledger.CanAfford(Currency.Cauris, _defs.Cauris.OfflineCapExtensionCauris);

        /// <summary>Puits de cauris : plafond hors ligne +N h pendant 24 h.</summary>
        public bool ExtendOfflineCap()
        {
            if (!CanExtendOfflineCap || !Ledger.TryDebit(Currency.Cauris, _defs.Cauris.OfflineCapExtensionCauris, "offline_cap")) return false;
            OfflineCapExtensionRemainingSeconds = _defs.Cauris.OfflineCapExtensionDurationSeconds;
            Session.ExtraOfflineCapSeconds = _defs.Cauris.OfflineCapExtensionSeconds;
            return true;
        }

        // ================================================================== Points rouges
        public bool IsUpgradeSeen(string id) => _seenUpgrades.Contains(id);
        public void MarkUpgradeSeen(string id) { if (!string.IsNullOrEmpty(id)) _seenUpgrades.Add(id); }

        /// <summary>Upgrades FCFA abordables jamais vus (badge « nouveau »).</summary>
        public int NewAffordableUpgrades
        {
            get
            {
                var b = Session.Businesses;
                int n = 0;
                for (int u = 0; u < b.UpgradeCount; u++)
                    if (b.IsUpgradeAvailable(u) && !_seenUpgrades.Contains(b.Upgrade(u).Id) && Session.Economy.CanAfford(b.UpgradeCost(u))) n++;
                return n;
            }
        }

        public bool AnyMissionClaimable
        {
            get { for (int s = 0; s < Missions.Slots; s++) if (CanClaimMission(s)) return true; return false; }
        }

        public bool AnyChallengeClaimable
        {
            get { for (int s = 0; s < Challenges.Slots; s++) if (CanClaimChallenge(s)) return true; return false; }
        }

        public bool AnyManagerActionable
        {
            get
            {
                if (!IsUnlocked(Feature.Chests)) return false;
                for (int i = 0; i < Managers.Count; i++) if (CanLevelUpManager(i)) return true;
                return false;
            }
        }

        private void UpdateAttention()
        {
            double now = _clock.TrustedSeconds;
            Attention.Set(AttentionSlot.Missions, AnyMissionClaimable, now);
            Attention.Set(AttentionSlot.Challenges, AnyChallengeClaimable, now);
            Attention.Set(AttentionSlot.Login, CanClaimLogin, now);
            Attention.Set(AttentionSlot.Wheel, CanSpinFree, now);
            Attention.Set(AttentionSlot.Shop, CanClaimFreeChest, now);
            Attention.Set(AttentionSlot.Album, IsUnlocked(Feature.Album) && Album.UnseenCount > 0, now);
            Attention.Set(AttentionSlot.Achievements, IsUnlocked(Feature.Achievements) && Achievements.ClaimableCount > 0, now);
            Attention.Set(AttentionSlot.Managers, AnyManagerActionable, now);
            Attention.Set(AttentionSlot.Upgrades, NewAffordableUpgrades > 0, now);
            Attention.Set(AttentionSlot.Prestige, Session.IsCityGoalReached && Session.HasNextCity, now);
            Attention.Set(AttentionSlot.Cities, false, now);
        }

        // ================================================================== Notifications
        /// <summary>Candidats (type, délai en s) pour le planificateur, à la mise en arrière-plan.</summary>
        public void NotificationCandidates(List<(NotificationKind, double)> into)
        {
            into.Clear();
            if (IsUnlocked(Feature.Chests) && Chests.FreeStored < Chests.Rules.FreeMaxStored) into.Add((NotificationKind.FreeChestReady, Chests.SecondsToNextFree));
            into.Add((NotificationKind.OfflineFull, Session.OfflineCapSeconds));
            if (IsUnlocked(Feature.Login))
            {
                double toMidnight = _clock.SecondsToLocalMidnight;
                if (Daily.Streak >= 2) into.Add((NotificationKind.StreakInDanger, toMidnight + 18d * 3600d));
                into.Add((NotificationKind.DailyReady, toMidnight + 9d * 3600d));
            }
        }

        // ================================================================== Sauvegarde
        public MetaSaveData Save()
        {
            Achievements.Save(out var achievementIds, out var achievementClaimed);
            var data = new MetaSaveData
            {
                LastTrustedUtcTicks = _clock.LastTrustedUtc.Ticks,
                TrustedSeconds = _clock.TrustedSeconds,
                Ledger = Ledger.Save(),
                Stats = Stats.Save(),
                Managers = Managers.Save(),
                Chests = Chests.Save(),
                Daily = Daily.Save(),
                Missions = Missions.Save(),
                MissionRefillAt = Missions.SaveRefill(),
                MissionRandomState = Missions.Random.State.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MissionReplaceDayKey = MissionReplaceDayKey,
                MissionFreeReplacesUsed = MissionFreeReplacesUsed,
                Challenges = Challenges.Save(),
                ChallengeRandomState = Challenges.Random.State.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ChallengeDayKey = ChallengeDayKey,
                ChallengeBonusClaimed = ChallengeBonusClaimed,
                Xp = Level.Xp,
                RewardedLevel = Level.RewardedLevel,
                Recipes = Album.SaveUnlocked(),
                RecipesSeen = Album.SaveSeen(),
                AchievementIds = achievementIds,
                AchievementClaimed = achievementClaimed,
                SeenUpgrades = new string[_seenUpgrades.Count],
                IntroducedFeatures = new int[_introduced.Count],
                OfflineCapExtensionRemainingSeconds = OfflineCapExtensionRemainingSeconds,
                NotificationsEnabled = NotificationsEnabled,
                NotificationPermissionAsked = NotificationPermissionAsked,
            };
            _seenUpgrades.CopyTo(data.SeenUpgrades);
            _introduced.CopyTo(data.IntroducedFeatures);
            return data;
        }

        /// <summary>Charge l'état méta (null = sauvegarde M2 migrée : la collection est reconstruite depuis la session).</summary>
        public void Load(MetaSaveData data)
        {
            if (data != null)
            {
                if (data.LastTrustedUtcTicks > 0) _clock.Restore(new DateTime(data.LastTrustedUtcTicks, DateTimeKind.Utc), data.TrustedSeconds);
                Ledger.Restore(data.Ledger);
                Stats.Restore(data.Stats);
                Managers.Restore(data.Managers);
                Chests.Restore(data.Chests);
                Daily.Restore(data.Daily);
                Missions.Restore(data.Missions);
                Missions.RestoreRefill(data.MissionRefillAt);
                RestoreRandom(Missions.Random, data.MissionRandomState);
                MissionReplaceDayKey = data.MissionReplaceDayKey;
                MissionFreeReplacesUsed = Math.Max(0, data.MissionFreeReplacesUsed);
                Challenges.Restore(data.Challenges);
                RestoreRandom(Challenges.Random, data.ChallengeRandomState);
                ChallengeDayKey = data.ChallengeDayKey;
                ChallengeBonusClaimed = data.ChallengeBonusClaimed;
                Level.Restore(data.Xp, data.RewardedLevel);
                Album.Restore(data.Recipes, data.RecipesSeen);
                Achievements.Restore(data.AchievementIds, data.AchievementClaimed);
                _seenUpgrades.Clear();
                if (data.SeenUpgrades != null) foreach (var id in data.SeenUpgrades) if (!string.IsNullOrEmpty(id)) _seenUpgrades.Add(id);
                _introduced.Clear();
                if (data.IntroducedFeatures != null) foreach (int f in data.IntroducedFeatures) _introduced.Add(f);
                OfflineCapExtensionRemainingSeconds = Math.Max(0d, Math.Min(_defs.Cauris.OfflineCapExtensionDurationSeconds, data.OfflineCapExtensionRemainingSeconds));
                NotificationsEnabled = data.NotificationsEnabled;
                NotificationPermissionAsked = data.NotificationPermissionAsked;
            }
            else
            {
                // Migration M2 → M3 : les statistiques repartent de l'état visible, rien n'est rétroactivement crédité.
                Stats.Restore(null);
                Achievements.Restore(null, null);
            }
            Session.ExtraOfflineCapSeconds = OfflineCapExtensionRemainingSeconds > 0d ? _defs.Cauris.OfflineCapExtensionSeconds : 0d;
            Stats.Set(StatId.PlayerLevel, Level.Level);
            Stats.Max(StatId.Stars, Session.Prestige.TotalStars);
            Stats.Max(StatId.CitiesVisited, Session.VisitedCities.Count + 1);
            Bind(Session.Businesses);
            for (int i = 0; i < _bound.Count; i++)
                if (Stats.Current(i).FirstServed) Album.OnBusinessProgress(_bound[i].Definition.Id, _bound[i].Level);
            OnManagerAcquired(-1);
            Achievements.Restore(data?.AchievementIds, data?.AchievementClaimed);
            _lastTrusted = _clock.TrustedSeconds;
            _passivesDirty = true;
            ApplyPassives();
        }

        private static void RestoreRandom(DeterministicRandom random, string state)
        {
            if (ulong.TryParse(state, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong s))
                random.State = s;
        }
    }
}
