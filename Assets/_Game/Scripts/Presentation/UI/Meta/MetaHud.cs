using System;
using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>Bouton rond de rail (maquette 02) : pastille, libellé dessous, minuterie, point rouge.</summary>
    public sealed class RailButton
    {
        public readonly RectTransform Root;
        public readonly UiButton Button;
        public readonly TextMeshProUGUI Caption, Timer;
        public readonly Image Dot, Disc;
        public readonly TextBuffer TimerText = new TextBuffer(32);

        public const float Size = 116f, Height = 176f;

        public RailButton(RectTransform parent, string name, HudConfig hud, StreetAssets assets, string glyph, string caption, Color color)
        {
            Root = UiFactory.Rect(parent, name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Height), Vector2.zero);
            var discRt = UiFactory.Anchored(Root, "Disc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Size, Size));
            Disc = UiFactory.Image(discRt, color, assets.circle, true);
            var glyphText = UiFactory.Text(UiFactory.Stretch(discRt, "Glyph"), hud, hud.fontSizeLarge * 0.8f, hud.text, TextAlignmentOptions.Center, glyph);
            Button = discRt.gameObject.AddComponent<UiButton>();
            Button.Init(Disc, glyphText, hud);
            Button.EnabledColor = color;
            Button.Background.color = color;
            Caption = MetaUiKit.Label(Root, "Caption", hud, hud.fontSizeSmall * 0.62f, hud.text, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(-20f, 30f), new Vector2(20f, 60f), TextAlignmentOptions.Center, caption);
            MetaUiKit.Outline(Caption);
            Caption.enableAutoSizing = true;
            Caption.fontSizeMin = hud.fontSizeSmall * 0.4f;
            Caption.fontSizeMax = hud.fontSizeSmall * 0.62f;
            var timerBack = UiFactory.Rect(Root, "TimerBack", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 0f), new Vector2(10f, 32f));
            UiFactory.Image(timerBack, hud.panel, assets.rounded);
            Timer = MetaUiKit.Label(timerBack, "Timer", hud, hud.fontSizeSmall * 0.6f, hud.text, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center);
            Dot = MetaUiKit.RedDot(discRt, assets, 34f);
        }

        public void SetVisible(bool on)
        {
            if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
        }

        public void SetTimerVisible(bool on)
        {
            var back = Timer.transform.parent.gameObject;
            if (back.activeSelf != on) back.SetActive(on);
        }
    }

    /// <summary>
    /// Couche méta du HUD (maquette 02) : bandeau « Objectif » sous le HUD (mission en cours, tap = missions),
    /// trophée (succès) ; rail droit Cadeaux / Roue / Coffre / Album ; rail gauche boost de gérant et x2 (pub).
    /// Chaque élément n'apparaît qu'une fois sa fonctionnalité débloquée par le niveau joueur (écran léger au début).
    /// Points rouges : règle unique <see cref="AttentionRules"/>.
    /// </summary>
    public sealed class MetaHud
    {
        private readonly MetaUiContext _ctx;
        private readonly RectTransform _banner, _leftRail, _rightRail;
        private readonly TextMeshProUGUI _objectiveText, _objectiveReward, _objectiveProgress;
        private readonly Image _objectiveFill, _objectiveDot;
        private readonly TextBuffer _progressText = new TextBuffer(48);
        private readonly RailButton _trophy, _gifts, _wheel, _chest, _album, _managerBoost, _adBoost;
        private readonly string _free, _ready, _go, _x2;
        private readonly LocTemplate _active, _cooldown;
        private MissionInstance _shownMission;
        private int _shownSlot = -1;
        private float _refresh;
        private Func<int> _selectedBusiness;

        public RectTransform Banner => _banner;
        public RectTransform LeftRail => _leftRail;
        public RectTransform RightRail => _rightRail;

        public Action MissionsClicked, AchievementsClicked, GiftsClicked, WheelClicked, ChestClicked, AlbumClicked, AdBoostClicked;

        public MetaHud(RectTransform safeArea, MetaUiContext ctx, Func<int> selectedBusiness)
        {
            _ctx = ctx;
            _selectedBusiness = selectedBusiness;
            var hud = ctx.Hud;
            var assets = ctx.Assets;
            _free = Loc.Get("chest.free");
            _ready = Loc.Get("managers.boost_ready");
            _go = Loc.Get("managers.boost_go");
            _x2 = "x2";
            _active = new LocTemplate("managers.boost_active");
            _cooldown = new LocTemplate("managers.boost_cooldown");
            float top = hud.topBarHeight + 24f;

            // --- Bandeau objectif (mission la plus avancée).
            _banner = UiFactory.Rect(safeArea, "Objective", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -top - 120f), new Vector2(-180f, -top));
            var bannerBack = UiFactory.Image(_banner, hud.panel, assets.rounded, true);
            var bannerButton = _banner.gameObject.AddComponent<UiButton>();
            bannerButton.Init(bannerBack, UiFactory.Text(UiFactory.Stretch(_banner, "Hit"), hud, 1f, Color.clear), hud);
            bannerButton.EnabledColor = hud.panel;
            bannerButton.PunchStrength = 0.04f;
            bannerButton.Clicked = () => MissionsClicked?.Invoke();
            var icon = UiFactory.Anchored(_banner, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(96f, 96f));
            UiFactory.Image(icon, hud.buttonEnabled, assets.rounded);
            UiFactory.Text(UiFactory.Stretch(icon, "Glyph"), hud, hud.fontSizeMedium, hud.text, TextAlignmentOptions.Center, "!");
            MetaUiKit.Label(_banner, "Title", hud, hud.fontSizeSmall * 0.75f, hud.accent, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(124f, -40f), new Vector2(-12f, -6f), TextAlignmentOptions.Left, Loc.Get("missions.title"));
            _objectiveText = MetaUiKit.Label(_banner, "Text", hud, hud.fontSizeSmall * 0.8f, hud.text, new Vector2(0f, 0.38f), new Vector2(1f, 0.68f),
                new Vector2(124f, 0f), new Vector2(-150f, 0f));
            _objectiveText.textWrappingMode = TextWrappingModes.NoWrap;
            _objectiveText.overflowMode = TextOverflowModes.Truncate;
            _objectiveFill = MetaUiKit.Bar(_banner, "Bar", hud, assets, hud.buttonEnabled, new Vector2(0f, 0f), new Vector2(1f, 0.34f),
                new Vector2(124f, 12f), new Vector2(-150f, 0f));
            _objectiveProgress = MetaUiKit.Label((RectTransform)_objectiveFill.transform.parent, "Progress", hud, hud.fontSizeSmall * 0.6f, hud.text,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            MetaUiKit.Outline(_objectiveProgress);
            _objectiveReward = MetaUiKit.Label(_banner, "Reward", hud, hud.fontSizeSmall * 0.75f, hud.cauris, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-144f, 0f), new Vector2(-12f, 0f), TextAlignmentOptions.Center);
            _objectiveReward.textWrappingMode = TextWrappingModes.Normal;
            _objectiveDot = MetaUiKit.RedDot(_banner, assets, 34f);

            // --- Trophée à droite du bandeau.
            var trophyCell = UiFactory.Rect(safeArea, "TrophyCell", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-164f, -top - RailButton.Height), new Vector2(-16f, -top));
            _trophy = new RailButton(trophyCell, "Trophy", hud, assets, "1er", Loc.Get("achievements.title"), new Color(0.85f, 0.62f, 0.15f));
            _trophy.Button.Clicked = () => AchievementsClicked?.Invoke();
            _trophy.SetTimerVisible(false);

            // --- Rail droit.
            _rightRail = UiFactory.Rect(safeArea, "RightRail", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-164f, -top - RailButton.Height * 5f), new Vector2(-16f, -top - RailButton.Height - 10f));
            _gifts = new RailButton(_rightRail, "Gifts", hud, assets, "♥", Loc.Get("daily.title"), new Color(0.85f, 0.25f, 0.45f));
            _gifts.Button.Clicked = () => GiftsClicked?.Invoke();
            _wheel = new RailButton(_rightRail, "Wheel", hud, assets, "R", Loc.Get("wheel.title"), new Color(0.25f, 0.55f, 0.95f));
            _wheel.Button.Clicked = () => WheelClicked?.Invoke();
            _chest = new RailButton(_rightRail, "Chest", hud, assets, "C", Loc.Get("shop.chests"), new Color(0.80f, 0.50f, 0.20f));
            _chest.Button.Clicked = () => ChestClicked?.Invoke();
            _album = new RailButton(_rightRail, "Album", hud, assets, "A", Loc.Get("album.title"), new Color(0.35f, 0.65f, 0.35f));
            _album.Button.Clicked = () => AlbumClicked?.Invoke();
            _album.SetTimerVisible(false);

            // --- Rail gauche : boosts (le joueur idle n'est jamais pénalisé : ce sont des bonus, pas des corvées).
            _leftRail = UiFactory.Rect(safeArea, "LeftRail", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -top - 140f - RailButton.Height * 2f), new Vector2(164f, -top - 140f));
            _managerBoost = new RailButton(_leftRail, "ManagerBoost", hud, assets, "»", Loc.Get("managers.boost_go"), new Color(0.95f, 0.55f, 0.10f));
            _managerBoost.Button.Clicked = OnManagerBoost;
            _adBoost = new RailButton(_leftRail, "AdBoost", hud, assets, "x2", Loc.Get("hud.ad_boost"), new Color(0.55f, 0.30f, 0.85f));
            _adBoost.Button.Clicked = () => AdBoostClicked?.Invoke();
            // Places fixes à gauche : le boost x2 ne saute pas quand le gérant change.
            _adBoost.Root.offsetMin = new Vector2(0f, -2f * RailButton.Height);
            _adBoost.Root.offsetMax = new Vector2(0f, -RailButton.Height);
            Refresh();
        }

        private void OnManagerBoost()
        {
            int b = _selectedBusiness();
            var session = _ctx.Session;
            if (b < 0 || b >= session.Businesses.Count) return;
            if (_ctx.Meta.ActivateManagerBoost(session.Businesses[b].Definition.Id))
            {
                _ctx.Audio?.Play(SoundId.Milestone);
                Refresh();
            }
        }

        /// <summary>Visibilité et empilement des rails (à l'ouverture et quand une fonctionnalité se débloque).</summary>
        public void Refresh()
        {
            var meta = _ctx.Meta;
            bool missions = meta.IsUnlocked(Feature.Missions);
            _banner.gameObject.SetActive(missions);
            _trophy.SetVisible(meta.IsUnlocked(Feature.Achievements));
            _gifts.SetVisible(meta.IsUnlocked(Feature.Login));
            _wheel.SetVisible(meta.IsUnlocked(Feature.Wheel));
            _chest.SetVisible(meta.IsUnlocked(Feature.Chests));
            _album.SetVisible(meta.IsUnlocked(Feature.Album));
            Stack(_rightRail, _gifts, _wheel, _chest, _album);
            _shownSlot = -1;
            Tick(1f);
        }

        private static void Stack(RectTransform rail, params RailButton[] buttons)
        {
            int n = 0;
            foreach (var b in buttons)
            {
                if (!b.Root.gameObject.activeSelf) continue;
                b.Root.offsetMin = new Vector2(0f, -(n + 1) * RailButton.Height);
                b.Root.offsetMax = new Vector2(0f, -n * RailButton.Height);
                n++;
            }
        }

        /// <summary>Minuteries et points rouges, 4 fois par seconde, sans allocation.</summary>
        public void Tick(float dt)
        {
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 0.25f;
            var meta = _ctx.Meta;
            var attention = meta.Attention;

            if (_banner.gameObject.activeSelf) UpdateObjective(meta);
            MetaUiKit.SetDot(_objectiveDot, attention.HasDot(AttentionSlot.Missions) || attention.HasDot(AttentionSlot.Challenges));
            MetaUiKit.SetDot(_trophy.Dot, attention.HasDot(AttentionSlot.Achievements));
            MetaUiKit.SetDot(_gifts.Dot, attention.HasDot(AttentionSlot.Login));
            MetaUiKit.SetDot(_wheel.Dot, attention.HasDot(AttentionSlot.Wheel));
            MetaUiKit.SetDot(_chest.Dot, attention.HasDot(AttentionSlot.Shop));
            MetaUiKit.SetDot(_album.Dot, attention.HasDot(AttentionSlot.Album));

            double toMidnight = meta.Clock.SecondsToLocalMidnight;
            if (_gifts.Root.gameObject.activeSelf)
                (meta.CanClaimLogin ? _gifts.TimerText.Clear().Append(_free) : _gifts.TimerText.Clear().AppendDuration(toMidnight)).ApplyIfChanged(_gifts.Timer);
            if (_wheel.Root.gameObject.activeSelf)
                (meta.CanSpinFree ? _wheel.TimerText.Clear().Append(_free) : _wheel.TimerText.Clear().AppendDuration(toMidnight)).ApplyIfChanged(_wheel.Timer);
            if (_chest.Root.gameObject.activeSelf)
            {
                var chests = meta.Chests;
                if (chests.FreeStored > 0) _chest.TimerText.Clear().Append(_free).Append(" x").AppendLong(chests.FreeStored);
                else _chest.TimerText.Clear().AppendDuration(chests.SecondsToNextFree);
                _chest.TimerText.ApplyIfChanged(_chest.Timer);
            }
            UpdateManagerBoost(meta);
            UpdateAdBoost();
        }

        private void UpdateObjective(MetaSession meta)
        {
            // Mission à réclamer d'abord, sinon la plus avancée.
            int best = -1;
            double bestProgress = -1d;
            for (int s = 0; s < meta.Missions.Slots; s++)
            {
                var m = meta.Missions[s];
                if (m == null || m.Claimed) continue;
                double p = meta.CanClaimMission(s) ? 2d : m.Progress01(meta);
                if (p > bestProgress) { bestProgress = p; best = s; }
            }
            if (best < 0)
            {
                if (_shownSlot != -2) { _objectiveText.text = Loc.Get("missions.title"); _objectiveReward.text = string.Empty; _objectiveFill.fillAmount = 0f; _shownSlot = -2; }
                return;
            }
            var mission = meta.Missions[best];
            if (best != _shownSlot || mission != _shownMission)
            {
                // Changement de mission affichée : rare, texte composé une fois.
                _shownSlot = best;
                _shownMission = mission;
                _objectiveText.text = MetaText.Mission(mission, _ctx.BusinessName);
                _objectiveReward.text = MetaText.MissionReward(mission);
            }
            double current = Math.Min(mission.Target, Math.Max(0d, mission.Current(meta)));
            _objectiveFill.fillAmount = (float)mission.Progress01(meta);
            _progressText.Clear().Append(BigDouble.FromDouble(Math.Floor(current))).Append(" / ").Append(BigDouble.FromDouble(mission.Target))
                .ApplyIfChanged(_objectiveProgress);
        }

        private void UpdateManagerBoost(MetaSession meta)
        {
            int b = _selectedBusiness();
            var businesses = _ctx.Session.Businesses;
            int manager = b >= 0 && b < businesses.Count ? meta.Managers.Equipped(businesses[b].Definition.Id) : -1;
            _managerBoost.SetVisible(manager >= 0);
            if (manager < 0) return;
            var managers = meta.Managers;
            var t = _managerBoost.TimerText.Clear();
            if (managers.IsBoostActive(manager)) t.Append(_active.Prefix).AppendDuration(managers.BoostRemaining(manager)).Append(_active.Suffix);
            else if (managers.CooldownRemaining(manager) > 0d) t.AppendDuration(managers.CooldownRemaining(manager));
            else t.Append(_ready);
            t.ApplyIfChanged(_managerBoost.Timer);
            _managerBoost.Button.SetInteractable(managers.IsBoostReady(manager));
        }

        private void UpdateAdBoost()
        {
            var economy = _ctx.Session.Economy;
            bool ready = _ctx.AdReady("boost_x2");
            bool active = economy.IsAdBoostActive;
            // Bouton masqué si aucune pub disponible et aucun boost en cours (§8.1).
            _adBoost.SetVisible(ready || active);
            var t = _adBoost.TimerText.Clear();
            if (active) t.AppendDuration(economy.AdBoostRemainingSeconds);
            else t.Append(_x2).Append(" 4 h");
            t.ApplyIfChanged(_adBoost.Timer);
        }
    }
}
