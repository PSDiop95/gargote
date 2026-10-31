using System;
using Gargote.Core;
using Gargote.Systems;
using Gargote.Systems.Meta;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// HUD haut (maquette docs/art-reference/02) : pastilles séparées sur la rue, sans bandeau plein.
    /// Ligne 1 : niveau joueur + barre XP en %, FCFA (compteur qui roule et pulse), cauris avec « + » (boutique), réglages.
    /// Ligne 2 : revenu/s, boost actif. Ligne 3 : objectif de ville. Lit l'état ; aucune écriture.
    /// </summary>
    public sealed class HudView
    {
        private const float PillHeight = 84f;
        private const float CaurisWidth = 250f, CaurisRight = 104f;

        private readonly GameSession _session;
        private readonly HudConfig _hud;
        private readonly JuiceConfig _juice;
        private readonly TextMeshProUGUI _money, _cauris, _income, _goalLabel, _boost, _levelNumber, _levelLabel, _xpLabel;
        private readonly Image _goalFill, _xpFill;
        private readonly RectTransform _moneyIcon, _levelChip, _caurisPill;
        private readonly Image _settingsDot;
        private readonly TextBuffer _moneyText = new TextBuffer(40), _incomeText = new TextBuffer(40),
            _goalText = new TextBuffer(96), _boostText = new TextBuffer(64), _levelText = new TextBuffer(24),
            _xpText = new TextBuffer(16), _levelLabelText = new TextBuffer(24), _caurisText = new TextBuffer(24);
        private BigDouble _displayed;
        private bool _initialized;
        private float _refresh;
        private int _shownLevel = -1;
        private Tween _pulse, _levelPop;
        private readonly string _perSecond, _welcome, _adBoost, _levelPrefix;
        private readonly LocTemplate _goalTemplate;
        private string _goalPrefix;
        private int _goalCity = -1;
        private MetaSession _meta;

        public RectTransform MoneyIcon => _moneyIcon;
        public RectTransform Root { get; }
        public RectTransform LevelChip => _levelChip;
        public RectTransform CaurisPill => _caurisPill;

        public Action LevelClicked, CaurisPlusClicked, SettingsClicked;

        public HudView(GameSession session, HudConfig hud, JuiceConfig juice, RectTransform safeArea, StreetAssets assets)
        {
            _session = session;
            _hud = hud;
            _juice = juice;
            _perSecond = Loc.Get("hud.per_second");
            _goalTemplate = new LocTemplate("hud.goal_city");
            _welcome = Loc.Get("hud.welcome");
            _adBoost = Loc.Get("hud.ad_boost");
            _levelPrefix = new LocTemplate("hud.level").Prefix;

            // Zone réservée du HUD (aucun fond plein : la rue reste visible entre les pastilles).
            Root = UiFactory.Rect(safeArea, "Hud", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -hud.topBarHeight - 12f), new Vector2(-16f, -12f));

            // --- Niveau joueur : médaillon + « Niv. 12 » + barre XP « 68 % ».
            _levelChip = UiFactory.Anchored(Root, "Level", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(330f, 112f));
            _levelChip.pivot = new Vector2(0f, 1f);
            _levelChip.anchoredPosition = Vector2.zero;
            var chipBack = UiFactory.Image(_levelChip, hud.panel, assets.rounded, true);
            var chipButton = _levelChip.gameObject.AddComponent<UiButton>();
            chipButton.Init(chipBack, UiFactory.Text(UiFactory.Stretch(_levelChip, "Hit"), hud, 1f, Color.clear), hud);
            chipButton.EnabledColor = hud.panel;
            chipButton.PunchStrength = 0.06f;
            chipButton.Clicked = () => LevelClicked?.Invoke();
            var medal = UiFactory.Anchored(_levelChip, "Medal", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(100f, 100f));
            medal.pivot = new Vector2(0f, 0.5f);
            medal.anchoredPosition = new Vector2(6f, 0f);
            UiFactory.Image(medal, hud.accent, assets.circle);
            var medalInner = UiFactory.Anchored(medal, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            UiFactory.Image(medalInner, hud.panelLight, assets.circle);
            _levelNumber = UiFactory.Text(UiFactory.Stretch(medal, "Number"), hud, hud.fontSizeMedium, hud.text, TextAlignmentOptions.Center, "1");
            _levelLabel = MetaUiKit.Label(_levelChip, "Label", hud, hud.fontSizeSmall, hud.text, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                new Vector2(118f, 0f), new Vector2(-12f, -6f));
            _xpFill = MetaUiKit.Bar(_levelChip, "Xp", hud, assets, hud.money, new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                new Vector2(118f, 14f), new Vector2(-14f, -6f));
            ((Image)_xpFill.transform.parent.GetComponent<Image>()).color = new Color(0f, 0f, 0f, 0.55f);
            _xpLabel = MetaUiKit.Label((RectTransform)_xpFill.transform.parent, "Percent", hud, hud.fontSizeSmall * 0.7f, hud.text, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            MetaUiKit.Outline(_xpLabel);
            _levelChip.gameObject.SetActive(false);

            // --- FCFA.
            // Étirée entre le niveau et les cauris : jamais de chevauchement, même en 20:9 (≈ 960 unités de large).
            var moneyPill = UiFactory.Rect(Root, "MoneyPill", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(344f, -14f - PillHeight), new Vector2(-(CaurisRight + CaurisWidth + 12f), -14f));
            UiFactory.Image(moneyPill, hud.panel, assets.rounded);
            _moneyIcon = UiFactory.Anchored(moneyPill, "MoneyIcon", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 0f), new Vector2(78f, 48f));
            UiFactory.Image(_moneyIcon, juice.billColor, assets.bill);
            _money = MetaUiKit.Label(moneyPill, "Money", hud, hud.fontSizeMedium * 1.05f, hud.money, Vector2.zero, Vector2.one,
                new Vector2(98f, 0f), new Vector2(-14f, 0f));
            // Gros montants sur écran étroit : la taille descend au lieu de déborder (recalcul seulement quand le texte change).
            _money.enableAutoSizing = true;
            _money.fontSizeMax = _money.fontSize;
            _money.fontSizeMin = _money.fontSize * 0.65f;

            // --- Cauris (monnaie premium) + « + » vers la boutique.
            _caurisPill = UiFactory.Anchored(Root, "CaurisPill", new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(CaurisWidth, PillHeight));
            _caurisPill.pivot = new Vector2(1f, 1f);
            _caurisPill.anchoredPosition = new Vector2(-CaurisRight, -14f);
            UiFactory.Image(_caurisPill, hud.panel, assets.rounded);
            var caurisIcon = UiFactory.Anchored(_caurisPill, "CaurisIcon", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(50f, 50f));
            UiFactory.Image(caurisIcon, hud.cauris, assets.circle);
            _cauris = MetaUiKit.Label(_caurisPill, "Cauris", hud, hud.fontSizeMedium * 0.9f, hud.cauris, Vector2.zero, Vector2.one,
                new Vector2(72f, 0f), new Vector2(-78f, 0f), TextAlignmentOptions.Right, "0");
            var plus = MetaUiKit.Button(_caurisPill, "Plus", hud, assets, "+", hud.fontSizeMedium, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-72f, -32f), new Vector2(-8f, 32f), hud.buttonEnabled);
            plus.Clicked = () => CaurisPlusClicked?.Invoke();

            // --- Réglages.
            var settings = MetaUiKit.Button(Root, "Settings", hud, assets, "≡", hud.fontSizeMedium, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-92f, -98f), new Vector2(0f, -14f), hud.panel);
            settings.Clicked = () => SettingsClicked?.Invoke();
            _settingsDot = MetaUiKit.RedDot((RectTransform)settings.transform.parent, assets, 28f);

            // --- Ligne 2 : revenu/s sous les FCFA, boost actif à droite.
            _income = MetaUiKit.Label(Root, "Income", hud, hud.fontSizeSmall * 0.9f, hud.money, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(354f, -142f), new Vector2(754f, -100f));
            _boost = MetaUiKit.Label(Root, "Boost", hud, hud.fontSizeSmall * 0.9f, hud.accent, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-440f, -142f), new Vector2(-104f, -100f), TextAlignmentOptions.Right);

            // --- Objectif de ville (barre logarithmique).
            var goalBack = UiFactory.Rect(Root, "GoalBack", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(0f, 62f));
            UiFactory.Image(goalBack, hud.panel, assets.rounded);
            var goalFillRt = UiFactory.Stretch(goalBack, "GoalFill");
            goalFillRt.offsetMin = new Vector2(4f, 4f);
            goalFillRt.offsetMax = new Vector2(-4f, -4f);
            _goalFill = UiFactory.Image(goalFillRt, hud.accent, assets.rounded);
            _goalFill.type = Image.Type.Filled;
            _goalFill.fillMethod = Image.FillMethod.Horizontal;
            var goalLabelRt = UiFactory.Stretch(goalBack, "GoalLabel");
            _goalLabel = UiFactory.Text(goalLabelRt, hud, hud.fontSizeSmall * 0.8f, hud.text);
        }

        /// <summary>Branche la méta (niveau joueur, cauris). Sans méta (sauvegarde M2 en cours de migration) : pastilles masquées.</summary>
        public void Bind(MetaSession meta)
        {
            _meta = meta;
            _levelChip.gameObject.SetActive(meta != null);
            _shownLevel = -1;
        }

        /// <summary>Un billet est arrivé : pulse du compteur.</summary>
        public void Pulse()
        {
            Juice.Punch(ref _pulse, _money.rectTransform, Vector3.one, _juice.counterPulseScale - 1f, 0.2f);
        }

        public void SetSettingsDot(bool on) => MetaUiKit.SetDot(_settingsDot, on);

        public void Tick(float dt)
        {
            BigDouble target = _session.Economy.Money;
            if (!_initialized) { _displayed = target; _initialized = true; }
            // Interpolation exponentielle vers la vraie valeur (jamais au-dessus de l'argent réel en fin de course).
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, _juice.counterRollSeconds));
            BigDouble diff = target - _displayed;
            _displayed = (diff.IsZero || (diff.IsNegative ? -diff : diff) <= target * 1e-4) ? target : _displayed + diff * k;
            _moneyText.Clear().Append(_displayed).ApplyIfChanged(_money);

            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = _hud.refreshInterval;

            var businesses = _session.Businesses;
            _incomeText.Clear().Append('+').Append(businesses.TotalIncomePerSecond()).Append(_perSecond).ApplyIfChanged(_income);

            if (_goalCity != _session.CityIndex)
            {
                // Une fois par ville (pas d'allocation par frame) : « Objectif Abidjan : … ».
                _goalCity = _session.CityIndex;
                _goalPrefix = _goalTemplate.Format(Loc.Get("city." + _session.City.Id));
            }
            double goal = _session.City.GoalLifetimeEarnings;
            // Barre logarithmique : l'objectif se compte en ordres de grandeur, une barre linéaire resterait vide des jours.
            double lifetime = Math.Max(1d, _session.Economy.CityLifetimeEarnings.Log10());
            _goalFill.fillAmount = Mathf.Clamp01((float)(_session.IsCityGoalReached ? 1d : lifetime / Math.Log10(Math.Max(10d, goal))));
            _goalText.Clear().Append(_goalPrefix).Append(' ').Append(BigDouble.FromDouble(goal)).Append(" FCFA").ApplyIfChanged(_goalLabel);

            var economy = _session.Economy;
            _boostText.Clear();
            if (economy.WelcomeRemainingSeconds > 0d)
                AppendTimer(_boostText.Append(_welcome).Append(' '), economy.WelcomeRemainingSeconds);
            else if (economy.IsAdBoostActive)
                AppendTimer(_boostText.Append(_adBoost).Append(' '), economy.AdBoostRemainingSeconds);
            _boostText.ApplyIfChanged(_boost);

            if (_meta == null) return;
            _caurisText.Clear().AppendLong(_meta.Cauris).ApplyIfChanged(_cauris);
            var level = _meta.Level;
            if (level.Level != _shownLevel)
            {
                if (_shownLevel > 0) Juice.Pop(ref _levelPop, _levelChip, Vector3.one, 0.3f);
                _shownLevel = level.Level;
                _levelText.Clear().AppendLong(level.Level).ApplyIfChanged(_levelNumber);
                _levelLabelText.Clear().Append(_levelPrefix).AppendLong(level.Level).ApplyIfChanged(_levelLabel);
            }
            float xp01 = level.IsMax || level.XpToNext <= 0 ? 1f : Mathf.Clamp01((float)level.XpIntoLevel / level.XpToNext);
            _xpFill.fillAmount = xp01;
            _xpText.Clear().AppendLong((long)(xp01 * 100f)).Append(" %").ApplyIfChanged(_xpLabel);
        }

        private static void AppendTimer(TextBuffer buffer, double seconds)
        {
            long s = (long)Math.Ceiling(seconds);
            long h = s / 3600, m = s / 60 % 60, r = s % 60;
            if (h > 0) buffer.AppendLong(h).Append(':');
            if (m < 10 && h > 0) buffer.Append('0');
            buffer.AppendLong(m).Append(':');
            if (r < 10) buffer.Append('0');
            buffer.AppendLong(r);
        }
    }
}
