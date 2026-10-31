using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Onglet Gérants : grille de collection (silhouettes « ??? » pour les gérants pas encore trouvés), compteur par rareté,
    /// tap = fiche. Les cartes sont construites une fois (32 gérants) ; l'ouverture ne fait que mettre à jour les textes.
    /// </summary>
    public sealed class ManagersScreen : MetaScreen
    {
        private const int Columns = 4;
        private const float CellHeight = 250f;

        private sealed class Cell
        {
            public Image Back, Ring, Dot;
            public TextMeshProUGUI Name, Level;
            public Image CardsFill;
        }

        private readonly MetaUiContext _ctx;
        private readonly Cell[] _cells;
        private readonly TextMeshProUGUI _summary;
        private readonly string _unknown;
        private readonly LocTemplate _level;
        private readonly ManagerDetailScreen _detail;

        public ManagersScreen(RectTransform parent, MetaUiContext ctx, ManagerDetailScreen detail)
            : base(parent, "Managers", ctx.Hud, ctx.Assets, new Vector2(1040f, 1640f), Loc.Get("managers.title"))
        {
            _ctx = ctx;
            _detail = detail;
            _unknown = Loc.Get("managers.unknown");
            _level = new LocTemplate("managers.level");
            _summary = MetaUiKit.Label(Card, "Summary", Hud, Hud.fontSizeSmall * 0.85f, Hud.mutedText, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -170f), new Vector2(-40f, -110f), TextAlignmentOptions.Center);
            var content = MetaUiKit.ScrollList(Card, "List", new Vector2(24f, 24f), new Vector2(-24f, -180f), out _);
            var managers = ctx.Meta.Managers;
            _cells = new Cell[managers.Count];
            int rows = (managers.Count + Columns - 1) / Columns;
            content.sizeDelta = new Vector2(0f, rows * CellHeight);
            for (int i = 0; i < managers.Count; i++)
            {
                int index = i;
                int col = i % Columns, row = i / Columns;
                var rt = UiFactory.Rect(content, "Manager" + i, new Vector2(col / (float)Columns, 1f), new Vector2((col + 1) / (float)Columns, 1f),
                    new Vector2(8f, -(row + 1) * CellHeight + 8f), new Vector2(-8f, -row * CellHeight - 8f));
                var c = new Cell();
                c.Back = UiFactory.Image(rt, Hud.panelLight, Assets.rounded, true);
                var button = rt.gameObject.AddComponent<UiButton>();
                button.Init(c.Back, UiFactory.Text(UiFactory.Stretch(rt, "Hit"), Hud, 1f, Color.clear), Hud);
                button.PunchStrength = 0.05f;
                button.Clicked = () => _detail.Show(index);
                var ring = UiFactory.Anchored(rt, "Portrait", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(110f, 110f));
                c.Ring = UiFactory.Image(ring, MetaFormat.Rarity(managers.Definition(i).Rarity), Assets.circle);
                var inner = UiFactory.Anchored(ring, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
                UiFactory.Image(inner, Hud.panel, Assets.circle);
                c.Name = MetaUiKit.Paragraph(rt, "Name", Hud, Hud.fontSizeSmall * 0.6f, Hud.text, new Vector2(0f, 0.22f), new Vector2(1f, 0.52f),
                    new Vector2(6f, 0f), new Vector2(-6f, 0f), TextAlignmentOptions.Center);
                c.Level = MetaUiKit.Label(rt, "Level", Hud, Hud.fontSizeSmall * 0.55f, Hud.mutedText, new Vector2(0f, 0.1f), new Vector2(1f, 0.24f),
                    Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
                c.CardsFill = MetaUiKit.Bar(rt, "Cards", Hud, Assets, Hud.money, new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.1f), Vector2.zero, Vector2.zero);
                c.Dot = MetaUiKit.RedDot(rt, Assets, 30f);
                _cells[i] = c;
            }
            detail.Closed += () => { if (IsOpen) Refresh(); };
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            var managers = meta.Managers;
            for (int i = 0; i < _cells.Length; i++)
            {
                var c = _cells[i];
                var def = managers.Definition(i);
                bool owned = managers.Owns(i);
                c.Name.text = owned ? MetaText.ManagerName(def) : _unknown;
                c.Back.color = owned ? Hud.panelLight : Hud.panel;
                c.Ring.color = owned ? MetaFormat.Rarity(def.Rarity) : Color.Lerp(Hud.panel, MetaFormat.Rarity(def.Rarity), 0.35f);
                c.Level.text = owned ? _level.Format(managers.Level(i), managers.MaxLevel) : MetaFormat.RarityName(def.Rarity);
                int need = managers.CardsToNext(i);
                c.CardsFill.transform.parent.gameObject.SetActive(owned && need > 0);
                if (owned && need > 0) c.CardsFill.fillAmount = Mathf.Clamp01((float)managers.Cards(i) / need);
                MetaUiKit.SetDot(c.Dot, meta.CanLevelUpManager(i));
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(new LocTemplate("managers.collection").Format(managers.OwnedCount, managers.Count));
            for (int r = 1; r < 4; r++) sb.Append("   ").Append(MetaFormat.RarityName((Rarity)r)).Append(' ').Append(managers.OwnedOfRarity((Rarity)r));
            _summary.text = sb.ToString();
        }
    }

    /// <summary>
    /// Fiche gérant : rareté, réplique, bonus passif, boost actif (lancer / recharger), montée de niveau (cartes + FCFA),
    /// placement au commerce. Le joueur idle n'est jamais pénalisé : le passif marche seul, le boost est un plus.
    /// </summary>
    public sealed class ManagerDetailScreen : MetaScreen
    {
        private readonly MetaUiContext _ctx;
        private readonly Image _portrait;
        private readonly TextMeshProUGUI _rarity, _business, _line, _passive, _boost, _level, _cards, _boostState;
        private readonly Image _cardsFill;
        private readonly UiButton _levelUp, _boostGo, _accelerate, _equip;
        private readonly TextBuffer _stateText = new TextBuffer(48);
        private readonly LocTemplate _active, _cooldown;
        private int _index = -1;
        private float _refresh;

        public ManagerDetailScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "ManagerDetail", ctx.Hud, ctx.Assets, new Vector2(960f, 1500f), "")
        {
            _ctx = ctx;
            _active = new LocTemplate("managers.boost_active");
            _cooldown = new LocTemplate("managers.boost_cooldown");
            var portrait = UiFactory.Anchored(Card, "Portrait", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(220f, 220f));
            _portrait = UiFactory.Image(portrait, Hud.accent, Assets.circle);
            var inner = UiFactory.Anchored(portrait, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));
            UiFactory.Image(inner, Hud.panelLight, Assets.circle);
            _rarity = Line("Rarity", 360f, 50f, Hud.fontSizeSmall, Hud.accent);
            _business = Line("Business", 410f, 46f, Hud.fontSizeSmall * 0.85f, Hud.mutedText);
            _line = Line("Line", 470f, 110f, Hud.fontSizeSmall * 0.9f, Hud.text);
            _line.fontStyle = FontStyles.Italic;
            _line.textWrappingMode = TextWrappingModes.Normal;
            _passive = Line("Passive", 600f, 50f, Hud.fontSizeSmall * 0.9f, Hud.money);
            _boost = Line("Boost", 650f, 50f, Hud.fontSizeSmall * 0.9f, Hud.text);
            _boostState = Line("BoostState", 700f, 46f, Hud.fontSizeSmall * 0.8f, Hud.mutedText);
            _level = Line("Level", 780f, 54f, Hud.fontSizeMedium, Hud.text);
            _cardsFill = MetaUiKit.Bar(Card, "CardsBar", Hud, Assets, Hud.money, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(140f, -900f), new Vector2(-140f, -846f));
            _cards = MetaUiKit.Outline(MetaUiKit.Label((RectTransform)_cardsFill.transform.parent, "Cards", Hud, Hud.fontSizeSmall * 0.75f, Hud.text,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center));
            _levelUp = MetaUiKit.Button(Card, "LevelUp", Hud, Assets, "", Hud.fontSizeSmall, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-400f, 300f), new Vector2(400f, 420f));
            _levelUp.Clicked = () => { if (_ctx.Meta.LevelUpManager(_index)) { _ctx.Audio?.Play(SoundId.Milestone); Refresh(); } };
            _boostGo = MetaUiKit.Button(Card, "BoostGo", Hud, Assets, Loc.Get("managers.boost_go"), Hud.fontSizeSmall,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-400f, 170f), new Vector2(-10f, 280f), Hud.accent);
            _boostGo.Clicked = () => { if (_ctx.Meta.ActivateManagerBoost(Def.BusinessId)) { _ctx.Audio?.Play(SoundId.Milestone); Refresh(); } };
            _accelerate = MetaUiKit.Button(Card, "Accelerate", Hud, Assets, Loc.Get("managers.accelerate"), Hud.fontSizeSmall * 0.85f,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-400f, 170f), new Vector2(-10f, 280f), Hud.accent);
            _accelerate.Clicked = () => _ctx.Rewarded("manager_boost", () => { if (_ctx.Meta.AccelerateBoost(_index, true)) Refresh(); });
            _equip = MetaUiKit.Button(Card, "Equip", Hud, Assets, Loc.Get("managers.equip"), Hud.fontSizeSmall,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(10f, 170f), new Vector2(400f, 280f), Hud.panelLight);
            _equip.Clicked = () => { if (_ctx.Meta.Managers.Equip(_index)) Refresh(); };
        }

        private ManagerDefinition Def => _ctx.Meta.Managers.Definition(_index);

        private TextMeshProUGUI Line(string name, float top, float height, float size, Color color) =>
            MetaUiKit.Label(Card, name, Hud, size, color, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(50f, -top - height), new Vector2(-50f, -top), TextAlignmentOptions.Center);

        public void Show(int index)
        {
            _index = index;
            Show();
        }

        public override void Refresh()
        {
            if (_index < 0) return;
            var meta = _ctx.Meta;
            var managers = meta.Managers;
            var def = Def;
            bool owned = managers.Owns(_index);
            Title.text = owned ? MetaText.ManagerName(def) : Loc.Get("managers.unknown");
            _portrait.color = MetaFormat.Rarity(def.Rarity);
            _rarity.text = MetaFormat.RarityName(def.Rarity);
            _business.text = _ctx.BusinessName(def.BusinessId) + " · " + MetaText.CityName(def.CityId);
            _line.text = owned ? "« " + Loc.Get("manager." + def.Id + ".line") + " »" : Loc.Get("managers.not_owned");
            int level = System.Math.Max(1, managers.Level(_index));
            string passive = MetaFormat.Percent(def.PassivePerLevel * level);
            _passive.text = new LocTemplate(def.Passive == PassiveScope.Global ? "managers.passive_global" : "managers.passive_business").Format(passive);
            _boost.text = new LocTemplate("managers.boost").Format(def.BoostMultiplier.ToString("0.#"), MetaFormat.Duration(def.BoostSeconds));
            _level.text = new LocTemplate("managers.level").Format(managers.Level(_index), managers.MaxLevel);

            int need = managers.CardsToNext(_index);
            _cardsFill.transform.parent.gameObject.SetActive(owned && need > 0);
            if (owned && need > 0)
            {
                _cardsFill.fillAmount = Mathf.Clamp01((float)managers.Cards(_index) / need);
                _cards.text = new LocTemplate("managers.cards").Format(managers.Cards(_index), need);
            }
            bool canLevel = owned && meta.CanLevelUpManager(_index);
            _levelUp.transform.parent.gameObject.SetActive(owned && !managers.IsMaxed(_index));
            _levelUp.SetInteractable(canLevel);
            _levelUp.Label.text = Loc.Get("managers.level_up") + "\n" + NumberFormatter.Format(meta.ManagerLevelCost(_index)) + " FCFA";

            bool equipped = managers.Equipped(def.BusinessId) == _index;
            _equip.transform.parent.gameObject.SetActive(owned);
            _equip.SetInteractable(!equipped);
            _equip.Label.text = Loc.Get(equipped ? "managers.equipped" : "managers.equip");
            UpdateBoost();
        }

        private void UpdateBoost()
        {
            var managers = _ctx.Meta.Managers;
            bool owned = managers.Owns(_index);
            bool equipped = owned && managers.Equipped(Def.BusinessId) == _index;
            bool cooling = owned && !managers.IsBoostActive(_index) && managers.CooldownRemaining(_index) > 0d;
            _boostGo.transform.parent.gameObject.SetActive(equipped && !cooling);
            _boostGo.SetInteractable(equipped && managers.IsBoostReady(_index));
            _accelerate.transform.parent.gameObject.SetActive(cooling && _ctx.AdReady("manager_boost"));
            var t = _stateText.Clear();
            if (!owned) { t.ApplyIfChanged(_boostState); return; }
            if (managers.IsBoostActive(_index)) t.Append(_active.Prefix).AppendDuration(managers.BoostRemaining(_index)).Append(_active.Suffix);
            else if (cooling) t.Append(_cooldown.Prefix).AppendDuration(managers.CooldownRemaining(_index)).Append(_cooldown.Suffix);
            else t.Append(Loc.Get("managers.boost_ready"));
            t.ApplyIfChanged(_boostState);
        }

        public override void Tick(float dt)
        {
            if (!IsOpen || _index < 0) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 0.25f;
            UpdateBoost();
        }
    }
}
