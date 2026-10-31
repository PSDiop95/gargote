using System;
using Gargote.Core;
using Gargote.Data;
using System.Collections.Generic;
using Gargote.Services.Ads;
using Gargote.Services.Notifications;
using Gargote.Services.Save;
using Gargote.Systems;
using Gargote.Systems.Meta;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Composition root de la scène Rue : crée les services, la session (source de vérité) et la présentation,
    /// puis cadence la boucle : économie dans Update, présentation dans LateUpdate (après la caméra).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("Économie (M1)")]
        public GameDatabase database;
        public SpecialCustomersConfigAsset specialCustomers;

        [Header("Réglages visuels")]
        public CameraConfig cameraConfig;
        public StreetConfig streetConfig;
        public CrowdConfig crowdConfig;
        public JuiceConfig juiceConfig;
        public DayNightVisualConfig dayNightConfig;
        public AudioConfig audioConfig;
        public QualityConfig qualityConfig;
        public HudConfig hudConfig;
        public StreetAssets assets;

        [Header("Scène")]
        public Camera mainCamera;
        public Light sun;

        [Tooltip("Sauvegarde automatique (secondes réelles).")]
        public float autosaveSeconds = 30f;

        /// <summary>Tests PlayMode : remplace la sauvegarde disque (null = fichier).</summary>
        internal static Func<ISaveService> SaveServiceOverride;
        internal static bool SkipOfflinePanel;
        internal static Func<INotificationService> NotificationServiceOverride;

        public GameSession Session { get; private set; }
        /// <summary>Méta M3 (collection, coffres, missions, quotidien, succès, niveau).</summary>
        public MetaSession Meta { get; private set; }
        public GameClock Clock => _gameClock;
        public IAdsService Ads => _ads;
        public INotificationService Notifications => _notifications;
        public SpecialCustomerSystem Specials { get; private set; }
        public GameTime GameTime { get; private set; }
        public StreetPresenter Presenter { get; private set; }
        public CameraRig Rig { get; private set; }
        public MoneyFx Money { get; private set; }
        public EffectsFx Effects { get; private set; }
        public BusinessSheet Sheet { get; private set; }
        public HudView Hud { get; private set; }
        public MetaUi MetaUi { get; private set; }
        public UiLayout Layout { get; private set; }
        public OfflinePanel Offline { get; private set; }
        public PersonRenderer Persons { get; private set; }
        public AudioDirector Audio { get; private set; }
        public QualityDirector Quality { get; private set; }
        public JuiceConfig Juice => _juice;
        public bool Ready { get; private set; }

        private IGameClock _clock;
        private GameClock _gameClock;
        private TimeSpan _utcOffset;
        private float _offsetTimer;
        private INotificationService _notifications;
        private readonly List<(NotificationKind, double)> _notificationCandidates = new List<(NotificationKind, double)>(8);
        private readonly List<PlannedNotification> _plannedNotifications = new List<PlannedNotification>(8);
        private ISaveService _save;
        private DeviceSettings _deviceSettings;
        private MockAdsService _ads;
        private JuiceConfig _juice;
        private CrowdRules _crowdRules;
        private MoneyFlowRules _moneyRules;
        private DayNightDirector _dayNight;
        private WorldUi _worldUi;
        private DebugPanel _debug;
        private InputRouter _input;
        private float _autosave;
        private double _pendingOfflineSeconds = -1d;
        private int _forcedHourIndex, _specialIndex, _speedIndex;
        private static readonly double[] ForcedHours = { -1d, 8d, 13d, 21d, 2d };
        private static readonly float[] Speeds = { 1f, 10f, 100f };

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || GARGOTE_PROBE
            StartupProbe.Mark("awake");
#endif
            _deviceSettings = DeviceSettings.Load(); // PlayerPrefs : jamais dans un initialiseur de champ
            // Copies runtime : les bascules debug (effets réduits, qualité) ne modifient jamais les assets du projet.
            _juice = Instantiate(juiceConfig);
            var quality = Instantiate(qualityConfig);
            Quality = new QualityDirector(quality);
            var tier = Quality.Apply(Quality.Detect());

            // Pool de tweens préalloué : aucune croissance (donc aucune allocation) pendant les rafales de juice.
            PrimeTween.PrimeTweenConfig.SetTweensCapacity(_juice.tweenCapacity);
            Loc.Initialize();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || GARGOTE_PROBE
            StartupProbe.Mark("loc");
