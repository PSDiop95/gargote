using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gargote.Presentation
{
    /// <summary>
    /// Qualité automatique Bas / Moyen / Haut : applique ombres, MSAA, échelle de rendu et HDR (off) sur une
    /// copie runtime de l'asset URP (l'asset du projet n'est jamais modifié), plus densité de foule et particules.
    /// </summary>
    public sealed class QualityDirector
    {
        private readonly QualityConfig _config;
        private RenderPipelineAsset _original;
        private UniversalRenderPipelineAsset _runtime;

        public QualityTier Tier { get; private set; }
        public QualityTierSettings Settings { get; private set; }

        public QualityDirector(QualityConfig config) => _config = config;

        public QualityTier Detect() => _config.Detect(SystemInfo.systemMemorySize, SystemInfo.graphicsMemorySize);

        public QualityTierSettings Apply(QualityTier tier)
        {
            Tier = tier;
            Settings = _config.Get(tier);
            var current = _config.pipeline != null ? _config.pipeline
                : QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (_runtime == null && current is UniversalRenderPipelineAsset urp)
            {
                _original = QualitySettings.renderPipeline;
                _runtime = Object.Instantiate(urp);
                _runtime.name = urp.name + " (runtime)";
            }
            if (_runtime != null)
            {
                _runtime.shadowDistance = Settings.shadows ? Settings.shadowDistance : 0f;
                _runtime.msaaSampleCount = Mathf.Clamp(Settings.msaa, 1, 2);
                _runtime.renderScale = Settings.renderScale;
                _runtime.supportsHDR = false;
                QualitySettings.renderPipeline = _runtime;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Settings.targetFrameRate;
            MatchDisplayRefresh(Settings.targetFrameRate);
            return Settings;
        }

        /// <summary>
        /// Écran 90/120 Hz : une cible de 60 s'aligne sur un diviseur du rafraîchissement (45 FPS à 90 Hz).
        /// On demande le mode d'affichage le plus proche de la cible (60 Hz) pour obtenir 60 FPS réguliers.
        /// </summary>
        private static void MatchDisplayRefresh(int targetFps)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            double current = Screen.currentResolution.refreshRateRatio.value;
            if (targetFps <= 0 || current <= targetFps + 1d) return;
            try
            {
                // Certains constructeurs (Samsung) ignorent Screen.SetResolution : on fixe le mode d'affichage préféré
                // de la fenêtre (même définition, rafraîchissement le plus proche de la cible).
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    var window = activity.Call<AndroidJavaObject>("getWindow");
                    var display = window.Call<AndroidJavaObject>("getWindowManager").Call<AndroidJavaObject>("getDefaultDisplay");
                    var mode = display.Call<AndroidJavaObject>("getMode");
                    int width = mode.Call<int>("getPhysicalWidth"), height = mode.Call<int>("getPhysicalHeight");
                    int bestId = -1;
                    float bestGap = float.MaxValue;
                    foreach (var m in display.Call<AndroidJavaObject[]>("getSupportedModes"))
                    {
                        if (m.Call<int>("getPhysicalWidth") != width || m.Call<int>("getPhysicalHeight") != height) continue;
                        float gap = Mathf.Abs(m.Call<float>("getRefreshRate") - targetFps);
                        if (gap < bestGap) { bestGap = gap; bestId = m.Call<int>("getModeId"); }
                    }
                    if (bestId < 0 || bestGap > 1f) return;
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        var attributes = window.Call<AndroidJavaObject>("getAttributes");
                        attributes.Set("preferredDisplayModeId", bestId);
                        window.Call("setAttributes", attributes);
                    }));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Quality] Display mode " + targetFps + " Hz unavailable: " + e.Message);
            }
#endif
        }

        /// <summary>Restaure l'asset d'origine (éditeur : ne pas laisser la copie runtime dans les réglages).</summary>
        public void Restore()
        {
            if (_runtime == null) return;
            QualitySettings.renderPipeline = _original;
            Object.Destroy(_runtime);
            _runtime = null;
        }
    }

    /// <summary>Vibration légère (Android : 25 ms via Vibrator ; ailleurs : rien ou vibration système).</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        public static void Light()
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    vibrator?.Call("vibrate", 25L);
            }
            catch (System.Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
