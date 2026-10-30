using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Gargote.Presentation
{
    /// <summary>
    /// Entrées monde (Input System) : tap, glissé vertical vers la caméra, pinch-zoom, molette en éditeur.
    /// Un geste qui commence sur l'UI lui appartient entièrement. Aucune allocation par frame.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        private CameraRig _rig;
        private CameraConfig _config;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>(8);
        private PointerEventData _pointer;
        private bool _pressed, _overUi, _dragging, _pinching;
        private Vector2 _start, _last;
        private float _pinchDistance;

        /// <summary>Tap sur le monde (position écran).</summary>
        public Action<Vector2> Tapped;

        public void Setup(CameraRig rig, CameraConfig config)
        {
            _rig = rig;
            _config = config;
            EnhancedTouchSupport.Enable();
        }

        private void OnDestroy()
        {
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
        }

        private bool OverUi(Vector2 screen)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (_pointer == null) _pointer = new PointerEventData(system);
            _pointer.Reset();
            _pointer.position = screen;
            _hits.Clear();
            system.RaycastAll(_pointer, _hits);
            return _hits.Count > 0;
        }

        private void Update()
        {
            if (_rig == null) return;
            float dt = Time.unscaledDeltaTime;
            var touches = Touch.activeTouches;
            if (touches.Count > 0)
            {
                if (touches.Count >= 2)
                {
                    float distance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                    if (!_pinching)
                    {
                        _pinching = true;
                        _pinchDistance = distance;
                        if (_dragging) { _dragging = false; _rig.EndDrag(); }
                        _overUi = true; // le relâchement d'un pinch n'est jamais un tap
                    }
                    else if (_pinchDistance > 1f && distance > 1f)
                    {
                        _rig.PinchZoom(distance / _pinchDistance);
                        _pinchDistance = distance;
                    }
                    return;
                }
                _pinching = false;
                var touch = touches[0];
                Pointer(touch.phase == UnityEngine.InputSystem.TouchPhase.Began,
                    touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled,
                    touch.screenPosition, dt);
                return;
            }
            _pinching = false;

            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !OverUi(mouse.position.ReadValue())) _rig.WheelZoom(Mathf.Sign(scroll));
            bool down = mouse.leftButton.wasPressedThisFrame;
            bool up = mouse.leftButton.wasReleasedThisFrame;
            if (down || up || mouse.leftButton.isPressed) Pointer(down, up, mouse.position.ReadValue(), dt);
        }

        private void Pointer(bool began, bool ended, Vector2 position, float dt)
        {
            if (began)
            {
                _pressed = true;
                _overUi = OverUi(position);
                _dragging = false;
                _start = _last = position;
                return;
            }
            if (!_pressed) return;

            if (!_overUi)
            {
                if (!_dragging && (position - _start).sqrMagnitude > _config.dragThresholdPixels * _config.dragThresholdPixels)
                {
                    _dragging = true;
                    _rig.BeginDrag();
                }
                if (_dragging) _rig.Drag(position - _last, dt);
            }
            _last = position;

            if (!ended) return;
            _pressed = false;
            if (_overUi) return;
            if (_dragging)
            {
                _dragging = false;
                _rig.EndDrag();
                return;
            }
            Tapped?.Invoke(position);
        }
    }
}
