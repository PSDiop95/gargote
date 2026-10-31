using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Caméra orthographique isométrique qui suit l'axe de la rue (Z). Scroll au doigt avec inertie et rebond
    /// élastique aux bords, pinch-zoom borné, recentrage doux, punch de zoom et secousse légère.
    /// Aucune allocation : tout est en champs.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private CameraConfig _config;
        private Camera _camera;
        private float _minZ, _maxZ;
        private float _focusZ, _velocity, _zoom = 1f;
        private bool _dragging;
        private float _focusTweenFrom, _focusTweenTo, _focusTweenT = -1f;
        private float _zoomPunchT = -1f, _zoomPunchAmount;
        private float _shakeT = -1f, _shakeDuration, _shakeStrength;
        private readonly Vector3[] _corners = new Vector3[4];

        public Camera Camera => _camera;
        public float FocusZ => _focusZ;
        public float Zoom => _zoom;
        /// <summary>Étendue Z du sol visible (mise à jour chaque frame).</summary>
        public float VisibleMinZ { get; private set; }
        public float VisibleMaxZ { get; private set; }
        /// <summary>Bande verticale (viewport 0-1) laissée libre par le HUD et le panneau bas (<see cref="UiLayout"/>).</summary>
        public Vector2 StreetViewport = new Vector2(0f, 1f);
        /// <summary>Plage Z au sol visible dans la zone de rue (hors HUD et panneau) : apparitions des clients spéciaux.</summary>
        public float StreetMinZ { get; private set; }
        public float StreetMaxZ { get; private set; }
        public float VisibleMinX { get; private set; }
        public float VisibleMaxX { get; private set; }

        public void Setup(CameraConfig config, Camera camera, float minZ, float maxZ, float startZ)
        {
            _config = config;
            _camera = camera;
            _camera.orthographic = true;
            _minZ = minZ - config.boundsMargin;
            _maxZ = maxZ + config.boundsMargin;
            _focusZ = Mathf.Clamp(startZ, _minZ, _maxZ);
            Apply();
        }

        public void BeginDrag()
        {
            _dragging = true;
            _velocity = 0f;
            _focusTweenT = -1f;
        }

        /// <summary>Glissé d'un delta écran (pixels) : convertit en unités monde le long de la rue.</summary>
        public void Drag(Vector2 screenDelta, float deltaTime)
        {
            float worldPerPixel = WorldPerPixelZ();
            float dz = -screenDelta.y * worldPerPixel;
            // Résistance élastique au-delà des bords.
            float over = Overshoot(_focusZ + dz);
            if (over != 0f) dz *= Mathf.Clamp01(1f - Mathf.Abs(over) / Mathf.Max(0.01f, _config.overscroll));
            _focusZ += dz;
            if (deltaTime > 0f)
                _velocity = Mathf.Clamp(Mathf.Lerp(_velocity, dz / deltaTime, 0.5f), -_config.maxInertiaSpeed, _config.maxInertiaSpeed);
        }

        public void EndDrag() => _dragging = false;

        public void PinchZoom(float factor)
        {
            if (factor <= 0f) return;
            _zoom = Mathf.Clamp(_zoom / factor, _config.minZoom, _config.maxZoom);
        }

        public void WheelZoom(float steps) =>
            _zoom = Mathf.Clamp(_zoom - steps * _config.wheelZoomStep, _config.minZoom, _config.maxZoom);

        /// <summary>Recentrage doux sur une position de la rue (le commerce apparaît au-dessus du panneau bas).</summary>
        public void FocusOn(float z)
        {
            float target = Mathf.Clamp(z - _config.focusScreenOffset * _zoom, _minZ, _maxZ);
            if (Mathf.Abs(target - _focusZ) < 0.75f) return;
            _focusTweenFrom = _focusZ;
            _focusTweenTo = target;
            _focusTweenT = 0f;
            _velocity = 0f;
        }

        public void ZoomPunch()
        {
            _zoomPunchT = 0f;
            _zoomPunchAmount = _config.milestoneZoomPunch;
        }

        public void Shake(float strength, float duration)
        {
            if (strength <= 0f) return;
            _shakeT = 0f;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeStrength = strength;
        }

        private float WorldPerPixelZ()
        {
            float worldHeight = _camera.orthographicSize * 2f;
            float pitch = Mathf.Max(10f, _config.pitch) * Mathf.Deg2Rad;
            return worldHeight / Mathf.Max(1f, Screen.height) / Mathf.Sin(pitch);
        }

        private float Overshoot(float z) => z < _minZ ? z - _minZ : z > _maxZ ? z - _maxZ : 0f;

        private void LateUpdate()
        {
            if (_config == null) return;
            float dt = Time.unscaledDeltaTime;

            if (_focusTweenT >= 0f)
            {
                _focusTweenT += dt / Mathf.Max(0.01f, _config.focusDuration);
                float t = Mathf.Clamp01(_focusTweenT);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                _focusZ = Mathf.Lerp(_focusTweenFrom, _focusTweenTo, eased);
                if (t >= 1f) _focusTweenT = -1f;
            }
            else if (!_dragging)
            {
                _focusZ += _velocity * dt;
                _velocity *= Mathf.Exp(-_config.inertiaDamping * dt);
                float over = Overshoot(_focusZ);
                if (over != 0f)
                {
                    // Ressort de rebond vers le bord, et coupe l'inertie qui pousse dehors.
                    _focusZ -= over * Mathf.Clamp01(_config.bounceStiffness * dt);
                    if (Mathf.Sign(_velocity) == Mathf.Sign(over)) _velocity *= 0.5f;
                    float limit = _config.overscroll;
                    _focusZ = Mathf.Clamp(_focusZ, _minZ - limit, _maxZ + limit);
                }
                if (Mathf.Abs(_velocity) < 0.01f) _velocity = 0f;
            }
            Apply();
        }

        private void Apply()
        {
            float zoom = _zoom;
            if (_zoomPunchT >= 0f)
            {
                _zoomPunchT += Time.unscaledDeltaTime / Mathf.Max(0.01f, _config.milestoneZoomDuration);
                zoom *= 1f - _zoomPunchAmount * Mathf.Sin(Mathf.Clamp01(_zoomPunchT) * Mathf.PI);
                if (_zoomPunchT >= 1f) _zoomPunchT = -1f;
            }
            // Portrait étroit (19,5:9) ou tablette : la largeur utile de la rue reste visible.
            float baseSize = Mathf.Max(_config.orthographicSize, _config.visibleHalfWidth / Mathf.Max(0.1f, _camera.aspect));
            _camera.orthographicSize = baseSize * zoom;

            var rotation = Quaternion.Euler(_config.pitch, _config.yaw, 0f);
            var target = new Vector3(_config.lookOffsetX, 0f, _focusZ);
            Vector3 position = target - rotation * Vector3.forward * _config.distance;
            if (_shakeT >= 0f)
            {
                _shakeT += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(_shakeT / _shakeDuration);
                float s = _shakeStrength * k;
                position += rotation * new Vector3(Mathf.Sin(_shakeT * 61f) * s, Mathf.Sin(_shakeT * 47f + 1.3f) * s, 0f);
                if (k <= 0f) _shakeT = -1f;
            }
            transform.SetPositionAndRotation(position, rotation);
            if (_camera.transform != transform) _camera.transform.SetPositionAndRotation(position, rotation);
            UpdateVisibleRange();
        }

        private void UpdateVisibleRange()
        {
            float minZ = float.MaxValue, maxZ = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var vp = new Vector3(i == 1 || i == 2 ? 1f : 0f, i >= 2 ? 1f : 0f, 0f);
                Ray ray = _camera.ViewportPointToRay(vp);
                float t = Mathf.Abs(ray.direction.y) > 1e-4f ? -ray.origin.y / ray.direction.y : 0f;
                Vector3 p = ray.origin + ray.direction * t;
                _corners[i] = p;
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            }
            VisibleMinZ = minZ; VisibleMaxZ = maxZ; VisibleMinX = minX; VisibleMaxX = maxX;
            float z0 = GroundZ(new Vector3(0.5f, StreetViewport.x, 0f)), z1 = GroundZ(new Vector3(0.5f, StreetViewport.y, 0f));
            StreetMinZ = Mathf.Min(z0, z1);
            StreetMaxZ = Mathf.Max(z0, z1);
        }

        private float GroundZ(Vector3 viewport)
        {
            Ray ray = _camera.ViewportPointToRay(viewport);
            float t = Mathf.Abs(ray.direction.y) > 1e-4f ? -ray.origin.y / ray.direction.y : 0f;
            return (ray.origin + ray.direction * t).z;
        }

        /// <summary>Un point au sol (et sa hauteur) est-il dans l'écran, avec une marge en unités monde ?</summary>
        public bool IsVisible(Vector3 worldPosition, float margin)
        {
            Vector3 vp = _camera.WorldToViewportPoint(worldPosition);
            float mx = margin / Mathf.Max(0.01f, _camera.orthographicSize * 2f * _camera.aspect);
            float my = margin / Mathf.Max(0.01f, _camera.orthographicSize * 2f);
            return vp.z > 0f && vp.x > -mx && vp.x < 1f + mx && vp.y > -my && vp.y < 1f + my;
        }
    }
}
