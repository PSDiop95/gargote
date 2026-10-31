using System.Text;
using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Album de recettes : une carte par plat (verrouillée tant que le plat n'a pas été servi), tap = fiche avec anecdote
    /// et recette réelle courte (contenu partageable). Bonus de revenus par album de ville complet.
    /// </summary>
    public sealed class AlbumScreen : MetaScreen
    {
        private const float RowHeight = 150f;
        private readonly MetaUiContext _ctx;
        private readonly Image[] _backs, _dots;
        private readonly TextMeshProUGUI[] _names, _sub;
        private readonly TextMeshProUGUI _progress;
        private readonly RecipeScreen _detail;
        private readonly string _locked;

        public AlbumScreen(RectTransform parent, MetaUiContext ctx, RecipeScreen detail)
            : base(parent, "Album", ctx.Hud, ctx.Assets, new Vector2(1000f, 1640f), Loc.Get("album.title"))
        {
            _ctx = ctx;
            _detail = detail;
            _locked = Loc.Get("album.locked");
            _progress = MetaUiKit.Paragraph(Card, "Progress", Hud, Hud.fontSizeSmall * 0.85f, Hud.mutedText, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -200f), new Vector2(-40f, -110f), TextAlignmentOptions.Center);
            var content = MetaUiKit.ScrollList(Card, "List", new Vector2(24f, 24f), new Vector2(-24f, -210f), out _);
            var recipes = ctx.Meta.Definitions.Recipes;
            content.sizeDelta = new Vector2(0f, recipes.Length * RowHeight);
            _backs = new Image[recipes.Length];
            _dots = new Image[recipes.Length];
            _names = new TextMeshProUGUI[recipes.Length];
            _sub = new TextMeshProUGUI[recipes.Length];
            for (int i = 0; i < recipes.Length; i++)
            {
                int index = i;
                var row = MetaUiKit.Row(content, i, RowHeight, Hud, Assets, "Recipe");
                _backs[i] = row.GetComponent<Image>();
                var b = row.gameObject.AddComponent<UiButton>();
                b.Init(_backs[i], UiFactory.Text(UiFactory.Stretch(row, "Hit"), Hud, 1f, Color.clear), Hud);
                b.EnabledColor = Hud.panelLight;
                b.PunchStrength = 0.03f;
                b.Clicked = () => Open(index);
                var plate = UiFactory.Anchored(row, "Plate", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(110f, 110f));
                UiFactory.Image(plate, Hud.cauris, Assets.circle);
                _names[i] = MetaUiKit.Label(row, "Name", Hud, Hud.fontSizeSmall, Hud.text, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                    new Vector2(150f, 0f), new Vector2(-20f, -10f));
                _sub[i] = MetaUiKit.Label(row, "Sub", Hud, Hud.fontSizeSmall * 0.75f, Hud.mutedText, new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                    new Vector2(150f, 10f), new Vector2(-20f, 0f));
                _dots[i] = MetaUiKit.RedDot(row, Assets, 30f);
            }
            detail.Closed += () => { if (IsOpen) Refresh(); };
        }

        private void Open(int index)
        {
            var r = _ctx.Meta.Definitions.Recipes[index];
            if (!_ctx.Meta.Album.IsUnlocked(r.Id)) return;
            _ctx.Meta.Album.MarkSeen(r.Id);
            _detail.Show(r);
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            var album = meta.Album;
            var recipes = meta.Definitions.Recipes;
            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                bool unlocked = album.IsUnlocked(r.Id);
                _names[i].text = unlocked ? Loc.Get("recipe." + r.Id + ".name") : "???";
                _sub[i].text = unlocked ? _ctx.BusinessName(r.BusinessId) : _locked;
                _backs[i].color = unlocked ? Hud.panelLight : Hud.panel;
                MetaUiKit.SetDot(_dots[i], unlocked && !album.IsSeen(r.Id));
            }
            var sb = new StringBuilder(new LocTemplate("album.progress").Format(album.Count, album.Total));
            sb.Append('\n').Append(new LocTemplate("album.bonus").Format(meta.Definitions.AlbumCityBonusMultiplier.ToString("0.##")));
            _progress.text = sb.ToString();
        }
    }

    /// <summary>Fiche recette : nom, commerce, anecdote culturelle, étapes de la recette réelle.</summary>
    public sealed class RecipeScreen : MetaScreen
    {
        private readonly MetaUiContext _ctx;
        private readonly TextMeshProUGUI _business, _anecdote, _steps;

        public RecipeScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Recipe", ctx.Hud, ctx.Assets, new Vector2(960f, 1500f), "")
        {
            _ctx = ctx;
            var plate = UiFactory.Anchored(Card, "Plate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(200f, 200f));
            UiFactory.Image(plate, Hud.cauris, Assets.circle);
            _business = MetaUiKit.Label(Card, "Business", Hud, Hud.fontSizeSmall * 0.85f, Hud.accent, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -390f), new Vector2(-40f, -336f), TextAlignmentOptions.Center);
            _anecdote = MetaUiKit.Paragraph(Card, "Anecdote", Hud, Hud.fontSizeSmall * 0.85f, Hud.text, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(50f, -660f), new Vector2(-50f, -410f), TextAlignmentOptions.TopLeft);
            _anecdote.fontStyle = FontStyles.Italic;
            _steps = MetaUiKit.Paragraph(Card, "Steps", Hud, Hud.fontSizeSmall * 0.8f, Hud.text, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(50f, 40f), new Vector2(-50f, -680f), TextAlignmentOptions.TopLeft);
        }

        private RecipeDefinition _recipe;

        public void Show(RecipeDefinition recipe)
        {
            _recipe = recipe;
            Show();
        }

        public override void Refresh()
        {
            if (_recipe == null) return;
            string prefix = "recipe." + _recipe.Id;
            Title.text = Loc.Get(prefix + ".name");
            _business.text = _ctx.BusinessName(_recipe.BusinessId);
            _anecdote.text = Loc.Get(prefix + ".anecdote");
            var sb = new StringBuilder();
            sb.Append("<b>").Append(Loc.Get("album.steps")).Append("</b>\n");
            int steps = Loc.CountLines(prefix + ".step");
            for (int s = 1; s <= steps; s++) sb.Append(s).Append(". ").Append(Loc.Get(prefix + ".step." + s)).Append('\n');
            _steps.text = sb.ToString();
        }
    }

    /// <summary>Succès (bronze / argent / or) : progression, réclamation des cauris, classés réclamables d'abord.</summary>
    public sealed class AchievementsScreen : MetaScreen
    {
        private const float RowHeight = 170f;
        private static readonly Color[] TierColors = { new Color(0.80f, 0.50f, 0.25f), new Color(0.78f, 0.80f, 0.85f), new Color(1f, 0.78f, 0.20f) };

        private readonly MetaUiContext _ctx;
        private readonly RectTransform[] _rows;
        private readonly Image[] _medals, _fills;
        private readonly TextMeshProUGUI[] _names, _descs, _progress;
        private readonly UiButton[] _claims;
        private readonly int[] _order;
        private readonly TextMeshProUGUI _summary;
        private readonly string[] _tierNames;

        public AchievementsScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Achievements", ctx.Hud, ctx.Assets, new Vector2(1000f, 1640f), Loc.Get("achievements.title"))
        {
            _ctx = ctx;
            _tierNames = new[] { Loc.Get("achievements.tier_bronze"), Loc.Get("achievements.tier_silver"), Loc.Get("achievements.tier_gold") };
            _summary = MetaUiKit.Label(Card, "Summary", Hud, Hud.fontSizeSmall * 0.85f, Hud.mutedText, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -170f), new Vector2(-40f, -110f), TextAlignmentOptions.Center);
            var content = MetaUiKit.ScrollList(Card, "List", new Vector2(24f, 24f), new Vector2(-24f, -180f), out _);
            int n = ctx.Meta.Achievements.Count;
            content.sizeDelta = new Vector2(0f, n * RowHeight);
            _rows = new RectTransform[n];
            _medals = new Image[n];
            _fills = new Image[n];
            _names = new TextMeshProUGUI[n];
            _descs = new TextMeshProUGUI[n];
            _progress = new TextMeshProUGUI[n];
            _claims = new UiButton[n];
            _order = new int[n];
            for (int i = 0; i < n; i++)
            {
                var row = MetaUiKit.Row(content, i, RowHeight, Hud, Assets, "Achievement");
                _rows[i] = row;
                var medal = UiFactory.Anchored(row, "Medal", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(100f, 100f));
                _medals[i] = UiFactory.Image(medal, Hud.panel, Assets.circle);
                _names[i] = MetaUiKit.Label(row, "Name", Hud, Hud.fontSizeSmall * 0.9f, Hud.text, new Vector2(0f, 0.62f), new Vector2(0.68f, 1f),
                    new Vector2(136f, 0f), new Vector2(0f, -8f));
                _descs[i] = MetaUiKit.Label(row, "Desc", Hud, Hud.fontSizeSmall * 0.7f, Hud.mutedText, new Vector2(0f, 0.36f), new Vector2(0.68f, 0.62f),
                    new Vector2(136f, 0f), Vector2.zero);
                _fills[i] = MetaUiKit.Bar(row, "Bar", Hud, Assets, Hud.money, new Vector2(0f, 0.08f), new Vector2(0.68f, 0.32f), new Vector2(136f, 0f), Vector2.zero);
                _progress[i] = MetaUiKit.Outline(MetaUiKit.Label((RectTransform)_fills[i].transform.parent, "Progress", Hud, Hud.fontSizeSmall * 0.6f, Hud.text,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center));
                int captured = i;
                _claims[i] = MetaUiKit.Button(row, "Claim", Hud, Assets, "", Hud.fontSizeSmall * 0.85f, new Vector2(0.7f, 0.15f), new Vector2(0.98f, 0.85f),
                    Vector2.zero, Vector2.zero);
                _claims[i].Clicked = () => Claim(_order[captured]);
            }
        }

        private void Claim(int achievement)
        {
            long cauris = _ctx.Meta.ClaimAchievement(achievement);
            if (cauris <= 0) return;
            _ctx.Audio?.Play(SoundId.Collect);
            _ctx.Toast?.Invoke(new LocTemplate("reward.cauris").Format(cauris));
            Refresh();
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            var a = meta.Achievements;
            var defs = meta.Definitions.Achievements;
            // Réclamables d'abord, puis en cours, puis terminés (tri par insertion, 60 entrées, à l'ouverture seulement).
            for (int i = 0; i < _order.Length; i++) _order[i] = i;
            for (int i = 1; i < _order.Length; i++)
            {
                int v = _order[i], k = Rank(a, v), j = i - 1;
                while (j >= 0 && Rank(a, _order[j]) > k) { _order[j + 1] = _order[j]; j--; }
                _order[j + 1] = v;
            }
            for (int slot = 0; slot < _order.Length; slot++)
            {
                int i = _order[slot];
                var def = defs[i];
                int claimed = a.ClaimedTiers(i), reached = a.ReachedTiers(i), tiers = def.Tiers.Length;
                bool done = claimed >= tiers;
                int shownTier = System.Math.Min(claimed, tiers - 1);
                _names[slot].text = Loc.Get("achievement." + def.Id + ".name") + (claimed > 0 ? "  · " + _tierNames[System.Math.Min(claimed, 3) - 1] : string.Empty);
                _descs[slot].text = MetaText.Achievement(def, shownTier, _ctx.BusinessName);
                _medals[slot].color = claimed > 0 ? TierColors[System.Math.Min(claimed, 3) - 1] : Hud.panel;
                double target = def.Tiers[shownTier], value = a.Value(i);
                _fills[slot].fillAmount = done ? 1f : Mathf.Clamp01((float)(value / target));
                _progress[slot].text = done ? "OK" : NumberFormatter.Format(BigDouble.FromDouble(System.Math.Floor(System.Math.Min(value, target))))
                    + " / " + NumberFormatter.Format(BigDouble.FromDouble(target));
                bool claimable = reached > claimed;
                _claims[slot].transform.parent.gameObject.SetActive(claimable || done);
                _claims[slot].SetInteractable(claimable);
                _claims[slot].Label.text = claimable ? new LocTemplate("achievements.claim").Format(def.CaurisRewards[claimed]) : "OK";
            }
            _summary.text = a.TotalClaimed + " / " + a.TotalTiers;
        }

        /// <summary>0 = réclamable, 1 = en cours, 2 = terminé (tous les paliers réclamés).</summary>
        private static int Rank(AchievementSystem a, int i)
        {
            if (a.IsClaimable(i)) return 0;
            return a.ClaimedTiers(i) >= a.Definition(i).Tiers.Length ? 2 : 1;
        }
    }
}
