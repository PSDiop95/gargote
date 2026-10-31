using System;
using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>Ligne de liste commune aux onglets : nom, sous-titre, badge, bouton d'action, point rouge.</summary>
    internal sealed class ListRow
    {
        public RectTransform Rt;
        public Image Back, Dot;
        public TextMeshProUGUI Name, Sub, Badge;
        public UiButton Action;

        public static ListRow Build(RectTransform content, int index, float height, HudConfig hud, StreetAssets assets, string name, float buttonWidth = 0.3f)
        {
            var r = new ListRow { Rt = MetaUiKit.Row(content, index, height, hud, assets, name) };
            r.Back = r.Rt.GetComponent<Image>();
            float right = 1f - buttonWidth - 0.02f;
            r.Name = MetaUiKit.Label(r.Rt, "Name", hud, hud.fontSizeSmall * 0.9f, hud.text, new Vector2(0f, 0.5f), new Vector2(right, 1f),
                new Vector2(24f, 0f), new Vector2(0f, -8f));
            r.Sub = MetaUiKit.Label(r.Rt, "Sub", hud, hud.fontSizeSmall * 0.72f, hud.mutedText, new Vector2(0f, 0f), new Vector2(right, 0.5f),
                new Vector2(24f, 8f), Vector2.zero);
            // Badge au-dessus du bouton (jamais sur le nom) : le bouton se tasse quand un badge est affiché.
            r.Badge = MetaUiKit.Outline(MetaUiKit.Label(r.Rt, "Badge", hud, hud.fontSizeSmall * 0.6f, hud.accent, new Vector2(right + 0.02f, 0.7f),
                new Vector2(0.98f, 1f), Vector2.zero, new Vector2(0f, -4f), TextAlignmentOptions.Center));
            r.Action = MetaUiKit.Button(r.Rt, "Action", hud, assets, "", hud.fontSizeSmall * 0.8f, new Vector2(right + 0.02f, 0.14f), new Vector2(0.98f, 0.86f),
                Vector2.zero, Vector2.zero);
            r._cell = (RectTransform)r.Action.transform.parent;
            r.Dot = MetaUiKit.RedDot(r.Rt, assets, 30f);
            return r;
        }

        private RectTransform _cell;

        public void SetVisible(bool on) { if (Rt.gameObject.activeSelf != on) Rt.gameObject.SetActive(on); }

        /// <summary>Badge (« NOUVEAU ») au-dessus du bouton ; vide = bouton pleine hauteur.</summary>
        public void SetBadge(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            Badge.text = on ? text : string.Empty;
            var max = _cell.anchorMax;
            float top = on ? 0.7f : 0.86f;
            if (!Mathf.Approximately(max.y, top)) _cell.anchorMax = new Vector2(max.x, top);
            var min = _cell.anchorMin;
            float bottom = on ? 0.08f : 0.14f;
            if (!Mathf.Approximately(min.y, bottom)) _cell.anchorMin = new Vector2(min.x, bottom);
        }
    }

    /// <summary>Petit sélecteur à deux onglets en haut d'une carte (FCFA / Étoiles).</summary>
    internal sealed class SubTabs
    {
        private readonly UiButton[] _buttons;
        private readonly HudConfig _hud;
        public int Selected { get; private set; }
        public Action<int> Changed;

        public SubTabs(RectTransform card, HudConfig hud, StreetAssets assets, float top, params string[] labels)
        {
            _hud = hud;
            _buttons = new UiButton[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                float x0 = i / (float)labels.Length, x1 = (i + 1) / (float)labels.Length;
                _buttons[i] = MetaUiKit.Button(card, "Sub" + i, hud, assets, labels[i], hud.fontSizeSmall * 0.85f, new Vector2(x0, 1f), new Vector2(x1, 1f),
                    new Vector2(30f, -top - 96f), new Vector2(-30f, -top));
                _buttons[i].Clicked = () => Select(index);
            }
            Select(0);
        }

        public UiButton Button(int i) => _buttons[i];

        public void Select(int index)
        {
            Selected = index;
            for (int i = 0; i < _buttons.Length; i++)
            {
                var c = i == index ? _hud.accent : _hud.panelLight;
                _buttons[i].EnabledColor = c;
                _buttons[i].Background.color = c;
            }
            Changed?.Invoke(index);
        }
    }

    /// <summary>
    /// Onglet Upgrades : FCFA de la ville (badge NOUVEAU tant que non vu, achetables d'abord) et upgrades d'Étoiles
    /// permanents (dévoilés par le niveau joueur). Les upgrades vus sont marqués à la fermeture.
    /// </summary>
    public sealed class UpgradesScreen : MetaScreen
    {
        private const float RowHeight = 150f;
        private readonly MetaUiContext _ctx;
        private readonly SubTabs _tabs;
        private readonly RectTransform _cashContent, _starContent;
        private readonly ListRow[] _cash, _stars;
        private readonly int[] _cashOrder;
        private readonly TextMeshProUGUI _starsBalance;
        private readonly string _new, _owned, _all;
        private float _refresh;

        public UpgradesScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Upgrades", ctx.Hud, ctx.Assets, new Vector2(1000f, 1640f), Loc.Get("upgrades.title"))
        {
            _ctx = ctx;
            _new = Loc.Get("upgrades.new");
            _owned = Loc.Get("upgrades.owned");
            _all = Loc.Get("upgrades.all_business");
            _tabs = new SubTabs(Card, Hud, Assets, 120f, Loc.Get("upgrades.cash"), Loc.Get("upgrades.stars"));
            _starsBalance = MetaUiKit.Label(Card, "Stars", Hud, Hud.fontSizeSmall * 0.85f, Hud.money, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -290f), new Vector2(-40f, -226f), TextAlignmentOptions.Center);

            int maxCash = 0;
            foreach (var c in ctx.Session.Definitions.Cities) maxCash = Math.Max(maxCash, c.Upgrades.Length);
            _cashContent = MetaUiKit.ScrollList(Card, "Cash", new Vector2(24f, 24f), new Vector2(-24f, -300f), out _);
            _cashContent.sizeDelta = new Vector2(0f, maxCash * RowHeight);
            _cash = new ListRow[maxCash];
            _cashOrder = new int[maxCash];
            for (int i = 0; i < maxCash; i++)
            {
                int slot = i;
                _cash[i] = ListRow.Build(_cashContent, i, RowHeight, Hud, Assets, "Cash");
                _cash[i].Action.Clicked = () => BuyCash(_cashOrder[slot]);
            }

            var prestige = ctx.Session.Prestige;
            _starContent = MetaUiKit.ScrollList(Card, "StarList", new Vector2(24f, 24f), new Vector2(-24f, -300f), out _);
            _starContent.sizeDelta = new Vector2(0f, prestige.UpgradeCount * RowHeight);
            _stars = new ListRow[prestige.UpgradeCount];
            for (int i = 0; i < _stars.Length; i++)
            {
                int index = i;
                _stars[i] = ListRow.Build(_starContent, i, RowHeight, Hud, Assets, "Star");
                _stars[i].Action.Clicked = () => BuyStar(index);
            }
            _tabs.Changed = _ => Refresh();
        }

        private void BuyCash(int index)
        {
            var businesses = _ctx.Session.Businesses;
            if (!businesses.BuyUpgrade(index)) return;
            _ctx.Meta.MarkUpgradeSeen(businesses.Upgrade(index).Id);
            _ctx.Audio?.Play(SoundId.Buy);
            Refresh();
        }

        private void BuyStar(int index)
        {
            if (!_ctx.Session.BuyStarUpgrade(index)) return;
            _ctx.Audio?.Play(SoundId.Milestone);
            Refresh();
        }

        protected override void OnHidden()
        {
            var businesses = _ctx.Session.Businesses;
            for (int i = 0; i < businesses.UpgradeCount; i++)
                if (businesses.IsUpgradeAvailable(i)) _ctx.Meta.MarkUpgradeSeen(businesses.Upgrade(i).Id);
        }

        public override void Refresh()
        {
            bool starsOpen = _ctx.Meta.IsUnlocked(Feature.StarUpgrades);
            _tabs.Button(1).transform.parent.gameObject.SetActive(starsOpen);
            if (!starsOpen && _tabs.Selected == 1) { _tabs.Select(0); return; }
            bool cash = _tabs.Selected == 0;
            _cashContent.parent.gameObject.SetActive(cash);
            _starContent.parent.gameObject.SetActive(!cash);
            _starsBalance.gameObject.SetActive(!cash);
            if (cash) RefreshCash(); else RefreshStars();
        }

        private int CashRank(int i)
        {
            var b = _ctx.Session.Businesses;
            if (b.OwnsUpgrade(i)) return 3;
            if (!b.IsUpgradeAvailable(i)) return 2;
            return _ctx.Session.Economy.CanAfford(b.UpgradeCost(i)) ? 0 : 1;
        }

        private void RefreshCash()
        {
            var b = _ctx.Session.Businesses;
            int n = b.UpgradeCount;
            // Achetables, puis disponibles, puis verrouillés, puis achetés ; à coût croissant (tri par insertion, à l'ouverture).
            for (int i = 0; i < n; i++) _cashOrder[i] = i;
            for (int i = 1; i < n; i++)
            {
                int v = _cashOrder[i], j = i - 1;
                while (j >= 0 && Before(v, _cashOrder[j])) { _cashOrder[j + 1] = _cashOrder[j]; j--; }
                _cashOrder[j + 1] = v;
            }
            for (int slot = 0; slot < _cash.Length; slot++)
            {
                var row = _cash[slot];
                row.SetVisible(slot < n);
                if (slot >= n) continue;
                int i = _cashOrder[slot];
                var u = b.Upgrade(i);
                bool owned = b.OwnsUpgrade(i), available = b.IsUpgradeAvailable(i);
                bool affordable = available && _ctx.Session.Economy.CanAfford(b.UpgradeCost(i));
                row.Name.text = Loc.Get("upgrade." + u.Id);
                row.Sub.text = (u.BusinessIndex < 0 ? _all : _ctx.BusinessName(b[u.BusinessIndex].Definition.Id)) + "  x" + u.RevenueMultiplier.ToString("0.##");
                bool isNew = available && !_ctx.Meta.IsUpgradeSeen(u.Id);
                row.SetBadge(isNew ? _new : null);
                row.Action.Label.text = owned ? _owned : NumberFormatter.Format(b.UpgradeCost(i)) + " F";
                row.Action.SetInteractable(affordable);
                row.Back.color = owned ? Hud.panel : Hud.panelLight;
                MetaUiKit.SetDot(row.Dot, isNew && affordable);
            }
            _cashContent.sizeDelta = new Vector2(0f, n * RowHeight);
        }

        private bool Before(int a, int b)
        {
            int ra = CashRank(a), rb = CashRank(b);
            if (ra != rb) return ra < rb;
            var bs = _ctx.Session.Businesses;
            return bs.Upgrade(a).Cost < bs.Upgrade(b).Cost;
        }

        private void RefreshStars()
        {
            var p = _ctx.Session.Prestige;
            _starsBalance.text = new LocTemplate("upgrades.stars_balance").Format(p.AvailableStars);
            for (int i = 0; i < _stars.Length; i++)
            {
                var u = p.Upgrade(i);
                var row = _stars[i];
                bool owned = p.Owns(i);
                row.Name.text = Loc.Get("star_upgrade." + u.Id);
                row.Sub.text = MetaText.StarEffect(u);
                row.SetBadge(null);
                row.Action.Label.text = owned ? _owned : new LocTemplate("upgrades.star_cost").Format(u.CostStars);
                row.Action.SetInteractable(p.CanBuyUpgrade(i));
                row.Back.color = owned ? Hud.panel : Hud.panelLight;
                MetaUiKit.SetDot(row.Dot, p.CanBuyUpgrade(i));
            }
        }

        public override void Tick(float dt)
        {
            if (!IsOpen) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 0.5f;
            Refresh();
        }
    }

    /// <summary>
    /// Onglet Villes : ville actuelle (objectif, Relancer / Voyager, deux actions distinctes), villes visitées
    /// (revenu passif réduit, consultable), villes à venir (objectif à atteindre).
    /// </summary>
    public sealed class CitiesScreen : MetaScreen
    {
        private const float RowHeight = 210f;
        private readonly MetaUiContext _ctx;
        private readonly PrestigeScreen _prestige;
        private readonly ListRow[] _rows;
        private readonly UiButton[] _restart;
        private readonly Image _goal;
        private readonly TextMeshProUGUI _difference;
        private readonly string _here = Loc.Get("cities.current");
        private float _refresh;

        public CitiesScreen(RectTransform parent, MetaUiContext ctx, PrestigeScreen prestige)
            : base(parent, "Cities", ctx.Hud, ctx.Assets,
                new Vector2(1000f, Mathf.Min(1500f, 300f + ctx.Session.Definitions.Cities.Length * RowHeight)), Loc.Get("cities.title"))
        {
            _ctx = ctx;
            _prestige = prestige;
            var cities = ctx.Session.Definitions.Cities;
            _difference = MetaUiKit.Paragraph(Card, "Difference", Hud, Hud.fontSizeSmall * 0.75f, Hud.mutedText, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, 24f), new Vector2(-40f, 150f), TextAlignmentOptions.Center);
            _difference.text = Loc.Get("prestige.difference");
            var content = MetaUiKit.ScrollList(Card, "List", new Vector2(24f, 160f), new Vector2(-24f, -120f), out _);
            content.sizeDelta = new Vector2(0f, cities.Length * RowHeight);
            _rows = new ListRow[cities.Length];
            _restart = new UiButton[cities.Length];
            for (int i = 0; i < cities.Length; i++)
            {
                _rows[i] = ListRow.Build(content, i, RowHeight, Hud, Assets, "City", 0.28f);
                _rows[i].Action.Clicked = () => _prestige.Show(true);
                // Relancer : à gauche du bouton Voyager, uniquement sur la ville actuelle.
                _restart[i] = MetaUiKit.Button(_rows[i].Rt, "Restart", Hud, Assets, Loc.Get("cities.restart"), Hud.fontSizeSmall * 0.75f,
                    new Vector2(0.44f, 0.14f), new Vector2(0.69f, 0.86f), Vector2.zero, Vector2.zero, Hud.panel);
                _restart[i].Clicked = () => _prestige.Show(false);
                if (i == 0)
                    _goal = MetaUiKit.Bar(_rows[0].Rt, "Goal", Hud, Assets, Hud.money, new Vector2(0f, 0.04f), new Vector2(0.42f, 0.16f),
                        new Vector2(24f, 0f), Vector2.zero);
            }
            _prestige.Closed += () => { if (IsOpen) Refresh(); };
        }

        public override void Refresh()
        {
            var s = _ctx.Session;
            var cities = s.Definitions.Cities;
            double ratio = s.Definitions.Prestige.VisitedCityIncomeRatio;
            for (int i = 0; i < cities.Length; i++)
            {
                var row = _rows[i];
                string name = MetaText.CityName(cities[i].Id);
                bool current = i == s.CityIndex, visited = i < s.CityIndex;
                row.Name.text = name;
                row.Back.color = current ? Hud.panelLight : Hud.panel;
                _restart[i].transform.parent.gameObject.SetActive(current);
                if (current)
                {
                    row.SetBadge(null);
                    row.Name.text = name + "  <size=70%><color=#" + ColorUtility.ToHtmlStringRGB(Hud.accent) + ">" + _here + "</color></size>";
                    row.Sub.text = new LocTemplate("cities.goal").Format(NumberFormatter.Format(s.Economy.CityLifetimeEarnings),
                        NumberFormatter.Format(BigDouble.FromDouble(cities[i].GoalLifetimeEarnings)));
                    bool canTravel = s.HasNextCity && s.IsCityGoalReached;
                    row.Action.transform.parent.gameObject.SetActive(s.HasNextCity);
                    row.Action.Label.text = s.HasNextCity ? new LocTemplate("prestige.travel_title").Format(MetaText.CityName(cities[i + 1].Id)) : string.Empty;
                    row.Action.SetInteractable(canTravel);
                    _restart[i].SetInteractable(s.CanPrestige);
                    MetaUiKit.SetDot(row.Dot, canTravel);
                    if (_goal != null)
                    {
                        _goal.transform.parent.SetParent(row.Rt, false);
                        _goal.fillAmount = Mathf.Clamp01((float)s.CityGoalProgress01);
                    }
                }
                else if (visited)
                {
                    row.SetBadge(null);
                    var v = FindVisited(cities[i].Id);
                    var passive = v != null ? v.ManagedIncomePerSecond * ratio : BigDouble.Zero;
                    row.Sub.text = new LocTemplate("cities.passive").Format(NumberFormatter.Format(passive));
                    row.Action.transform.parent.gameObject.SetActive(false);
                    MetaUiKit.SetDot(row.Dot, false);
                }
                else
                {
                    row.SetBadge(null);
                    row.Sub.text = new LocTemplate("cities.locked").Format(NumberFormatter.Format(BigDouble.FromDouble(cities[i - 1].GoalLifetimeEarnings)));
                    row.Action.transform.parent.gameObject.SetActive(false);
                    MetaUiKit.SetDot(row.Dot, false);
                }
            }
        }

        private Gargote.Systems.VisitedCity FindVisited(string id)
        {
            var list = _ctx.Session.VisitedCities;
            for (int i = 0; i < list.Count; i++) if (list[i].CityId == id) return list[i];
            return null;
        }

        public override void Tick(float dt)
        {
            if (!IsOpen) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 0.5f;
            Refresh();
        }
    }

    /// <summary>
    /// Écran de prestige (§4.2) : « Tu vas gagner N étoiles → revenus x a → x b », ce qu'on garde, ce qui repart à zéro,
    /// confirmation ou « Pas encore ». Deux modes distincts : Relancer la ville / Voyager vers la suivante.
    /// </summary>
    public sealed class PrestigeScreen : MetaScreen
    {
        private readonly MetaUiContext _ctx;
        private readonly TextMeshProUGUI _gain, _multiplier, _keep, _reset, _text;
        private readonly UiButton _confirm, _notYet;
        private bool _travel;

        /// <summary>Après confirmation : (ville de départ, ville d'arrivée, a voyagé).</summary>
        public Action<string, string, bool> Performed;

        public PrestigeScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Prestige", ctx.Hud, ctx.Assets, new Vector2(960f, 1400f), "", false)
        {
            _ctx = ctx;
            _text = MetaUiKit.Paragraph(Card, "Text", Hud, Hud.fontSizeSmall * 0.8f, Hud.mutedText, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(50f, -260f), new Vector2(-50f, -120f), TextAlignmentOptions.Center);
            _gain = MetaUiKit.Outline(MetaUiKit.Label(Card, "Gain", Hud, Hud.fontSizeLarge * 0.7f, Hud.money, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -400f), new Vector2(-30f, -270f), TextAlignmentOptions.Center));
            _multiplier = MetaUiKit.Label(Card, "Multiplier", Hud, Hud.fontSizeMedium, Hud.text, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -490f), new Vector2(-30f, -410f), TextAlignmentOptions.Center);
            _keep = MetaUiKit.Paragraph(Card, "Keep", Hud, Hud.fontSizeSmall * 0.75f, Hud.text, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(60f, -740f), new Vector2(-60f, -520f), TextAlignmentOptions.TopLeft);
            _reset = MetaUiKit.Paragraph(Card, "Reset", Hud, Hud.fontSizeSmall * 0.75f, Hud.text, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(60f, -960f), new Vector2(-60f, -760f), TextAlignmentOptions.TopLeft);
            _confirm = MetaUiKit.Button(Card, "Confirm", Hud, Assets, "", Hud.fontSizeMedium * 0.9f, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f),
                new Vector2(0f, 200f), new Vector2(0f, 330f), Hud.accent);
            _confirm.Clicked = Confirm;
            _notYet = MetaUiKit.Button(Card, "NotYet", Hud, Assets, Loc.Get("prestige.not_yet"), Hud.fontSizeSmall, new Vector2(0.25f, 0f), new Vector2(0.75f, 0f),
                new Vector2(0f, 50f), new Vector2(0f, 170f), Hud.panelLight);
            _notYet.Clicked = Hide;
        }

        public void Show(bool travel)
        {
            _travel = travel;
            Show();
        }

        private void Confirm()
        {
            var s = _ctx.Session;
            string from = s.City.Id;
            bool travel = _travel && s.HasNextCity && s.IsCityGoalReached;
            if (!travel && !s.CanPrestige) return;
            if (!s.PerformPrestige(travel)) return;
            Hide();
            Performed?.Invoke(from, s.City.Id, travel);
        }

        public override void Refresh()
        {
            var s = _ctx.Session;
            var cities = s.Definitions.Cities;
            string here = MetaText.CityName(s.City.Id);
            bool travel = _travel && s.HasNextCity;
            string next = travel ? MetaText.CityName(cities[s.CityIndex + 1].Id) : here;
            Title.text = travel ? new LocTemplate("prestige.travel_title").Format(next) : new LocTemplate("prestige.title").Format(here);
            _text.text = travel ? Loc.Get("prestige.travel_text") : Loc.Get("prestige.difference");
            long stars = s.StarsToGain;
            _gain.text = new LocTemplate("prestige.gain").Format(stars);
            double before = s.Prestige.RevenueMultiplier, after = s.Prestige.MultiplierAfter(s.Economy.CityLifetimeEarnings);
            _multiplier.text = new LocTemplate("prestige.multiplier").Format(before.ToString("0.##"), after.ToString("0.##"));
            _keep.text = "<b>" + Loc.Get("prestige.keep") + "</b>\n" + Loc.Get("prestige.keep_list");
            _reset.text = "<b>" + Loc.Get("prestige.reset") + "</b>\n" + Loc.Get("prestige.reset_list");
            _confirm.Label.text = travel ? new LocTemplate("prestige.travel_confirm").Format(next) : Loc.Get("prestige.confirm");
            _confirm.SetInteractable(travel ? s.IsCityGoalReached : s.CanPrestige);
        }
    }

    /// <summary>
    /// Cinématique de voyage (§9.3, 5 s) : carte stylisée, le car rapide part de la ville de départ vers la suivante en arc.
    /// « Passer » apparaît après 1 s. À l'arrivée : bannière « Bienvenue à … ! ».
    /// </summary>
    public sealed class TravelCinematic : MetaScreen
    {
        private const float Duration = 5f, SkipAfter = 1f, BannerSeconds = 1.6f;
        private readonly MetaUiContext _ctx;
        private readonly RectTransform _from, _to, _bus;
        private readonly TextMeshProUGUI _fromLabel, _toLabel, _banner;
        private readonly UiButton _skip;
        private readonly Image[] _dashes = new Image[14];
        private float _t, _bannerLeft;
        private bool _arrived;
        private string _arrival;

        public TravelCinematic(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Travel", ctx.Hud, ctx.Assets, new Vector2(1000f, 1300f), "", false)
        {
            _ctx = ctx;
            var sea = UiFactory.Stretch(Card, "Sea");
            sea.offsetMin = new Vector2(20f, 20f);
            sea.offsetMax = new Vector2(-20f, -20f);
            UiFactory.Image(sea, new Color(0.16f, 0.42f, 0.62f), Assets.rounded);
            var land = UiFactory.Anchored(sea, "Land", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 60f), new Vector2(820f, 900f));
            UiFactory.Image(land, new Color(0.86f, 0.62f, 0.32f), Assets.rounded);
            for (int i = 0; i < _dashes.Length; i++)
            {
                var d = UiFactory.Anchored(Card, "Dash" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
                _dashes[i] = UiFactory.Image(d, new Color(1f, 1f, 1f, 0.8f), Assets.circle);
            }
            _from = Pin("From", new Vector2(-330f, 260f), out _fromLabel);
            _to = Pin("To", new Vector2(250f, -330f), out _toLabel);
            _bus = UiFactory.Anchored(Card, "CarRapide", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 70f));
            UiFactory.Image(_bus, new Color(1f, 0.82f, 0.18f), Assets.rounded);
            var stripe = UiFactory.Anchored(_bus, "Stripe", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 18f));
            UiFactory.Image(stripe, new Color(0.15f, 0.45f, 0.75f), Assets.rounded);
            _banner = MetaUiKit.Outline(MetaUiKit.Label(Card, "Banner", Hud, Hud.fontSizeLarge * 0.75f, Hud.money, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -220f), new Vector2(-30f, -60f), TextAlignmentOptions.Center));
            _skip = MetaUiKit.Button(Card, "Skip", Hud, Assets, Loc.Get("travel.skip"), Hud.fontSizeSmall, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-260f, 40f), new Vector2(-40f, 140f), Hud.panelLight);
            _skip.Clicked = Arrive;
        }

        private RectTransform Pin(string name, Vector2 pos, out TextMeshProUGUI label)
        {
            var rt = UiFactory.Anchored(Card, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(60f, 60f));
            UiFactory.Image(rt, new Color(0.85f, 0.2f, 0.18f), Assets.circle);
            label = MetaUiKit.Outline(MetaUiKit.Label(rt, "Label", Hud, Hud.fontSizeSmall, Hud.text, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200f, 0f), new Vector2(200f, 70f), TextAlignmentOptions.Center));
            return rt;
        }

        public void Play(string fromCity, string toCity)
        {
            _fromLabel.text = MetaText.CityName(fromCity);
            _toLabel.text = MetaText.CityName(toCity);
            _arrival = new LocTemplate("travel.arrival").Format(MetaText.CityName(toCity));
            _t = 0f;
            _arrived = false;
            Show();
            _ctx.Audio?.Play(SoundId.Horn);
        }

        private Vector2 PathAt(float u)
        {
            Vector2 a = _from.anchoredPosition, b = _to.anchoredPosition;
            var mid = (a + b) * 0.5f + new Vector2(260f, 160f);
            float v = 1f - u;
            return v * v * a + 2f * v * u * mid + u * u * b;
        }

        private void Arrive()
        {
            if (_arrived) return;
            _arrived = true;
            _t = Duration;
            _bus.anchoredPosition = PathAt(1f);
            _banner.text = _arrival;
            _bannerLeft = BannerSeconds;
            _skip.transform.parent.gameObject.SetActive(false);
            _ctx.Audio?.Play(SoundId.Unlock);
        }

        public override void Refresh()
        {
            _banner.text = string.Empty;
            for (int i = 0; i < _dashes.Length; i++) ((RectTransform)_dashes[i].transform).anchoredPosition = PathAt((i + 0.5f) / _dashes.Length);
            _bus.anchoredPosition = PathAt(0f);
            _skip.transform.parent.gameObject.SetActive(false);
        }

        public override void Tick(float dt)
        {
            if (!IsOpen) return;
            if (_arrived)
            {
                _bannerLeft -= dt;
                if (_bannerLeft <= 0f) Hide();
                return;
            }
            _t += dt;
            if (_t >= SkipAfter && !_skip.transform.parent.gameObject.activeSelf) _skip.transform.parent.gameObject.SetActive(true);
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / Duration));
            _bus.anchoredPosition = PathAt(u);
            var ahead = PathAt(Mathf.Min(1f, u + 0.01f)) - _bus.anchoredPosition;
            _bus.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg * 0.25f);
            if (_t >= Duration) Arrive();
        }
    }

    /// <summary>
    /// Boutique (mock jusqu'à M5) : coffre gratuit, coffre bonus (rewarded), coffres en cauris avec bouton « i »
    /// (probabilités), extension du plafond hors ligne, offres IAP annoncées « Bientôt ».
    /// </summary>
    public sealed class ShopScreen : MetaScreen
    {
        private const float RowHeight = 170f;
        private readonly MetaUiContext _ctx;
        private readonly ListRow _free, _rewarded, _offline;
        private readonly ListRow[] _chests;
        private readonly string[] _chestIds;
        private readonly TextMeshProUGUI _balance;
        private float _refresh;

        public ShopScreen(RectTransform parent, MetaUiContext ctx, ChestOddsScreen odds)
            : base(parent, "Shop", ctx.Hud, ctx.Assets, new Vector2(1000f, 1640f), Loc.Get("shop.title"))
        {
            _ctx = ctx;
            _balance = MetaUiKit.Label(Card, "Balance", Hud, Hud.fontSizeSmall * 0.9f, Hud.cauris, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -180f), new Vector2(-40f, -110f), TextAlignmentOptions.Center);
            var defs = ctx.Meta.Definitions.Chests;
            int buyable = 0;
            foreach (var c in defs) if (c.CostCauris > 0) buyable++;
            string[] offers = { "shop.starter_pack", "shop.cauris_pack", "shop.no_ads" };
            int rows = 3 + buyable + offers.Length;
            var content = MetaUiKit.ScrollList(Card, "List", new Vector2(24f, 24f), new Vector2(-24f, -190f), out _);
            content.sizeDelta = new Vector2(0f, rows * RowHeight);
            int r = 0;
            _free = ListRow.Build(content, r++, RowHeight, Hud, Assets, "Free");
            _free.Action.Clicked = () => { if (_ctx.Meta.CanClaimFreeChest) { _ctx.Meta.ClaimFreeChest(); Refresh(); } };
            _rewarded = ListRow.Build(content, r++, RowHeight, Hud, Assets, "Rewarded");
            _rewarded.Action.Clicked = () => _ctx.Rewarded("chest_bonus", () => { _ctx.Meta.ClaimRewardedChest(); Refresh(); });
            _chests = new ListRow[buyable];
            _chestIds = new string[buyable];
            int k = 0;
            foreach (var c in defs)
            {
                if (c.CostCauris <= 0) continue;
                string id = c.Id;
                var row = ListRow.Build(content, r++, RowHeight, Hud, Assets, "Chest", 0.26f);
                row.Action.Clicked = () => { if (_ctx.Meta.BuyChest(id) != null) { _ctx.Audio?.Play(SoundId.Buy); Refresh(); } };
                var info = MetaUiKit.Button(row.Rt, "Info", Hud, Assets, "i", Hud.fontSizeSmall, new Vector2(0.6f, 0.5f), new Vector2(0.6f, 0.5f),
                    new Vector2(-50f, -45f), new Vector2(40f, 45f), Hud.panel);
                info.Clicked = () => odds.Show(id);
                _chests[k] = row;
                _chestIds[k++] = id;
            }
            _offline = ListRow.Build(content, r++, RowHeight, Hud, Assets, "Offline");
            _offline.Action.Clicked = () => { if (_ctx.Meta.ExtendOfflineCap()) { _ctx.Audio?.Play(SoundId.Buy); Refresh(); } };
            string soon = Loc.Get("shop.soon");
            foreach (var key in offers)
            {
                var row = ListRow.Build(content, r++, RowHeight, Hud, Assets, "Offer");
                row.Name.text = Loc.Get(key);
                row.Sub.text = Loc.Get("shop.offers");
                row.Action.Label.text = soon;
                row.Action.SetInteractable(false);
                row.Back.color = Hud.panel;
            }
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            var rules = meta.Chests.Rules;
            _balance.text = new LocTemplate("shop.balance").Format(meta.Cauris);

            _free.Name.text = Loc.Get("shop.free_chest");
            _free.Sub.text = new LocTemplate("shop.free_stored").Format(meta.Chests.FreeStored, rules.FreeMaxStored);
            _free.Action.Label.text = meta.CanClaimFreeChest ? Loc.Get("chest.open")
                : new LocTemplate("chest.free_in").Format(MetaFormat.Duration(meta.Chests.SecondsToNextFree));
            _free.Action.SetInteractable(meta.CanClaimFreeChest);
            MetaUiKit.SetDot(_free.Dot, meta.CanClaimFreeChest);

            bool ad = meta.CanClaimRewardedChest && _ctx.AdReady("chest_bonus");
            _rewarded.Name.text = Loc.Get("chest.rewarded");
            _rewarded.Sub.text = new LocTemplate("shop.rewarded_left").Format(Math.Max(0, rules.RewardedPerDay - (meta.Chests.RewardedDayKey == meta.DayKey ? meta.Chests.RewardedToday : 0)));
            _rewarded.Action.Label.text = Loc.Get("shop.watch");
            _rewarded.Action.SetInteractable(ad);
            _rewarded.SetVisible(meta.IsUnlocked(Feature.Chests));

            bool shop = meta.IsUnlocked(Feature.Shop);
            var defs = meta.Definitions.Chests;
            for (int i = 0; i < _chests.Length; i++)
            {
                ChestDefinition def = null;
                foreach (var c in defs) if (c.Id == _chestIds[i]) { def = c; break; }
                var row = _chests[i];
                row.Name.text = MetaText.ChestName(def.Id);
                row.Sub.text = new LocTemplate("chest.cards").Format(def.Cards);
                row.Action.Label.text = new LocTemplate("shop.buy").Format(def.CostCauris);
                row.Action.SetInteractable(shop && meta.Cauris >= def.CostCauris);
            }

            var cauris = meta.Definitions.Cauris;
            _offline.Name.text = new LocTemplate("shop.offline_extension").Format(MetaFormat.Duration(cauris.OfflineCapExtensionSeconds));
            bool active = meta.OfflineCapExtensionRemainingSeconds > 0d;
            _offline.Sub.text = active ? new LocTemplate("shop.active").Format(MetaFormat.Duration(meta.OfflineCapExtensionRemainingSeconds))
                : new LocTemplate("shop.offline_cap").Format(MetaFormat.Duration(_ctx.Session.OfflineCapSeconds));
            _offline.Action.Label.text = new LocTemplate("shop.buy").Format(cauris.OfflineCapExtensionCauris);
            _offline.Action.SetInteractable(meta.CanExtendOfflineCap);
        }

        public override void Tick(float dt)
        {
            if (!IsOpen) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 0.5f;
            Refresh();
        }
    }
}
