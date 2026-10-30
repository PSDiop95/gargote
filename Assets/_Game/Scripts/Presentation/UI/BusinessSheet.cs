using System;
using Gargote.Core;
using Gargote.Systems;
using Gargote.Systems.Street;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Panneau bas rétractable : liste des commerces (niveau, barre « prochain palier », revenu, achat avec coût,
    /// gérant), sélecteur de quantité, « Acheter le meilleur », onglets (vides jusqu'à M3).
    /// N'émet que des intentions ; le presenter les transmet à l'économie.
    /// </summary>
    public sealed class BusinessSheet
    {
        private static readonly BuyMode[] Modes = { BuyMode.One, BuyMode.Ten, BuyMode.Hundred, BuyMode.Max, BuyMode.NextMilestone };

        private sealed class Row
        {
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI Name, Level, Income, Badge;
            public Image MilestoneFill;
            public UiButton Buy, Manager;
            public readonly TextBuffer LevelText = new TextBuffer(32), IncomeText = new TextBuffer(40),
                BuyText = new TextBuffer(48), ManagerText = new TextBuffer(48);
            public AffinityBadge ShownBadge = (AffinityBadge)(-1);
        }

        private readonly GameSession _session;
        private readonly HudConfig _hud;
        private readonly Row[] _rows;
        private readonly RectTransform _root, _content;
        private readonly ScrollRect _scroll;
        private readonly UiButton[] _modeButtons;
        private readonly UiButton _best;
        private readonly GameObject _comingSoon;
        private readonly string _levelPrefix, _open, _manager, _perSecond, _morning, _night;
        private float _refresh;
        private Tween _heightTween;
        private bool _expanded;
        private int _selected;

        public BuyMode Mode { get; private set; } = BuyMode.One;
        public bool Expanded => _expanded;
        public int Selected => _selected;
        public RectTransform Root => _root;

        public Action<int> BuyClicked;
        public Action<int> ManagerClicked;
        public Action<int> RowSelected;
        public Action BestClicked;

        public BusinessSheet(GameSession session, HudConfig hud, RectTransform safeArea, StreetAssets assets, Func<int, string> businessName)
        {
            _session = session;
            _hud = hud;
            _levelPrefix = Loc.Get("sheet.level");
            _open = Loc.Get("sheet.open");
            _manager = Loc.Get("sheet.manager");
            _perSecond = Loc.Get("hud.per_second");
            _morning = Loc.Get("badge.morning");
            _night = Loc.Get("badge.night");

            const float handle = 64f, toolbar = 140f, tabs = 150f;
            _root = UiFactory.Rect(safeArea, "Sheet", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.sizeDelta = new Vector2(0f, hud.sheetCollapsedHeight);
            UiFactory.Image(_root, hud.panel, assets.rounded, true);

            var handleRt = UiFactory.Rect(_root, "Handle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -handle), Vector2.zero);
            var handleButton = handleRt.gameObject.AddComponent<UiButton>();
            var handleBg = UiFactory.Image(handleRt, Color.clear, null, true);
            var gripRt = UiFactory.Anchored(handleRt, "Grip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 14f));
            UiFactory.Image(gripRt, hud.mutedText, assets.rounded);
            var gripLabel = UiFactory.Text(UiFactory.Stretch(handleRt, "HandleLabel"), hud, 1f, Color.clear);
            handleButton.Init(handleBg, gripLabel, hud);
            handleButton.EnabledColor = Color.clear;
            handleButton.DisabledColor = Color.clear;
            handleButton.PunchStrength = 0f;
            handleButton.Clicked = Toggle;

            // Liste (ScrollRect + masque) entre la poignée et la barre d'outils.
            var viewport = UiFactory.Rect(_root, "Viewport", Vector2.zero, Vector2.one, new Vector2(16f, toolbar + tabs), new Vector2(-16f, -handle));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Image(viewport, Color.clear, null, true);
            _content = UiFactory.Rect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _content.pivot = new Vector2(0.5f, 1f);
            int count = session.Businesses.Count;
            _content.sizeDelta = new Vector2(0f, count * hud.rowHeight);
            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.inertia = true;
            _scroll.scrollSensitivity = 30f;

            _rows = new Row[count];
            for (int i = 0; i < count; i++) _rows[i] = BuildRow(i, assets, businessName(i));

            // Barre d'outils : quantité + acheter le meilleur.
            var tool = UiFactory.Rect(_root, "Toolbar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, tabs), new Vector2(-16f, tabs + toolbar));
            _modeButtons = new UiButton[Modes.Length];
            string[] labels = { "x1", "x10", "x100", Loc.Get("sheet.max"), Loc.Get("sheet.next_milestone") };
            for (int m = 0; m < Modes.Length; m++)
            {
                int captured = m;
                float x0 = m * 0.12f, x1 = x0 + 0.115f;
                var cell = UiFactory.Rect(tool, "Mode" + m, new Vector2(x0, 0.08f), new Vector2(x1, 0.92f), Vector2.zero, Vector2.zero);
                var b = UiFactory.Button(cell, "Button", hud, assets.rounded, labels[m], hud.fontSizeSmall);
                b.Clicked = () => SetMode(Modes[captured]);
                _modeButtons[m] = b;
            }
            var bestCell = UiFactory.Rect(tool, "Best", new Vector2(0.62f, 0.08f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);
            _best = UiFactory.Button(bestCell, "Button", hud, assets.rounded, Loc.Get("sheet.buy_best"), hud.fontSizeSmall);
            _best.EnabledColor = hud.accent;
            _best.Background.color = hud.accent;
            _best.Clicked = () => BestClicked?.Invoke();

            // Onglets : Rue active, les autres annoncent M3.
            var tabBar = UiFactory.Rect(_root, "Tabs", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, tabs));
            UiFactory.Image(tabBar, hud.panelLight, null);
            string[] tabKeys = { "tab.street", "tab.managers", "tab.upgrades", "tab.cities", "tab.shop" };
            var soonRt = UiFactory.Rect(_root, "ComingSoon", Vector2.zero, Vector2.one, new Vector2(16f, toolbar + tabs), new Vector2(-16f, -handle));
            UiFactory.Image(soonRt, hud.panel, assets.rounded, true);
            UiFactory.Text(UiFactory.Stretch(soonRt, "Label"), hud, hud.fontSizeMedium, hud.mutedText, TextAlignmentOptions.Center,
                Loc.Get("tab.coming_soon"));
            _comingSoon = soonRt.gameObject;
            _comingSoon.SetActive(false);
            for (int t = 0; t < tabKeys.Length; t++)
            {
                int tab = t;
                var cell = UiFactory.Rect(tabBar, "Tab" + t, new Vector2(t / 5f, 0f), new Vector2((t + 1) / 5f, 1f), new Vector2(6f, 10f), new Vector2(-6f, -10f));
                var b = UiFactory.Button(cell, "Button", hud, assets.rounded, Loc.Get(tabKeys[t]), hud.fontSizeSmall * 0.9f);
                b.EnabledColor = t == 0 ? hud.accent : hud.panelLight;
                b.Background.color = b.EnabledColor;
                b.Clicked = () => _comingSoon.SetActive(tab != 0);
            }

            SetMode(BuyMode.One);
            Select(0, false);
        }

        private Row BuildRow(int index, StreetAssets assets, string name)
        {
            var hud = _hud;
            var row = new Row();
            row.Root = UiFactory.Rect(_content, "Row" + index, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -(index + 1) * hud.rowHeight + 8f), new Vector2(0f, -index * hud.rowHeight - 8f));
            row.Background = UiFactory.Image(row.Root, hud.panelLight, assets.rounded, true);
            var select = row.Root.gameObject.AddComponent<UiButton>();
            var selectLabel = UiFactory.Text(UiFactory.Stretch(row.Root, "SelectLabel"), hud, 1f, Color.clear);
            select.Init(row.Background, selectLabel, hud);
            select.EnabledColor = hud.panelLight;
            select.DisabledColor = hud.panelLight;
            select.PunchStrength = 0.02f;
            select.Clicked = () => { Select(index, true); RowSelected?.Invoke(index); };

            row.Name = UiFactory.Text(UiFactory.Anchored(row.Root, "Name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(460f, 56f)),
                hud, hud.fontSizeMedium, hud.text, TextAlignmentOptions.Left, name);
            row.Badge = UiFactory.Text(UiFactory.Anchored(row.Root, "Badge", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(220f, 40f)),
                hud, hud.fontSizeSmall * 0.85f, hud.accent, TextAlignmentOptions.Left);
            row.Level = UiFactory.Text(UiFactory.Anchored(row.Root, "Level", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -66f), new Vector2(220f, 40f)),
                hud, hud.fontSizeSmall, hud.mutedText, TextAlignmentOptions.Left);
            var barBack = UiFactory.Anchored(row.Root, "MilestoneBack", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 56f), new Vector2(440f, 22f));
            barBack.pivot = new Vector2(0f, 0.5f);
            UiFactory.Image(barBack, hud.panel, assets.rounded);
            var barFill = UiFactory.Stretch(barBack, "Fill");
            row.MilestoneFill = UiFactory.Image(barFill, hud.money, assets.rounded);
            row.MilestoneFill.type = Image.Type.Filled;
            row.MilestoneFill.fillMethod = Image.FillMethod.Horizontal;
            row.Income = UiFactory.Text(UiFactory.Anchored(row.Root, "Income", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 10f), new Vector2(440f, 40f)),
                hud, hud.fontSizeSmall, hud.money, TextAlignmentOptions.Left);
            row.Income.rectTransform.pivot = new Vector2(0f, 0f);

            var buyCell = UiFactory.Rect(row.Root, "BuyCell", new Vector2(0.7f, 0.1f), new Vector2(0.985f, 0.9f), Vector2.zero, Vector2.zero);
            row.Buy = UiFactory.Button(buyCell, "Buy", hud, assets.rounded, "", hud.fontSizeSmall);
            row.Buy.Clicked = () => { Select(index, false); BuyClicked?.Invoke(index); };
            var managerCell = UiFactory.Rect(row.Root, "ManagerCell", new Vector2(0.47f, 0.1f), new Vector2(0.69f, 0.9f), Vector2.zero, Vector2.zero);
            row.Manager = UiFactory.Button(managerCell, "Manager", hud, assets.rounded, "", hud.fontSizeSmall * 0.85f);
            row.Manager.EnabledColor = hud.accent;
            row.Manager.Clicked = () => ManagerClicked?.Invoke(index);
            return row;
        }

        public void SetMode(BuyMode mode)
        {
            Mode = mode;
            for (int m = 0; m < Modes.Length; m++)
            {
                var b = _modeButtons[m];
                b.EnabledColor = Modes[m] == mode ? _hud.accent : _hud.buttonDisabled;
                b.Background.color = b.EnabledColor;
            }
            _refresh = 0f;
        }

        public void Toggle() => SetExpanded(!_expanded);

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_heightTween.isAlive) _heightTween.Stop();
            float target = expanded ? _hud.sheetExpandedHeight : _hud.sheetCollapsedHeight;
            _heightTween = Tween.Custom(this, _root.sizeDelta.y, target, _hud.sheetAnimSeconds,
                (sheet, h) => sheet._root.sizeDelta = new Vector2(0f, h), Ease.OutCubic);
            if (!expanded) ScrollTo(_selected);
        }

        /// <summary>Sélectionne un commerce (surligné, visible en mode replié).</summary>
        public void Select(int index, bool scroll)
        {
            if (index < 0 || index >= _rows.Length) return;
            _rows[_selected].Background.color = _hud.panelLight;
            _selected = index;
            _rows[index].Background.color = Color.Lerp(_hud.panelLight, _hud.accent, 0.35f);
            if (scroll || !_expanded) ScrollTo(index);
        }

        private void ScrollTo(int index)
        {
            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(0f, index * _hud.rowHeight);
        }

        /// <summary>Rafraîchit (au plus toutes les refreshInterval) textes, coûts et disponibilité des boutons.</summary>
        public void Tick(float dt, double hourOfDay, DayNightConfig dayNight, bool force = false)
        {
            _refresh -= dt;
            if (_refresh > 0f && !force) return;
            _refresh = _hud.refreshInterval;
            var businesses = _session.Businesses;
            var milestones = _session.EconomyConfig.Milestones;
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                var s = businesses[i];
                bool unlocked = s.IsUnlocked;

                row.LevelText.Clear();
                if (unlocked) row.LevelText.Append(_levelPrefix).Append(' ').AppendLong(s.Level);
                row.LevelText.ApplyIfChanged(row.Level);

                long next = EconomyMath.NextMilestone(s.Level, milestones);
                long previous = 0;
                for (int m = 0; m < milestones.Length && milestones[m] <= s.Level; m++) previous = milestones[m];
                row.MilestoneFill.fillAmount = next > previous ? (float)(s.Level - previous) / (next - previous) : 1f;

                row.IncomeText.Clear();
                if (unlocked)
                {
                    double dn = DayNight.Multiplier(s.Definition.Affinity, hourOfDay, dayNight);
                    row.IncomeText.Append('+').Append(s.RevenuePerSecond * (dn * _session.Economy.GlobalRevenueMultiplier * businesses.AllBusinessesMultiplier)).Append(_perSecond);
                }
                row.IncomeText.ApplyIfChanged(row.Income);

                var badge = DayNightBadges.For(s.Definition.Affinity, hourOfDay, dayNight);
                if (badge != row.ShownBadge)
                {
                    row.ShownBadge = badge;
                    row.Badge.text = badge == AffinityBadge.MorningActive ? _morning : badge == AffinityBadge.NightActive ? _night : string.Empty;
                }

                var quote = businesses.Quote(i, Mode);
                if (quote.Count <= 0) quote = businesses.QuoteLevels(i, 1);
                row.BuyText.Clear();
                if (!unlocked) row.BuyText.Append(_open);
                else row.BuyText.Append('x').AppendLong(quote.Count);
                row.BuyText.Append('\n').Append(quote.Cost).ApplyIfChanged(row.Buy.Label);
                row.Buy.SetInteractable(quote.Affordable && quote.Count > 0);

                bool showManager = unlocked && !s.HasManager;
                if (row.Manager.gameObject.activeSelf != showManager) row.Manager.gameObject.SetActive(showManager);
                if (showManager)
                {
                    BigDouble cost = businesses.ManagerCost(i);
                    row.ManagerText.Clear().Append(_manager).Append('\n').Append(cost).ApplyIfChanged(row.Manager.Label);
                    row.Manager.SetInteractable(businesses.CanHireManager(i));
                }
            }

            var best = PurchaseAdvisor.FindBestLevels(businesses, _session.Economy, _session.EconomyConfig);
            _best.SetInteractable(best.IsValid && _session.Economy.CanAfford(best.Cost));
        }

        /// <summary>Position écran du bouton d'achat d'un commerce (onboarding futur, tests).</summary>
        public UiButton BuyButton(int index) => _rows[index].Buy;
    }
}
