using System.Text;
using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Cadeaux du jour : calendrier de connexion (14 cases), série avec flamme, bouclier de série (rewarded, 1/semaine).
    /// Horloge reculée ou fuseau changé : rien n'est réclamable, sans message accusateur (le bouton dit simplement « demain »).
    /// </summary>
    public sealed class DailyScreen : MetaScreen
    {
        private const int Columns = 7;
        private readonly MetaUiContext _ctx;
        private readonly Image[] _cells;
        private readonly TextMeshProUGUI[] _cellDay, _cellReward;
        private readonly TextMeshProUGUI _streak, _info;
        private readonly UiButton _claim, _shield;
        private readonly LocTemplate _dayTemplate, _streakTemplate;
        private readonly TextBuffer _timer = new TextBuffer(64);
        private float _refresh;

        public DailyScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Daily", ctx.Hud, ctx.Assets, new Vector2(1000f, 1180f), Loc.Get("daily.title"))
        {
            _ctx = ctx;
            _dayTemplate = new LocTemplate("login.day");
            _streakTemplate = new LocTemplate("login.streak");
            int days = ctx.Meta.Definitions.Login.Days.Length;
            _cells = new Image[days];
            _cellDay = new TextMeshProUGUI[days];
            _cellReward = new TextMeshProUGUI[days];
            const float cell = 128f, gap = 6f;
            float x0 = -(Columns * cell + (Columns - 1) * gap) * 0.5f + cell * 0.5f;
            for (int d = 0; d < days; d++)
            {
                int col = d % Columns, row = d / Columns;
                var rt = UiFactory.Anchored(Card, "Day" + d, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x0 + col * (cell + gap), -150f - row * (cell * 1.35f + gap)), new Vector2(cell, cell * 1.35f));
                _cells[d] = UiFactory.Image(rt, Hud.panelLight, Assets.rounded);
                _cellDay[d] = MetaUiKit.Label(rt, "Day", Hud, Hud.fontSizeSmall * 0.6f, Hud.mutedText, new Vector2(0f, 0.72f), Vector2.one,
                    Vector2.zero, new Vector2(0f, -4f), TextAlignmentOptions.Center, _dayTemplate.Format(d + 1));
                _cellReward[d] = MetaUiKit.Paragraph(rt, "Reward", Hud, Hud.fontSizeSmall * 0.55f, Hud.cauris, Vector2.zero, new Vector2(1f, 0.72f),
                    new Vector2(4f, 4f), new Vector2(-4f, 0f), TextAlignmentOptions.Center);
                _cellReward[d].text = MetaText.Reward(ctx.Meta.Definitions.Login.Days[d]).Replace("  ·  ", "\n");
            }
            float y = 150f + 2f * (cell * 1.35f + gap) + 30f;
            _streak = MetaUiKit.Label(Card, "Streak", Hud, Hud.fontSizeMedium, Hud.accent, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -y - 70f), new Vector2(-40f, -y), TextAlignmentOptions.Center);
            _info = MetaUiKit.Paragraph(Card, "Info", Hud, Hud.fontSizeSmall * 0.85f, Hud.mutedText, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -y - 160f), new Vector2(-40f, -y - 76f), TextAlignmentOptions.Center);
            _claim = MetaUiKit.Button(Card, "Claim", Hud, Assets, Loc.Get("login.claim"), Hud.fontSizeMedium,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-300f, 170f), new Vector2(300f, 290f));
            _claim.Clicked = Claim;
            _shield = MetaUiKit.Button(Card, "Shield", Hud, Assets, Loc.Get("login.shield_get"), Hud.fontSizeSmall,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-300f, 40f), new Vector2(300f, 150f), Hud.accent);
            _shield.Clicked = () => _ctx.Rewarded("streak_shield", () => { if (_ctx.Meta.EarnStreakShield()) Refresh(); });
        }

        private void Claim()
        {
            var reward = _ctx.Meta.ClaimLogin();
            if (reward == null) return;
            _ctx.Audio?.Play(SoundId.Collect);
            if (_ctx.Meta.Daily.LastClaimUsedShield) _ctx.Toast?.Invoke(Loc.Get("login.shield_used"));
            _ctx.Toast?.Invoke(MetaText.Reward(reward));
            Refresh();
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            var daily = meta.Daily;
            int days = _cells.Length;
            bool canClaim = meta.CanClaimLogin;
            int today = daily.CalendarIndex % days;
            for (int d = 0; d < days; d++)
            {
                bool claimed = d < today;
                bool current = d == today;
                _cells[d].color = current ? (canClaim ? Hud.buttonEnabled : Hud.accent) : claimed ? Hud.panel : Hud.panelLight;
                _cellDay[d].color = current ? Hud.text : Hud.mutedText;
            }
            _streak.text = new LocTemplate("login.streak").Format(daily.Streak) + (daily.Streak >= 3 ? "  !" : "");
            var sb = new StringBuilder();
            if (daily.IsStreakInDanger(meta.DayKey)) sb.Append(Loc.Get("login.streak_danger")).Append('\n');
            sb.Append(Loc.Get("login.shield")).Append(" : ").Append(daily.Shields).Append('/').Append(daily.Login.MaxShields);
            _info.text = sb.ToString();
            _claim.SetInteractable(canClaim);
            _claim.Label.text = canClaim ? Loc.Get("login.claim") : Loc.Get("login.tomorrow");
            bool shield = daily.CanEarnShield(meta.Clock.LocalWeekKey) && meta.TimeTrusted && _ctx.AdReady("streak_shield");
            _shield.transform.parent.gameObject.SetActive(shield);
        }

        public override void Tick(float dt)
        {
            if (!IsOpen) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 1f;
            if (_ctx.Meta.CanClaimLogin != _claim.Interactable) Refresh();
        }
    }

    /// <summary>
    /// Roue Teranga : segments proportionnels aux poids (probabilités affichées), 1 tour gratuit + rewarded par jour.
    /// Le tirage est fait par la méta (déterministe) ; la roue ne fait que tourner jusqu'au segment tiré.
    /// </summary>
    public sealed class WheelScreen : MetaScreen
    {
        private const float SpinSeconds = 3.2f;
        private readonly MetaUiContext _ctx;
        private readonly RectTransform _wheel;
        private readonly float[] _segmentCenter;
        private readonly UiButton _free, _ad, _oddsButton;
        private readonly TextMeshProUGUI _result, _odds;
        private readonly LocTemplate _adTemplate;
        private float _spinTime, _from, _to;
        private int _pending = -1;

        public WheelScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Wheel", ctx.Hud, ctx.Assets, new Vector2(1000f, 1450f), Loc.Get("wheel.title"))
        {
            _ctx = ctx;
            _adTemplate = new LocTemplate("wheel.spin_ad");
            var wheel = ctx.Meta.Definitions.Wheel;
            var segments = wheel.Segments;
            _wheel = UiFactory.Anchored(Card, "WheelDisc", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -560f), new Vector2(760f, 760f));
            UiFactory.Image(_wheel, Hud.panelLight, Assets.circle);
            _segmentCenter = new float[segments.Length];
            double total = 0d;
            foreach (var s in segments) total += s.Weight;
            Color[] palette = { new Color(0.95f, 0.62f, 0.12f), new Color(0.22f, 0.70f, 0.32f), new Color(0.25f, 0.55f, 0.95f), new Color(0.85f, 0.25f, 0.45f) };
            float start = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                float frac = (float)(segments[i].Weight / total);
                var segRt = UiFactory.Stretch(_wheel, "Segment" + i);
                segRt.offsetMin = new Vector2(10f, 10f);
                segRt.offsetMax = new Vector2(-10f, -10f);
                var img = UiFactory.Image(segRt, palette[i % palette.Length], Assets.circle);
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = frac;
                segRt.localEulerAngles = new Vector3(0f, 0f, -start * 360f);
                float center = (start + frac * 0.5f) * 360f;
                _segmentCenter[i] = center;
                // Libellé au milieu du segment, orienté vers l'extérieur.
                var labelPivot = UiFactory.Anchored(_wheel, "LabelPivot" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                labelPivot.localEulerAngles = new Vector3(0f, 0f, -center);
                var label = UiFactory.Anchored(labelPivot, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(200f, 90f));
                var t = UiFactory.Text(label, Hud, Hud.fontSizeSmall * 0.6f, Hud.text, TextAlignmentOptions.Center,
                    MetaText.Reward(segments[i].Reward).Replace("  ·  ", "\n"));
                MetaUiKit.Outline(t);
                start += frac;
            }
            var hub = UiFactory.Anchored(_wheel, "Hub", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            UiFactory.Image(hub, Hud.panel, Assets.circle);
            // Pointeur fixe en haut.
            var pointer = UiFactory.Anchored(Card, "Pointer", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(60f, 70f));
            UiFactory.Image(pointer, Hud.text, Assets.rounded);

            _result = MetaUiKit.Label(Card, "Result", Hud, Hud.fontSizeMedium, Hud.cauris, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(30f, 330f), new Vector2(-30f, 400f), TextAlignmentOptions.Center);
            _free = MetaUiKit.Button(Card, "Free", Hud, Assets, Loc.Get("wheel.spin_free"), Hud.fontSizeMedium,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-320f, 180f), new Vector2(320f, 310f));
            _free.Clicked = () => Spin(false);
            _ad = MetaUiKit.Button(Card, "Ad", Hud, Assets, "", Hud.fontSizeSmall, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-320f, 180f), new Vector2(320f, 310f), Hud.accent);
            _ad.Clicked = () => _ctx.Rewarded("wheel_spin", () => Spin(true));
            _oddsButton = MetaUiKit.Button(Card, "OddsButton", Hud, Assets, Loc.Get("wheel.odds"), Hud.fontSizeSmall * 0.8f,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-180f, 50f), new Vector2(180f, 140f), Hud.panelLight);
            _odds = MetaUiKit.Paragraph(Card, "Odds", Hud, Hud.fontSizeSmall * 0.7f, Hud.text, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.85f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            _odds.transform.SetAsLastSibling();
            _odds.gameObject.SetActive(false);
            _oddsButton.Clicked = () => { _odds.gameObject.SetActive(!_odds.gameObject.activeSelf); if (_odds.gameObject.activeSelf) FillOdds(); };
        }

        private void FillOdds()
        {
            var daily = _ctx.Meta.Daily;
            var segments = daily.Wheel.Segments;
            var sb = new StringBuilder();
            for (int i = 0; i < segments.Length; i++)
                sb.Append(MetaText.Reward(segments[i].Reward)).Append(" : ").Append((daily.Probability(i) * 100d).ToString("0.#")).Append(" %\n");
            _odds.text = sb.ToString();
        }

        private void Spin(bool rewarded)
        {
            if (_pending >= 0) return;
            int index = _ctx.Meta.SpinWheel(rewarded);
            if (index < 0) return;
            _pending = index;
            _spinTime = 0f;
            _from = Mathf.Repeat(_wheel.localEulerAngles.z, 360f);
            // Le segment tiré finit sous le pointeur (en haut) après 4 tours complets.
            _to = 360f * 4f + _segmentCenter[index];
            _result.text = string.Empty;
            _ctx.Audio?.Play(SoundId.Button);
            Refresh();
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            bool spinning = _pending >= 0;
            bool free = meta.CanSpinFree;
            bool ad = !free && meta.CanSpinRewarded && _ctx.AdReady("wheel_spin");
            _free.transform.parent.gameObject.SetActive(free || (!ad && !spinning));
            _free.SetInteractable(free && !spinning);
            if (!free) _free.Label.text = Loc.Get("wheel.done");
            else _free.Label.text = Loc.Get("wheel.spin_free");
            _ad.transform.parent.gameObject.SetActive(ad);
            _ad.SetInteractable(!spinning);
            if (ad)
            {
                var daily = meta.Daily;
                int used = daily.Wheel.RewardedPerDay - daily.RewardedSpinsLeft(meta.DayKey);
                _ad.Label.text = _adTemplate.Format(used + 1, daily.Wheel.RewardedPerDay);
            }
        }

        public override void Tick(float dt)
        {
            if (!IsOpen || _pending < 0) return;
            _spinTime += dt;
            float k = Mathf.Clamp01(_spinTime / SpinSeconds);
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            _wheel.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(_from, _to, eased));
            if (k < 1f) return;
            var reward = _ctx.Meta.Definitions.Wheel.Segments[_pending].Reward;
            _pending = -1;
            _result.text = MetaText.Reward(reward);
            _ctx.Audio?.Play(SoundId.Collect);
            Refresh();
        }

        protected override void OnHidden()
        {
            // Fermé pendant la rotation : la récompense est déjà créditée, on arrête juste l'animation.
            if (_pending < 0) return;
            _wheel.localEulerAngles = new Vector3(0f, 0f, _to);
            _pending = -1;
        }
    }
}
