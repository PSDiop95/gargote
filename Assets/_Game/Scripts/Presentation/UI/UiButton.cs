using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Bouton placeholder : squash au toucher, grisé si indisponible, s'illumine quand il devient utilisable.
    /// Ne bloque jamais l'input (aucune animation n'empêche un clic).
    /// </summary>
    public sealed class UiButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        public Image Background { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public bool Interactable { get; private set; } = true;
        public Action Clicked;
        public Color EnabledColor;
        public Color DisabledColor;
        public float PunchStrength = 0.1f;

        private HudConfig _hud;
        private Tween _punch, _glow;
        private bool _initialized;

        public void Init(Image background, TextMeshProUGUI label, HudConfig hud)
        {
            Background = background;
            Label = label;
            _hud = hud;
            EnabledColor = hud.buttonEnabled;
            DisabledColor = hud.buttonDisabled;
            _initialized = true;
        }

        /// <summary>Change l'état ; passage grisé → disponible = illumination (glow).</summary>
        public void SetInteractable(bool value, bool glowOnEnable = true)
        {
            if (_initialized && value == Interactable && Background.color != Color.clear) return;
            bool becameAvailable = value && !Interactable;
            Interactable = value;
            if (_glow.isAlive) _glow.Stop();
            Background.color = value ? EnabledColor : DisabledColor;
            Label.alpha = value ? 1f : 0.6f;
            if (becameAvailable && glowOnEnable && isActiveAndEnabled)
                _glow = Tween.Color(Background, _hud.buttonGlow, EnabledColor, 0.45f, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Juice.Punch(ref _punch, transform, Vector3.one, PunchStrength, 0.2f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            Clicked?.Invoke();
        }

        private void OnDisable()
        {
            if (_punch.isAlive) _punch.Stop();
            if (_glow.isAlive) _glow.Stop();
            transform.localScale = Vector3.one;
            if (Background != null) Background.color = Interactable ? EnabledColor : DisabledColor;
        }
    }
}
