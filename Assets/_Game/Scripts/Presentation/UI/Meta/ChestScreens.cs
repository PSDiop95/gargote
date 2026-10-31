using System;
using System.Text;
using Gargote.Core;
using Gargote.Systems.Meta;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Ouverture de coffre (§9.3, moment fort) : secousse, toucher pour ouvrir, cartes qui jaillissent une par une,
    /// révélation plus longue pour un Légendaire, bouton « Tout révéler ». Le tirage est déjà fait (déterministe) :
    /// l'écran ne fait que le montrer.
    /// </summary>
    public sealed class ChestOpenScreen : MetaScreen
    {
        private const int MaxCards = 15;
        private const float CardInterval = 0.32f, LegendaryHold = 1.2f;

        private sealed class CardView
        {
            public RectTransform Root;
            public Image Frame, Glow;
            public TextMeshProUGUI Name, Rarity, Note;
            public Tween Pop;
        }

        private readonly MetaUiContext _ctx;
        private readonly RectTransform _chest;
        private readonly Image _chestImage;
        private readonly TextMeshProUGUI _hint, _summary;
        private readonly CardView[] _cards = new CardView[MaxCards];
        private readonly UiButton _revealAll, _continue;
        private readonly Image _flash;
        private readonly string _new, _duplicate;
        private ChestResult _result;
        private int _revealed;
        private float _timer, _shake;
        private bool _opened;
        private Tween _chestTween, _flashTween;

        /// <summary>Le joueur a fini de regarder ce coffre (le suivant de la file peut s'afficher).</summary>
        public Action Finished;

        public ChestOpenScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "ChestOpen", ctx.Hud, ctx.Assets, new Vector2(1040f, 1500f), "", false)
        {
            _ctx = ctx;
            _new = Loc.Get("chest.new");
            _duplicate = Loc.Get("chest.duplicate_cauris");
            _chest = UiFactory.Anchored(Card, "Chest", new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 320f));
            _chestImage = UiFactory.Image(_chest, Hud.accent, Assets.rounded, true);
            var tap = _chest.gameObject.AddComponent<UiButton>();
            tap.Init(_chestImage, UiFactory.Text(UiFactory.Stretch(_chest, "Lid"), Hud, Hud.fontSizeLarge, Hud.text, TextAlignmentOptions.Center, "?"), Hud);
            tap.PunchStrength = 0.15f;
            tap.Clicked = Open;
            _hint = MetaUiKit.Label(Card, "Hint", Hud, Hud.fontSizeMedium, Hud.mutedText, new Vector2(0f, 0.35f), new Vector2(1f, 0.42f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Loc.Get("chest.tap"));

            for (int i = 0; i < MaxCards; i++)
            {
                var c = new CardView();
                int col = i % 5, row = i / 5;
                c.Root = UiFactory.Anchored(Card, "Card" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((col - 2) * 196f, -150f - row * 300f), new Vector2(184f, 284f));
                c.Glow = UiFactory.Image(UiFactory.Stretch(c.Root, "Glow"), Color.clear, Assets.rounded);
                c.Glow.rectTransform.offsetMin = new Vector2(-12f, -12f);
                c.Glow.rectTransform.offsetMax = new Vector2(12f, 12f);
                c.Frame = UiFactory.Image(UiFactory.Stretch(c.Root, "Frame"), Hud.panelLight, Assets.rounded);
                c.Rarity = MetaUiKit.Label(c.Root, "Rarity", Hud, Hud.fontSizeSmall * 0.7f, Hud.text, new Vector2(0f, 0.82f), new Vector2(1f, 1f),
                    new Vector2(6f, 0f), new Vector2(-6f, -6f), TextAlignmentOptions.Center);
                c.Name = MetaUiKit.Paragraph(c.Root, "Name", Hud, Hud.fontSizeSmall * 0.8f, Hud.text, new Vector2(0f, 0.3f), new Vector2(1f, 0.8f),
                    new Vector2(8f, 0f), new Vector2(-8f, 0f), TextAlignmentOptions.Center);
                c.Name.fontStyle = FontStyles.Bold;
                c.Note = MetaUiKit.Paragraph(c.Root, "Note", Hud, Hud.fontSizeSmall * 0.65f, Hud.accent, new Vector2(0f, 0f), new Vector2(1f, 0.3f),
                    new Vector2(6f, 6f), new Vector2(-6f, 0f), TextAlignmentOptions.Center);
                c.Root.gameObject.SetActive(false);
                _cards[i] = c;
            }
            _summary = MetaUiKit.Paragraph(Card, "Summary", Hud, Hud.fontSizeMedium, Hud.cauris, new Vector2(0f, 0.12f), new Vector2(1f, 0.2f),
                new Vector2(40f, 0f), new Vector2(-40f, 0f), TextAlignmentOptions.Center);
            _revealAll = MetaUiKit.Button(Card, "RevealAll", Hud, Assets, Loc.Get("chest.reveal_all"), Hud.fontSizeSmall,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 30f), new Vector2(220f, 160f), Hud.panelLight);
            _revealAll.Clicked = RevealAll;
            _continue = MetaUiKit.Button(Card, "Continue", Hud, Assets, Loc.Get("chest.continue"), Hud.fontSizeMedium,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 30f), new Vector2(220f, 160f));
            _continue.Clicked = () => { Hide(); Finished?.Invoke(); };
            _flash = UiFactory.Image(UiFactory.Stretch(Root, "Flash"), new Color(1f, 0.9f, 0.5f, 0f), null);
        }

        public void Show(ChestResult result)
        {
            _result = result;
            _opened = false;
            _revealed = 0;
            _timer = 0f;
            _shake = 0f;
            Title.text = MetaText.ChestName(result.ChestId);
            Show();
        }

        public override void Refresh()
        {
            if (_result == null) return;
            _chest.gameObject.SetActive(!_opened);
            _hint.gameObject.SetActive(!_opened);
            _chestImage.color = ChestColor(_result.ChestId);
            for (int i = 0; i < MaxCards; i++) _cards[i].Root.gameObject.SetActive(_opened && i < _revealed && i < _result.Cards.Count);
            bool done = _opened && _revealed >= _result.Cards.Count;
            _revealAll.transform.parent.gameObject.SetActive(_opened && !done);
            _continue.transform.parent.gameObject.SetActive(done);
            _summary.gameObject.SetActive(done);
            if (done) _summary.text = Summary(_result);
        }

        private string Summary(ChestResult r)
        {
            var sb = new StringBuilder();
            if (r.Cauris > 0) sb.Append(new LocTemplate("reward.cauris").Format(r.Cauris));
            if (r.Boost)
            {
                var rules = _ctx.Meta.Definitions.ChestRules;
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(new LocTemplate("chest.boost").Format(rules.BoostMultiplier, MetaFormat.Duration(rules.BoostSeconds)));
            }
            return sb.ToString();
        }

        public static Color ChestColor(string id)
        {
            switch (id)
            {
                case "wood": return new Color(0.55f, 0.36f, 0.20f);
                case "bronze": return new Color(0.80f, 0.50f, 0.25f);
                case "gold": return new Color(0.98f, 0.78f, 0.20f);
                case "teranga": return new Color(0.20f, 0.65f, 0.55f);
                default: return Color.gray;
            }
        }

        private void Open()
        {
            if (_opened) return;
            _opened = true;
            _timer = 0.2f;
            _ctx.Audio?.Play(SoundId.Unlock);
            Flash(0.6f);
            Refresh();
        }

        private void RevealAll()
        {
            while (_result != null && _revealed < _result.Cards.Count) RevealNext(false);
            Refresh();
        }

        private void RevealNext(bool animate)
        {
            int i = _revealed++;
            if (i >= MaxCards) return;
            var card = _result.Cards[i];
            var v = _cards[i];
            var def = _ctx.Meta.Managers.Definition(card.ManagerIndex);
            Color color = MetaFormat.Rarity(card.Rarity);
            v.Frame.color = Color.Lerp(Hud.panelLight, color, 0.55f);
            v.Glow.color = card.Rarity >= Gargote.Core.Rarity.Epic ? new Color(color.r, color.g, color.b, 0.7f) : Color.clear;
            v.Rarity.text = MetaFormat.RarityName(card.Rarity);
            v.Name.text = MetaText.ManagerName(def);
            v.Note.text = card.IsNew ? _new : card.ConvertedCauris > 0 ? new LocTemplate("chest.duplicate_cauris").Format(card.ConvertedCauris) : "+1";
            v.Root.gameObject.SetActive(true);
            if (animate)
            {
                Juice.Pop(ref v.Pop, v.Root, Vector3.one, card.Rarity == Gargote.Core.Rarity.Legendary ? 0.8f : 0.35f);
                if (card.Rarity >= Gargote.Core.Rarity.Epic) Flash(card.Rarity == Gargote.Core.Rarity.Legendary ? 0.9f : 0.4f);
                _ctx.Audio?.Play(card.Rarity >= Gargote.Core.Rarity.Epic ? SoundId.Milestone : SoundId.Tap, 1f + 0.05f * i, 0.8f);
            }
        }

        private void Flash(float alpha)
        {
            if (_flashTween.isAlive) _flashTween.Stop();
            _flash.color = new Color(1f, 0.9f, 0.5f, alpha);
            _flashTween = Tween.Alpha(_flash, alpha, 0f, 0.5f, Ease.OutQuad);
        }

        public override void Tick(float dt)
        {
            if (!IsOpen || _result == null) return;
            if (!_opened)
            {
                // Secousse d'impatience.
                _shake += dt;
                float k = Mathf.Sin(_shake * 22f) * (Mathf.Repeat(_shake, 1.4f) < 0.5f ? 6f : 0f);
                _chest.localEulerAngles = new Vector3(0f, 0f, k);
                return;
            }
            if (_revealed >= _result.Cards.Count) return;
            _timer -= dt;
            if (_timer > 0f) return;
            var next = _result.Cards[_revealed];
            RevealNext(true);
            _timer = next.Rarity == Gargote.Core.Rarity.Legendary ? LegendaryHold : CardInterval;
            if (_revealed >= _result.Cards.Count) Refresh();
        }
    }

    /// <summary>Probabilités affichées (bouton « i ») et compteur de pitié visible.</summary>
    public sealed class ChestOddsScreen : MetaScreen
    {
        private readonly MetaUiContext _ctx;
        private readonly TextMeshProUGUI _body;
        private string _chestId;

        public ChestOddsScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "ChestOdds", ctx.Hud, ctx.Assets, new Vector2(900f, 900f), Loc.Get("chest.odds"))
        {
            _ctx = ctx;
            _body = MetaUiKit.Paragraph(Card, "Body", Hud, Hud.fontSizeSmall, Hud.text, Vector2.zero, Vector2.one,
                new Vector2(50f, 40f), new Vector2(-50f, -140f));
        }

        public void Show(string chestId)
        {
            _chestId = chestId;
            Show();
        }

        public override void Refresh()
        {
            var chests = _ctx.Meta.Chests;
            var def = _ctx.Meta.Definitions.Chest(_chestId);
            if (def == null) return;
            Title.text = MetaText.ChestName(_chestId);
            var p = chests.RarityProbabilities(_chestId);
            var sb = new StringBuilder();
            sb.Append(new LocTemplate("chest.cards").Format(def.Cards)).Append('\n').Append(Loc.Get("chest.odds")).Append(" :\n");
            for (int r = 0; r < 4; r++)
                sb.Append("  ").Append(MetaFormat.RarityName((Rarity)r)).Append(" : ").Append((p[r] * 100d).ToString("0.##")).Append(" %\n");
            sb.Append('\n');
            for (int r = 1; r < 4; r++)
                sb.Append(MetaFormat.RarityName((Rarity)r)).Append(" ≥ 1 : ").Append((chests.ChanceAtLeastOne(_chestId, (Rarity)r) * 100d).ToString("0.#")).Append(" %\n");
            if (def.PityEvery > 0)
            {
                int left = chests.ChestsUntilGuaranteed(_chestId);
                sb.Append('\n').Append(left <= 1
                    ? new LocTemplate("chest.pity_next").Format(MetaFormat.RarityName(def.PityRarity))
                    : new LocTemplate("chest.pity").Format(MetaFormat.RarityName(def.PityRarity), left));
            }
            _body.text = sb.ToString();
        }
    }
}