#endif
            _clock = new UnityGameClock();
            // Fuseau mis en cache (relu chaque minute et au retour au premier plan) : aucune allocation par frame.
            _utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            _gameClock = new GameClock(_clock, () => _utcOffset);
            var definitions = database.ToDefinitions();
            Session = new GameSession(definitions);
            GameTime = new GameTime(_clock, definitions.Economy.DayNight);
            _ads = new MockAdsService();
            _notifications = NotificationServiceOverride != null ? NotificationServiceOverride() : new MockNotificationService();
            _save = SaveServiceOverride != null ? SaveServiceOverride() : new FileSaveService(Application.persistentDataPath);
            LoadGame(database.ToMetaDefinitions(definitions));
#if UNITY_EDITOR || DEVELOPMENT_BUILD || GARGOTE_PROBE
            StartupProbe.Mark("data");
#endif

            var specialRules = specialCustomers.ToRules();
            Specials = new SpecialCustomerSystem(specialRules, Session.Economy, () => Session.Businesses, Environment.TickCount);
            Specials.Rewarded += (i, amount) => Meta.OnSpecialTapped();
            _crowdRules = crowdConfig.ToRules();
            _crowdRules.MaxTotal = Mathf.Min(_crowdRules.MaxTotal, crowdConfig.maxTotal);
            _moneyRules = _juice.ToMoneyRules();

            BuildScene(specialRules, tier);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || GARGOTE_PROBE
            StartupProbe.Mark("scene");
#endif
            Ready = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || GARGOTE_PROBE
            StartupProbe.Attach(gameObject);
