using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>3 missions actives (réclamer, x2 via rewarded, changer) + 3 défis du jour (renouvelés à minuit local).</summary>
    public sealed class MissionsScreen : MetaScreen
    {
        private sealed class Slot
        {
            public RectTransform Root;
            public TextMeshProUGUI Text, Reward, Progress;
            public Image Fill;
            public UiButton Claim, Double, Replace;
            public readonly TextBuffer ProgressText = new TextBuffer(48), InfoText = new TextBuffer(96);
            public MissionInstance Shown;
        }

        private const float RowHeight = 230f;
        private readonly MetaUiContext _ctx;
        private readonly Slot[] _missions, _challenges;
        private readonly TextMeshProUGUI _challengeTitle, _challengeInfo;
        private readonly TextBuffer _infoText = new TextBuffer(96);
        private readonly LocTemplate _nextIn, _renew;
        private readonly string _replaceFree, _replaceAd, _replaceCauris, _claimedLabel;
        private float _refresh;

        public MissionsScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "Missions", ctx.Hud, ctx.Assets, new Vector2(1000f, 1640f), Loc.Get("missions.title"))
        {
            _ctx = ctx;
            _nextIn = new LocTemplate("missions.next_in");
            _renew = new LocTemplate("challenges.renew");
            _replaceFree = Loc.Get("missions.replace_free");
            _replaceAd = Loc.Get("missions.replace_ad");
            _replaceCauris = Loc.Get("missions.replace") + " (" + ctx.Meta.Definitions.MissionRules.ReplaceCauris + ")";
            _claimedLabel = "✓";
            var meta = ctx.Meta;
            _missions = new Slot[meta.Missions.Slots];
            for (int s = 0; s < _missions.Length; s++) _missions[s] = BuildSlot(s, 130f + s * RowHeight, false);

            float y = 130f + _missions.Length * RowHeight + 20f;
            _challengeTitle = MetaUiKit.Label(Card, "ChallengesTitle", Hud, Hud.fontSizeMedium, Hud.accent, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -y - 60f), new Vector2(-40f, -y), TextAlignmentOptions.Left, Loc.Get("challenges.title"));
            _challengeInfo = MetaUiKit.Label(Card, "ChallengesInfo", Hud, Hud.fontSizeSmall * 0.85f, Hud.mutedText, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -y - 104f), new Vector2(-40f, -y - 60f), TextAlignmentOptions.Left);
            _challenges = new Slot[meta.Challenges.Slots];
            for (int s = 0; s < _challenges.Length; s++) _challenges[s] = BuildSlot(s, y + 110f + s * RowHeight, true);
        }

        private Slot BuildSlot(int index, float top, bool challenge)
        {
            var hud = Hud;
            var slot = new Slot();
            slot.Root = UiFactory.Rect(Card, (challenge ? "Challenge" : "Mission") + index, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -top - RowHeight + 10f), new Vector2(-30f, -top));
            UiFactory.Image(slot.Root, hud.panelLight, Assets.rounded);
            slot.Text = MetaUiKit.Paragraph(slot.Root, "Text", hud, hud.fontSizeSmall, hud.text, new Vector2(0f, 0.5f), new Vector2(0.62f, 1f),
                new Vector2(24f, 0f), new Vector2(0f, -12f));
            slot.Fill = MetaUiKit.Bar(slot.Root, "Bar", hud, Assets, hud.money, new Vector2(0f, 0.3f), new Vector2(0.62f, 0.46f), new Vector2(24f, 0f), Vector2.zero);
            slot.Progress = MetaUiKit.Label(slot.Root, "Progress", hud, hud.fontSizeSmall * 0.8f, hud.mutedText, new Vector2(0f, 0.3f), new Vector2(0.62f, 0.46f),
                new Vector2(24f, 0f), Vector2.zero, TextAlignmentOptions.Center);
            MetaUiKit.Outline(slot.Progress);
            slot.Reward = MetaUiKit.Label(slot.Root, "Reward", hud, hud.fontSizeSmall * 0.8f, hud.cauris, new Vector2(0f, 0f), new Vector2(0.62f, 0.28f),
                new Vector2(24f, 6f), Vector2.zero);
            int captured = index;
            slot.Claim = MetaUiKit.Button(slot.Root, "Claim", hud, Assets, Loc.Get("missions.claim"), hud.fontSizeSmall,
                new Vector2(0.64f, 0.52f), new Vector2(0.98f, 0.94f), Vector2.zero, Vector2.zero);
            slot.Double = MetaUiKit.Button(slot.Root, "Double", hud, Assets, Loc.Get("missions.double"), hud.fontSizeSmall * 0.85f,
                new Vector2(0.64f, 0.06f), new Vector2(0.98f, 0.48f), Vector2.zero, Vector2.zero, hud.accent);
            if (challenge)
            {
                slot.Claim.Clicked = () => { if (_ctx.Meta.ClaimChallenge(captured)) OnClaimed(); };
                slot.Double.gameObject.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                slot.Claim.Clicked = () => { if (_ctx.Meta.ClaimMission(captured)) OnClaimed(); };
                slot.Double.Clicked = () => _ctx.Rewarded("mission_double", () => { if (_ctx.Meta.ClaimMission(captured, true)) OnClaimed(); });
                slot.Replace = MetaUiKit.Button(slot.Root, "Replace", hud, Assets, _replaceFree, hud.fontSizeSmall * 0.8f,
                    new Vector2(0.64f, 0.06f), new Vector2(0.98f, 0.48f), Vector2.zero, Vector2.zero, hud.panel);
                slot.Replace.Clicked = () => ReplaceMission(captured);
            }
            return slot;
        }

        private void ReplaceMission(int slot)
        {
            var meta = _ctx.Meta;
            if (meta.HasFreeReplace || meta.Cauris >= meta.Definitions.MissionRules.ReplaceCauris && !_ctx.AdReady("mission_replace"))
            {
                if (meta.ReplaceMission(slot, false)) Refresh();
                return;
            }
            _ctx.Rewarded("mission_replace", () => { if (meta.ReplaceMission(slot, true)) Refresh(); });
        }

        private void OnClaimed()
        {
            _ctx.Audio?.Play(SoundId.Collect);
            Refresh();
        }

        public override void Refresh()
        {
            var meta = _ctx.Meta;
            for (int s = 0; s < _missions.Length; s++) Fill(_missions[s], meta.Missions[s], false, meta.Missions.RefillAt(s));
            bool challenges = meta.IsUnlocked(Feature.DailyChallenges);
            _challengeTitle.gameObject.SetActive(challenges);
            _challengeInfo.gameObject.SetActive(challenges);
            for (int s = 0; s < _challenges.Length; s++)
            {
                _challenges[s].Root.gameObject.SetActive(challenges);
                if (challenges) Fill(_challenges[s], meta.Challenges[s], true, 0d);
            }
            UpdateDynamic();
        }

        private void Fill(Slot slot, MissionInstance m, bool challenge, double refillAt)
        {
            slot.Shown = m;
            bool has = m != null;
            slot.Fill.transform.parent.gameObject.SetActive(has);
            slot.Reward.gameObject.SetActive(has);
            slot.Claim.transform.parent.gameObject.SetActive(has);
            if (!challenge) slot.Double.transform.parent.gameObject.SetActive(has);
            if (slot.Replace != null) slot.Replace.transform.parent.gameObject.SetActive(has);
            if (!has)
            {
                slot.Text.text = string.Empty;
                return;
            }
            slot.Text.text = MetaText.Mission(m, _ctx.BusinessName);
            slot.Reward.text = MetaText.MissionReward(m);
        }

        /// <summary>Progression, boutons, minuteries : tampons de caractères, aucune allocation.</summary>
        private void UpdateDynamic()
        {
            var meta = _ctx.Meta;
            bool adReady = _ctx.AdReady("mission_double");
            for (int s = 0; s < _missions.Length; s++)
            {
                var slot = _missions[s];
                var m = slot.Shown;
                if (m == null)
                {
                    slot.InfoText.Clear().Append(_nextIn.Prefix).AppendDuration(meta.Missions.RefillAt(s) - meta.Clock.TrustedSeconds).Append(_nextIn.Suffix)
                        .ApplyIfChanged(slot.Text);
                    continue;
                }
                UpdateProgress(slot, m);
                bool complete = meta.CanClaimMission(s);
                slot.Claim.SetInteractable(complete);
                slot.Double.transform.parent.gameObject.SetActive(complete && adReady);
                slot.Replace.transform.parent.gameObject.SetActive(!complete);
                slot.Replace.Label.text = meta.HasFreeReplace ? _replaceFree : _ctx.AdReady("mission_replace") ? _replaceAd : _replaceCauris;
            }
            if (!meta.IsUnlocked(Feature.DailyChallenges)) return;
            for (int s = 0; s < _challenges.Length; s++)
            {
                var slot = _challenges[s];
                if (slot.Shown == null) continue;
                UpdateProgress(slot, slot.Shown);
                slot.Claim.SetInteractable(meta.CanClaimChallenge(s));
                if (slot.Shown.Claimed) slot.Claim.Label.text = _claimedLabel;
            }
            _infoText.Clear().Append(Loc.Get("challenges.bonus")).Append("   ").Append(_renew.Prefix)
                .AppendDuration(meta.Clock.SecondsToLocalMidnight).Append(_renew.Suffix).ApplyIfChanged(_challengeInfo);
        }

        private void UpdateProgress(Slot slot, MissionInstance m)
        {
            double current = System.Math.Min(m.Target, System.Math.Max(0d, m.Current(_ctx.Meta)));
            slot.Fill.fillAmount = (float)m.Progress01(_ctx.Meta);
            slot.ProgressText.Clear().Append(BigDouble.FromDouble(System.Math.Floor(current))).Append(" / ").Append(BigDouble.FromDouble(m.Target))
                .ApplyIfChanged(slot.Progress);
        }

        public override void Tick(float dt)
        {
            if (!IsOpen) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 0.25f;
            // Une mission rechargée apparaît : on reconstruit les textes (rare).
            var meta = _ctx.Meta;
            for (int s = 0; s < _missions.Length; s++)
                if (_missions[s].Shown != meta.Missions[s]) { Refresh(); return; }
            UpdateDynamic();
        }
    }
}
