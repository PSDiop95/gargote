using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Rendu du cycle jour/nuit piloté par <see cref="GameTime"/> : lumière directionnelle (couleur, intensité,
    /// hauteur), ambiance, ciel, et lampes émissives via le global _GargoteNight. Aucune lumière dynamique ajoutée.
    /// </summary>
    public sealed class DayNightDirector
    {
        private static readonly int NightId = Shader.PropertyToID("_GargoteNight");
        private readonly DayNightVisualConfig _config;
        private readonly Light _sun;
        private readonly Camera _camera;

        /// <summary>0 = plein jour, 1 = nuit (lampes allumées).</summary>
        public float Night { get; private set; }

        public DayNightDirector(DayNightVisualConfig config, Light sun, Camera camera)
        {
            _config = config;
            _sun = sun;
            _camera = camera;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            _camera.clearFlags = CameraClearFlags.SolidColor;
        }

        public void Apply(float dayFraction)
        {
            float t = Mathf.Repeat(dayFraction, 1f);
            _sun.color = _config.sunColor.Evaluate(t);
            _sun.intensity = _config.sunIntensity.Evaluate(t);
            _sun.transform.rotation = Quaternion.Euler(_config.sunPitch.Evaluate(t), _config.sunYaw, 0f);
            RenderSettings.ambientLight = _config.ambient.Evaluate(t);
            _camera.backgroundColor = _config.sky.Evaluate(t);
            Night = Mathf.Clamp01(_config.lampIntensity.Evaluate(t));
            // L'intensité nocturne max (lampEmissionBoost) est portée par le matériau (_EmissionStrength).
            Shader.SetGlobalFloat(NightId, Night);
        }
    }
}
