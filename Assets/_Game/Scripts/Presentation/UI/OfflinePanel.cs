using System;
using Gargote.Core;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Écran « Pendant ton absence… » : montant qui défile, pluie de billets, Récupérer / x2 (rewarded MOCK en M2).
    /// N'applique rien lui-même : renvoie le choix au presenter, qui passe par la session.
    /// </summary>
    public sealed class OfflinePanel
    {
        private readonly RectTransform _root, _card;
        private readonly TextMeshProUGUI _amount, _duration;
        private readonly UiButton _claim, _double;
        private readonly TextBuffer _amountText = new TextBuffer(48), _durationText = new TextBuffer(48);
        private BigDouble _target;
        private float _rollT;
        private Action<bool> _onChoice;
        private Tween _pop;

        public bool IsOpen => _root.gameObject.activeSelf;

        public OfflinePanel(RectTransform parent, HudConfig hud, StreetAssets assets)
        {
            _root = UiFactory.Stretch(parent, "Offline");
            UiFactory.Image(_root, hud.modalVeil, null, true);
            _card = UiFactory.Anchored(_root, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f));
            UiFactory.Image(_card, hud.panel, assets.rounded, true);
            UiFactory.Text(UiFactory.Anchored(_card, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 90f)),
                hud, hud.fontSizeLarge * 0.8f, hud.text, TextAlignmentOptions.Center, Loc.Get("offline.title"));
            _duration = UiFactory.Text(UiFactory.Anchored(_card, "Duration", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(820f, 56f)),
                hud, hud.fontSizeSmall, hud.mutedText);
            _amount = UiFactory.Text(UiFactory.Anchored(_card, "Amount", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(820f, 140f)),
                hud, hud.fontSizeLarge * 1.4f, hud.money);
            var claimCell = UiFactory.Anchored(_card, "Claim", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 50f), new Vector2(380f, 150f));
            _claim = UiFactory.Button(claimCell, "Button", hud, assets.rounded, Loc.Get("offline.claim"), hud.fontSizeMedium);
            _claim.Clicked = () => Choose(false);
            var doubleCell = UiFactory.Anchored(_card, "Double", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(210f, 50f), new Vector2(380f, 150f));
            _double = UiFactory.Button(doubleCell, "Button", hud, assets.rounded, Loc.Get("offline.double"), hud.fontSizeMedium);
            _double.EnabledColor = hud.accent;
            _double.Background.color = hud.accent;
            _double.Clicked = () => Choose(true);
            _root.gameObject.SetActive(false);
        }

        public void Show(BigDouble earnings, double awaySeconds, bool adAvailable, Action<bool> onChoice)
        {
            _target = earnings;
            _rollT = 0f;
            _onChoice = onChoice;
            _double.gameObject.SetActive(adAvailable);
            long minutes = (long)(awaySeconds / 60d);
            _durationText.Clear().Append(Loc.Get("offline.away")).Append(' ').AppendLong(minutes / 60).Append("h").AppendLong(minutes % 60).Append("min")
                .ApplyIfChanged(_duration);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Juice.Pop(ref _pop, _card, Vector3.one, 0.4f);
        }

        private void Choose(bool doubled)
        {
            var callback = _onChoice;
            _onChoice = null;
            _root.gameObject.SetActive(false);
            callback?.Invoke(doubled);
        }

        public void Tick(float dt)
        {
            if (!IsOpen) return;
            _rollT = Mathf.Min(1f, _rollT + dt / 1.4f);
            float e = 1f - (1f - _rollT) * (1f - _rollT) * (1f - _rollT);
            _amountText.Clear().Append('+').Append(_target * e).Append(" FCFA").ApplyIfChanged(_amount);
        }
    }
}
