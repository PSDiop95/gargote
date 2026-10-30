using System;
using Gargote.Core;
using Gargote.Systems;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// HUD haut : FCFA (compteur qui roule et pulse), cauris, revenu/s, barre d'objectif de ville, boosts actifs.
    /// Lit l'état de la session ; aucune écriture.
    /// </summary>
    public sealed class HudView
    {
        private readonly GameSession _session;
        private readonly HudConfig _hud;
        private readonly JuiceConfig _juice;
        private readonly TextMeshProUGUI _money, _cauris, _income, _goalLabel, _boost;
        private readonly Image _goalFill;
        private readonly RectTransform _moneyIcon;
        private readonly TextBuffer _moneyText = new TextBuffer(40), _incomeText = new TextBuffer(40),
            _goalText = new TextBuffer(96), _boostText = new TextBuffer(64);
        private BigDouble _displayed;
        private bool _initialized;
        private float _refresh;
        private Tween _pulse;
        private readonly string _perSecond, _goalPrefix, _welcome, _adBoost;

        public RectTransform MoneyIcon => _moneyIcon;
        public RectTransform Root { get; }

        public HudView(GameSession session, HudConfig hud, JuiceConfig juice, RectTransform safeArea, StreetAssets assets)
        {
            _session = session;
            _hud = hud;
            _juice = juice;
            _perSecond = Loc.Get("hud.per_second");
            _goalPrefix = Loc.Get("hud.goal");
            _welcome = Loc.Get("hud.welcome");
            _adBoost = Loc.Get("hud.ad_boost");

            Root = UiFactory.Rect(safeArea, "Hud", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -hud.topBarHeight - 12f), new Vector2(-16f, -12f));
            UiFactory.Image(Root, hud.panel, assets.rounded);

            _moneyIcon = UiFactory.Anchored(Root, "MoneyIcon", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -26f), new Vector2(90f, 56f));
            UiFactory.Image(_moneyIcon, juice.billColor, assets.bill);
            var moneyRt = UiFactory.Anchored(Root, "Money", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(136f, -14f), new Vector2(520f, 80f));
            _money = UiFactory.Text(moneyRt, hud, hud.fontSizeLarge, hud.money, TextAlignmentOptions.Left);

            var caurisIcon = UiFactory.Anchored(Root, "CaurisIcon", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -30f), new Vector2(52f, 52f));
            UiFactory.Image(caurisIcon, hud.cauris, assets.circle);
            var caurisRt = UiFactory.Anchored(Root, "Cauris", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(150f, 64f));
            _cauris = UiFactory.Text(caurisRt, hud, hud.fontSizeMedium, hud.cauris, TextAlignmentOptions.Right, "0");

            var incomeRt = UiFactory.Anchored(Root, "Income", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(136f, -92f), new Vector2(480f, 44f));
            _income = UiFactory.Text(incomeRt, hud, hud.fontSizeSmall, hud.mutedText, TextAlignmentOptions.Left);
            var boostRt = UiFactory.Anchored(Root, "Boost", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -92f), new Vector2(420f, 44f));
            _boost = UiFactory.Text(boostRt, hud, hud.fontSizeSmall, hud.accent, TextAlignmentOptions.Right);

            var goalBack = UiFactory.Rect(Root, "GoalBack", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 18f), new Vector2(-24f, 70f));
            UiFactory.Image(goalBack, hud.panelLight, assets.rounded);
            var goalFillRt = UiFactory.Stretch(goalBack, "GoalFill");
            goalFillRt.offsetMin = new Vector2(4f, 4f);
            goalFillRt.offsetMax = new Vector2(-4f, -4f);
            _goalFill = UiFactory.Image(goalFillRt, hud.accent, assets.rounded);
            _goalFill.type = Image.Type.Filled;
            _goalFill.fillMethod = Image.FillMethod.Horizontal;
            var goalLabelRt = UiFactory.Stretch(goalBack, "GoalLabel");
            _goalLabel = UiFactory.Text(goalLabelRt, hud, hud.fontSizeSmall * 0.9f, hud.text);
        }

        /// <summary>Un billet est arrivé : pulse du compteur.</summary>
        public void Pulse()
        {
            Juice.Punch(ref _pulse, _money.rectTransform, Vector3.one, _juice.counterPulseScale - 1f, 0.2f);
        }

        public void SetCauris(long cauris) => _cauris.SetText("{0}", cauris);

        public void Tick(float dt)
        {
            BigDouble target = _session.Economy.Money;
            if (!_initialized) { _displayed = target; _initialized = true; }
            // Interpolation exponentielle vers la vraie valeur (jamais au-dessus de l'argent réel en fin de course).
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, _juice.counterRollSeconds));
            BigDouble diff = target - _displayed;
            _displayed = (diff.IsZero || (diff.IsNegative ? -diff : diff) <= target * 1e-4) ? target : _displayed + diff * k;
            _moneyText.Clear().Append(_displayed).Append(" FCFA").ApplyIfChanged(_money);

            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = _hud.refreshInterval;

            var businesses = _session.Businesses;
            _incomeText.Clear().Append('+').Append(businesses.TotalIncomePerSecond()).Append(_perSecond).ApplyIfChanged(_income);

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
