using System;
using Gargote.Core;
using Gargote.Systems.Meta;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>Toast court sous le HUD (maquette 02 « Super ! Tu as gagné… »). File de 4 messages, sans allocation par frame.</summary>
    public sealed class ToastView
    {
        private const int Capacity = 4;
        private const float Seconds = 2.4f;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _text;
        private readonly string[] _queue = new string[Capacity];
        private int _head, _count;
        private float _left;
        private Tween _pop;

        public RectTransform Root => _root;

        public ToastView(RectTransform parent, HudConfig hud, StreetAssets assets, float top)
        {
            _root = UiFactory.Anchored(parent, "Toast", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(700f, 110f));
            UiFactory.Image(_root, hud.buttonEnabled, assets.rounded);
            var inner = UiFactory.Stretch(_root, "Inner");
            inner.offsetMin = new Vector2(6f, 6f);
            inner.offsetMax = new Vector2(-6f, -6f);
            UiFactory.Image(inner, hud.panel, assets.rounded);
            _text = MetaUiKit.Paragraph(inner, "Text", hud, hud.fontSizeSmall * 0.85f, hud.text, Vector2.zero, Vector2.one,
                new Vector2(24f, 6f), new Vector2(-24f, -6f), TextAlignmentOptions.Center);
            _root.gameObject.SetActive(false);
        }

        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_count == Capacity) { _head = (_head + 1) % Capacity; _count--; }
            _queue[(_head + _count) % Capacity] = message;
            _count++;
            if (!_root.gameObject.activeSelf) Next();
        }

        private void Next()
        {
            if (_count == 0) { _root.gameObject.SetActive(false); return; }
            _text.text = _queue[_head];
            _queue[_head] = null;
            _head = (_head + 1) % Capacity;
            _count--;
            _left = Seconds;
            _root.gameObject.SetActive(true);
            Juice.Pop(ref _pop, _root, Vector3.one, 0.3f);
        }

        /// <summary>Cale le toast entre les rails (largeur max 700, centré sur l'espace libre), en unités du canvas.</summary>
        public void FitBetween(float left, float right, float screenCenterX)
        {
            float width = Mathf.Min(MaxWidth, right - left);
            var size = _root.sizeDelta;
            if (!Mathf.Approximately(size.x, width)) _root.sizeDelta = new Vector2(width, size.y);
            var pos = _root.anchoredPosition;
            float x = (left + right) * 0.5f - screenCenterX;
            if (!Mathf.Approximately(pos.x, x)) _root.anchoredPosition = new Vector2(x, pos.y);
        }

        private const float MaxWidth = 700f;

        public void Tick(float dt)
        {
            if (!_root.gameObject.activeSelf) return;
            _left -= dt;
            if (_left <= 0f) Next();
        }
    }

    /// <summary>
    /// Composition de l'UI méta : HUD méta, écrans modaux (construits une fois, activés à la demande), file de révélation
    /// des coffres, toasts. Les écrans ne s'ouvrent jamais d'eux-mêmes par-dessus une révélation de coffre.
    /// </summary>
    public sealed class MetaUi
    {
        private readonly MetaUiContext _ctx;
        private readonly MetaScreen[] _screens;

        public MetaHud Hud { get; }
        public ToastView Toast { get; }
        public MissionsScreen Missions { get; }
        public ChestOpenScreen ChestOpen { get; }
        public ChestOddsScreen ChestOdds { get; }
        public DailyScreen Daily { get; }
        public WheelScreen Wheel { get; }
        public ManagersScreen Managers { get; }
        public ManagerDetailScreen ManagerDetail { get; }
        public AlbumScreen Album { get; }
        public RecipeScreen Recipe { get; }
        public AchievementsScreen Achievements { get; }
        public UpgradesScreen Upgrades { get; }
        public CitiesScreen Cities { get; }
        public ShopScreen Shop { get; }
        public PrestigeScreen Prestige { get; }
        public TravelCinematic Travel { get; }
        public SettingsScreen Settings { get; }
        public NotificationPromptScreen NotificationPrompt { get; }
        private bool _promptPending;

        private BusinessSheet _sheet;
        /// <summary>Zones d'interface (rails) : le toast se place entre elles. Assigné par le composition root.</summary>
        public UiLayout Layout;
        /// <summary>Rue figée sous les fenêtres (budget GPU des téléphones d'entrée de gamme).</summary>
        public StreetFreeze Freeze;
        private float _dots;

        public bool AnyOpen
        {
            get { foreach (var s in _screens) if (s.IsOpen) return true; return false; }
        }

        public MetaUi(RectTransform hudSafe, RectTransform modalSafe, MetaUiContext ctx, Func<int> selectedBusiness, DeviceSettings settings)
        {
            _ctx = ctx;
            var meta = ctx.Meta;
            Hud = new MetaHud(hudSafe, ctx, selectedBusiness);
            Toast = new ToastView(modalSafe, ctx.Hud, ctx.Assets, ctx.Hud.topBarHeight + 160f);
            // Sous le bandeau objectif, entre les rails.
            ctx.Toast = Toast.Show;

            Missions = new MissionsScreen(modalSafe, ctx);
            ChestOpen = new ChestOpenScreen(modalSafe, ctx);
            ChestOdds = new ChestOddsScreen(modalSafe, ctx);
            Daily = new DailyScreen(modalSafe, ctx);
            Wheel = new WheelScreen(modalSafe, ctx);
            ManagerDetail = new ManagerDetailScreen(modalSafe, ctx);
            Managers = new ManagersScreen(modalSafe, ctx, ManagerDetail);
            Recipe = new RecipeScreen(modalSafe, ctx);
            Album = new AlbumScreen(modalSafe, ctx, Recipe);
            Achievements = new AchievementsScreen(modalSafe, ctx);
            Upgrades = new UpgradesScreen(modalSafe, ctx);
            Prestige = new PrestigeScreen(modalSafe, ctx);
            Cities = new CitiesScreen(modalSafe, ctx, Prestige);
            Shop = new ShopScreen(modalSafe, ctx, ChestOdds);
            Travel = new TravelCinematic(modalSafe, ctx);
            Settings = new SettingsScreen(modalSafe, ctx, settings);
            NotificationPrompt = new NotificationPromptScreen(modalSafe, ctx);
            _screens = new MetaScreen[]
            {
                Missions, ChestOpen, ChestOdds, Daily, Wheel, Managers, ManagerDetail, Album, Recipe, Achievements,
                Upgrades, Cities, Shop, Prestige, Travel, Settings, NotificationPrompt,
            };
            // Demande douce après le premier coffre gratuit, une fois sa révélation terminée.
            meta.NotificationPermissionWanted += () => _promptPending = true;
            Prestige.Performed = (from, to, travelled) =>
            {
                Cities.Hide();
                if (travelled) Travel.Play(from, to);
                else Toast.Show(new LocTemplate("prestige.done").Format(_ctx.Session.Prestige.RevenueMultiplier.ToString("0.##")));
                Hud.Refresh();
            };

            Hud.MissionsClicked = Missions.Show;
            Hud.GiftsClicked = Daily.Show;
            Hud.WheelClicked = Wheel.Show;
            Hud.AlbumClicked = Album.Show;
            Hud.AchievementsClicked = Achievements.Show;
            Hud.ChestClicked = OnChestClicked;
            Hud.AdBoostClicked = OnAdBoost;
            ChestOpen.Finished = ShowNextReveal;

            meta.ChestQueued += _ => { if (!ChestOpen.IsOpen) ShowNextReveal(); };
            meta.FeatureIntroduced += f => { Hud.Refresh(); Toast.Show(Loc.Get("feature." + MetaText.Snake(f.ToString()))); };
            meta.PlayerLeveledUp += (before, after) =>
            {
                Hud.Refresh();
                Toast.Show(new LocTemplate("level.up").Format(after));
                ctx.Audio?.Play(SoundId.Milestone);
            };
            meta.RecipeUnlocked += r => Toast.Show(Loc.Get("album.new_recipe") + " " + Loc.Get("recipe." + r.Id + ".name"));
            meta.AchievementReached += (index, tier) =>
                Toast.Show(new LocTemplate("achievements.reached").Format(Loc.Get("achievement." + meta.Definitions.Achievements[index].Id + ".name")));
            meta.MissionsChanged += () => { if (Missions.IsOpen) Missions.Refresh(); };
        }

        /// <summary>Onglets du panneau bas : 1 Gérants, 2 Upgrades, 3 Villes, 4 Boutique.</summary>
        public void BindSheet(BusinessSheet sheet)
        {
            _sheet = sheet;
            sheet.TabClicked = OpenTab;
        }

        public void OpenTab(int tab)
        {
            switch (tab)
            {
                case 1: Managers.Show(); break;
                case 2: Upgrades.Show(); break;
                case 3: Cities.Show(); break;
                case 4: Shop.Show(); break;
            }
        }

        private void FitToast()
        {
            var screen = Layout.Screen;
            float left = Layout.IsActive(UiZone.LeftRail) ? Layout.Zone(UiZone.LeftRail).xMax : screen.xMin;
            float right = Layout.IsActive(UiZone.RightRail) ? Layout.Zone(UiZone.RightRail).xMin : screen.xMax;
            // Le canvas des fenêtres et l'espace du layout n'ont pas forcément la même échelle : conversion mesurée.
            // Mesure sur le parent (jamais animé) : le toast lui-même change d'échelle pendant son apparition.
            var parent = (RectTransform)Toast.Root.parent;
            float local = parent.rect.width;
            float k = local > 1f ? Layout.ToSpace(parent).width / local : 1f;
            if (k <= 0f) k = 1f;
            Toast.FitBetween((left + UiLayout.Margin * 2f) / k, (right - UiLayout.Margin * 2f) / k, screen.center.x / k);
        }

        private void RefreshTabDots()
        {
            if (_sheet == null) return;
            var a = _ctx.Meta.Attention;
            _sheet.SetTabDot(1, a.HasDot(AttentionSlot.Managers));
            _sheet.SetTabDot(2, a.HasDot(AttentionSlot.Upgrades));
            _sheet.SetTabDot(3, a.HasDot(AttentionSlot.Cities));
            _sheet.SetTabDot(4, a.HasDot(AttentionSlot.Shop));
        }

        private void OnChestClicked()
        {
            var meta = _ctx.Meta;
            if (meta.CanClaimFreeChest) { meta.ClaimFreeChest(); return; }
            Toast.Show(new LocTemplate("chest.free_in").Format(MetaFormat.Duration(meta.Chests.SecondsToNextFree)));
        }

        private void OnAdBoost()
        {
            var meta = _ctx.Meta;
            _ctx.Rewarded("boost_x2", () =>
            {
                if (meta.Session.RewardBoostAd(meta.DayKey)) _ctx.Audio?.Play(SoundId.Milestone);
            });
        }

        private void ShowNextReveal()
        {
            var next = _ctx.Meta.NextReveal();
            if (next != null) { ChestOpen.Show(next); return; }
            if (_promptPending) { _promptPending = false; NotificationPrompt.Show(); }
        }

        /// <summary>Clic sur le niveau : prochaine fonction à débloquer (onboarding progressif, §7.3).</summary>
        public void ShowLevelInfo()
        {
            var level = _ctx.Meta.Level;
            Feature next = default;
            int at = int.MaxValue;
            foreach (Feature f in (Feature[])Enum.GetValues(typeof(Feature)))
            {
                int l = level.Config.UnlockLevel(f);
                if (l > level.Level && l < at) { at = l; next = f; }
            }
            Toast.Show(at == int.MaxValue
                ? new LocTemplate("level.all_unlocked").Format(level.Level)
                : new LocTemplate("level.next_feature").Format(Loc.Get("feature_name." + MetaText.Snake(next.ToString())), at));
        }

        public void Tick(float dt)
        {
            if (Freeze != null)
            {
                bool open = false;
                for (int i = 0; i < _screens.Length && !open; i++) open = _screens[i].IsOpen;
                Freeze.Set(open);
            }
            Hud.Tick(dt);
            Toast.Tick(dt);
            if (Layout != null && Toast.Root.gameObject.activeSelf) FitToast();
            _dots -= dt;
            if (_dots <= 0f) { _dots = 0.25f; RefreshTabDots(); }
            for (int i = 0; i < _screens.Length; i++) _screens[i].Tick(dt);
        }
    }
}