#endif
        }

        private void LoadGame(MetaDefinitions metaDefinitions)
        {
            var status = _save.TryLoad(out var data);
            bool loaded = (status == SaveLoadStatus.Loaded || status == SaveLoadStatus.LoadedFromBackup) && data != null;
            if (loaded)
            {
                Session.LoadFrom(data);
                var savedUtc = new DateTime(data.SavedUtcTicks, DateTimeKind.Utc);
                _pendingOfflineSeconds = OfflineCalculator.ResolveElapsedSeconds(savedUtc, _clock.UtcNow,
                    data.SavedMonotonicSeconds, _clock.MonotonicSeconds);
            }
            // Méta après la session (elle se branche sur les commerces chargés). Sauvegarde M2 : Meta == null.
            Meta = new MetaSession(Session, metaDefinitions, _gameClock, (ulong)DateTime.UtcNow.Ticks);
            Meta.Load(loaded ? data.Meta : null);
            // Absence validée (0 si l'heure a reculé) : fait avancer les minuteries de confiance (coffre gratuit...).
            if (_pendingOfflineSeconds > 0d) _gameClock.AdvanceOffline(_pendingOfflineSeconds);
        }

        private void BuildScene(SpecialCustomerRules specialRules, QualityTierSettings tier)
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var world = new GameObject("World").transform;
            world.SetParent(transform, false);

            Rig = mainCamera.gameObject.GetComponent<CameraRig>();
            if (Rig == null) Rig = mainCamera.gameObject.AddComponent<CameraRig>();
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var slot in streetConfig.slots) { minZ = Mathf.Min(minZ, slot.position.z); maxZ = Mathf.Max(maxZ, slot.position.z); }
            Rig.Setup(cameraConfig, mainCamera, minZ, maxZ, minZ);

            var bounds = new Bounds(new Vector3(0f, 2f, (streetConfig.streetStartZ + streetConfig.streetEndZ) * 0.5f),
                new Vector3(60f, 20f, streetConfig.streetEndZ - streetConfig.streetStartZ + 60f));
            Persons = new PersonRenderer(assets.toon, 32, bounds);

            // Canvases : monde (labels/billets, dynamique), HUD (statique), modal.
            var worldCanvas = UiFactory.Canvas("WorldCanvas", 0, hudConfig, transform);
            var hudCanvas = UiFactory.Canvas("HudCanvas", 10, hudConfig, transform);
            var modalCanvas = UiFactory.Canvas("ModalCanvas", 20, hudConfig, transform);
            var hudSafe = UiFactory.Stretch(hudCanvas.transform, "SafeArea");
            hudSafe.gameObject.AddComponent<SafeArea>();
            var modalSafe = UiFactory.Stretch(modalCanvas.transform, "SafeArea");
            modalSafe.gameObject.AddComponent<SafeArea>();
            var worldRoot = (RectTransform)worldCanvas.transform;
            var labelsRoot = UiFactory.Stretch(worldRoot, "Labels");
            var fxRoot = UiFactory.Stretch(modalCanvas.transform, "MoneyFx");

            _worldUi = new WorldUi(labelsRoot, mainCamera, hudConfig, _juice, assets, Session.Businesses.Count);
            Hud = new HudView(Session, hudConfig, _juice, hudSafe, assets);
            Money = new MoneyFx(_juice, hudConfig, fxRoot, mainCamera, assets.bill, 48, 16);
            Money.SetTarget(() => (Vector2)fxRoot.InverseTransformPoint(Hud.MoneyIcon.position));
            Money.BillArrived = OnBillArrived;
            Effects = new EffectsFx(_juice, assets.particles, world, streetConfig.businessAccents);
            Effects.SetParticleScale(tier.particleScale);

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            Audio = audioGo.AddComponent<AudioDirector>();
            Audio.Setup(audioConfig);

            string[] specialNames = new string[specialCustomers.customers.Length];
            for (int i = 0; i < specialNames.Length; i++) specialNames[i] = Loc.Get(specialCustomers.customers[i].nameKey);

            Presenter = new StreetPresenter(Session, streetConfig, crowdConfig, _juice, _crowdRules, _moneyRules, assets, world,
                Persons, Specials, specialRules, specialNames, Rig, Money, Effects, Audio, _worldUi, Hud);
            Presenter.Crowd.SetMaxTotal(tier.maxCustomers);

            Sheet = new BusinessSheet(Session, hudConfig, hudSafe, assets, i => Loc.Get("business." + Session.Businesses[i].Definition.Id));
            Sheet.BuyClicked = OnBuyClicked;
            Sheet.ManagerClicked = i => Session.Businesses.HireManager(i);
            Sheet.BestClicked = OnBestClicked;
            Sheet.RowSelected = i => Rig.FocusOn(Presenter.Views[i].transform.position.z);
            Presenter.BusinessSelected = i => Sheet.Select(i, true);

            Offline = new OfflinePanel(modalSafe, hudConfig, assets);

            // Méta : HUD (niveau, cauris), rails, écrans. Zones réservées : rien d'accroché au monde ne passe dessous.
            Hud.Bind(Meta);
            var metaContext = new MetaUiContext
            {
                Meta = Meta, Hud = hudConfig, Assets = assets, Ads = _ads, Audio = Audio,
                BusinessName = id => Loc.Get("business." + id),
            };
            metaContext.RequestNotificationPermission = done => _notifications.RequestPermission(done);
            MetaUi = new MetaUi(hudSafe, modalSafe, metaContext, () => Sheet.Selected, _deviceSettings);
            Hud.SettingsClicked = MetaUi.Settings.Show;
            Hud.LevelClicked = MetaUi.ShowLevelInfo;
            _deviceSettings.Changed += ApplyDeviceSettings;
            ApplyDeviceSettings();
            MetaUi.BindSheet(Sheet);
            Hud.CaurisPlusClicked = () => MetaUi.OpenTab(4);
            Layout = new UiLayout(worldRoot);
            Layout.Register(UiZone.TopHud, Hud.Root);
            Layout.Register(UiZone.Toasts, MetaUi.Hud.Banner);
            Layout.Register(UiZone.LeftRail, MetaUi.Hud.LeftRail);
            Layout.Register(UiZone.RightRail, MetaUi.Hud.RightRail);
            Layout.Register(UiZone.BottomSheet, Sheet.Root);
            MetaUi.Layout = Layout;
            MetaUi.Freeze = new StreetFreeze(worldRoot, mainCamera, hudConfig.streetFreezeScale);
            _worldUi.Layout = Layout;
            Money.Layout = Layout;
            _dayNight = new DayNightDirector(dayNightConfig, sun, mainCamera);

            _input = gameObject.AddComponent<InputRouter>();
            _input.Setup(Rig, cameraConfig);
            _input.Tapped = screen => Presenter.HandleTap(screen);

            if (DebugPanel.Available) BuildDebug(hudSafe);
            _dayNight.Apply(GameTime.DayFraction);

            if (_pendingOfflineSeconds > 0d && !SkipOfflinePanel) ShowOffline(_pendingOfflineSeconds);
        }

        private void OnBillArrived()
        {
            Hud.Pulse();
            Audio.Play(SoundId.Bill, 1f, 0.5f);
        }

        private void OnBuyClicked(int index)
        {
            if (Session.Businesses.Buy(index, Sheet.Mode)) return;
            Audio.Play(SoundId.Button, 0.8f);
        }

        private void OnBestClicked()
        {
            var best = PurchaseAdvisor.FindBestLevels(Session.Businesses, Session.Economy, Session.EconomyConfig);
            if (!best.IsValid || !PurchaseAdvisor.Apply(Session.Businesses, Session.Economy, best)) return;
            if (best.BusinessIndex >= 0)
            {
                Sheet.Select(best.BusinessIndex, true);
                Rig.FocusOn(Presenter.Views[best.BusinessIndex].transform.position.z);
            }
        }

        /// <summary>Écran de retour : les gains sont calculés par la session (OfflineCalculator), jamais ici.</summary>
        public void ShowOffline(double awaySeconds)
        {
            var result = Session.ComputeOffline(awaySeconds); // plafond : base + étoiles + extension cauris
            if (result.Earnings <= BigDouble.Zero)
            {
                Session.ClaimOffline(result, 1d);
                return;
            }
            Offline.Show(result.Earnings, awaySeconds, _ads.IsRewardedReady("offline_x2"), doubled =>
            {
                if (!doubled)
                {
                    Session.ClaimOffline(result, 1d);
                    Money.Rain(18, 1000f, 1800f);
                    return;
                }
                _ads.ShowRewarded("offline_x2", ad =>
                {
                    // Récompense uniquement sur le callback de récompense.
                    if (ad == AdResult.Rewarded) Meta.OnRewardedWatched();
                    double multiplier = ad == AdResult.Rewarded ? Session.RewardOfflineAd(DayKey()) : 1d;
                    Session.ClaimOffline(result, multiplier);
                    Money.Rain(ad == AdResult.Rewarded ? 36 : 18, 1000f, 1800f);
                });
            });
            Money.Rain(24, 1000f, 1800f);
            Audio.Play(SoundId.Cash);
        }

        private int DayKey() => _gameClock.LocalDayKey;

        private void BuildDebug(RectTransform safe)
        {
            _debug = new DebugPanel(safe, hudConfig, assets);
            _debug.AddButton("Temps x1 / x10 / x100", () =>
            {
                _speedIndex = (_speedIndex + 1) % Speeds.Length;
                GameTime.Speed = Speeds[_speedIndex];
            });
            _debug.AddButton("Donner de l'argent", () =>
                Session.Economy.Earn(BigDouble.Max(BigDouble.FromDouble(1e4), Session.Economy.Money * 10d)));
            _debug.AddButton("Tout débloquer", () =>
            {
                for (int i = 0; i < Session.Businesses.Count; i++)
                {
                    if (Session.Businesses[i].IsUnlocked) continue;
                    Session.Economy.Earn(Session.Businesses.QuoteLevels(i, 1).Cost);
                    Session.Businesses.BuyLevels(i, 1);
                }
            });
            _debug.AddButton("Méta : niveau +1", () =>
            {
                var level = Meta.Level;
                if (!level.IsMax) level.AddRaw(level.XpToNext - level.XpIntoLevel);
            });
            _debug.AddButton("Méta : coffre Or", () => Meta.Chests.Open("gold", "debug"));
            _debug.AddButton("Méta : +500 cauris", () => Meta.Ledger.Credit(Currency.Cauris, 500, "debug"));
            _debug.AddButton("Forcer jour / nuit", () =>
            {
                _forcedHourIndex = (_forcedHourIndex + 1) % ForcedHours.Length;
                GameTime.ForcedHour = ForcedHours[_forcedHourIndex];
            });
            _debug.AddButton("Client spécial", () =>
            {
                Specials.ForceSpawn(_specialIndex);
                _specialIndex = (_specialIndex + 1) % Math.Max(1, specialCustomers.customers.Length);
            });
            _debug.AddButton("Retour après 8 h", () => ShowOffline(8 * 3600d));
            _debug.AddButton("Stats FPS / batches", () => _debug.ToggleStats());
            _debug.AddButton("Effets réduits on/off", () =>
            {
                _juice.reducedEffects = !_juice.reducedEffects;
                _moneyRules.MinBurstIntervalSeconds = _juice.ToMoneyRules().MinBurstIntervalSeconds;
            });
            _debug.AddButton("Qualité Bas / Moyen / Haut", () => ApplyQuality((QualityTier)(((int)Quality.Tier + 1) % 3)));
            _debug.AddButton("Prestige (rejouer Dakar)", () => Session.PerformPrestige(false));
            _debug.AddButton("Effacer la sauvegarde", () => _save.Delete());
        }

        /// <summary>Réglages joueur : son, effets réduits, qualité (auto = détection au démarrage).</summary>
        private void ApplyDeviceSettings()
        {
            Audio.SetBusEnabled(AudioBus.Music, _deviceSettings.Music);
            Audio.SetBusEnabled(AudioBus.Sfx, _deviceSettings.Sfx);
            Audio.SetBusEnabled(AudioBus.Voice, _deviceSettings.Sfx);
            if (_juice.reducedEffects != _deviceSettings.ReducedEffects)
            {
                _juice.reducedEffects = _deviceSettings.ReducedEffects;
                _moneyRules.MinBurstIntervalSeconds = _juice.ToMoneyRules().MinBurstIntervalSeconds;
            }
            var tier = _deviceSettings.Quality < 0 ? Quality.Detect() : (QualityTier)_deviceSettings.Quality;
            if (tier != Quality.Tier) ApplyQuality(tier);
        }

        public void ApplyQuality(QualityTier tier)
        {
            var t = Quality.Apply(tier);
            Presenter.Crowd.SetMaxTotal(t.maxCustomers);
            Effects.SetParticleScale(t.particleScale);
            Presenter.Resync();
        }

        private void Update()
        {
            if (!Ready) return;
            double gameDt = GameTime.Advance(Time.unscaledDeltaTime);
            double hour = GameTime.HourOfDay;
            // Sous-pas : fins de cycle précises même en x100 (aucune allocation).
            int guard = 0;
            double remaining = gameDt;
            while (remaining > 1e-9 && guard++ < 400)
            {
                double step = Math.Min(remaining, GameTime.MaxSubstep);
                Session.Tick(step, hour);
                Specials.Tick(step);
                remaining -= step;
            }
            _gameClock.AdvancePlayed(Time.unscaledDeltaTime);
            Meta.Tick(gameDt);
            _lastGameDt = (float)gameDt;

            _offsetTimer += Time.unscaledDeltaTime;
            if (_offsetTimer >= 60f) RefreshUtcOffset();

            _autosave += Time.unscaledDeltaTime;
            if (_autosave >= autosaveSeconds)
            {
                _autosave = 0f;
                Save();
            }
        }

        private float _lastGameDt;

        private void LateUpdate()
        {
            if (!Ready) return;
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            double hour = GameTime.HourOfDay;
            _dayNight.Apply(GameTime.DayFraction);
            Layout.Refresh();
            var screen = Layout.Screen;
            if (screen.height > 0f)
                Rig.StreetViewport = new Vector2((Layout.Street.yMin - screen.yMin) / screen.height, (Layout.Street.yMax - screen.yMin) / screen.height);
            Presenter.Tick(dt, _lastGameDt, time, hour);
            Persons.Render();
            Money.Update(dt);
            _worldUi.Tick();
            Hud.Tick(dt);
            MetaUi.Tick(dt);
            Sheet.Tick(dt, hour, Session.EconomyConfig.DayNight);
            Offline.Tick(dt);
            Audio.Tick(dt, Presenter.Activity(), _dayNight.Night);
            _debug?.Tick(dt);
        }

        public void Save()
        {
            if (Session == null || _save == null) return;
            try
            {
                var data = Session.ToSaveData(_clock.UtcNow, _clock.MonotonicSeconds);
                data.Meta = Meta?.Save();
                _save.Save(data);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameRoot] Save failed: " + e.Message);
            }
        }

        private void RefreshUtcOffset()
        {
            _offsetTimer = 0f;
            _utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
                ScheduleNotifications();
            }
            else
            {
                RefreshUtcOffset();
                _notifications?.CancelAll();
            }
        }

        /// <summary>Mise en arrière-plan : max 2 par jour, jamais entre 22 h et 8 h, désactivables (§7.6).</summary>
        public void ScheduleNotifications()
        {
            if (Meta == null || _notifications == null) return;
            Meta.NotificationCandidates(_notificationCandidates);
            NotificationPlanner.Plan(Meta.Definitions.Notifications, Meta.NotificationsEnabled && _notifications.HasPermission,
                _gameClock.LocalNow, _notificationCandidates, _plannedNotifications);
            _notifications.Schedule(_plannedNotifications, NotificationText);
        }

        private static string NotificationText(NotificationKind kind)
        {
            switch (kind)
            {
                case NotificationKind.FreeChestReady: return Loc.Get("notif.chest_ready");
                case NotificationKind.OfflineFull: return Loc.Get("notif.offline_full");
                case NotificationKind.StreakInDanger: return Loc.Get("notif.streak_danger");
                default: return Loc.Get("login.title");
            }
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            _debug?.Dispose();
            MetaUi?.Freeze?.Dispose();
            Quality?.Restore();
            if (_juice != null) Destroy(_juice);
        }
    }
}
