using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>
    /// APK de développement pour les mesures sur appareil (IL2CPP, ARM64, Development Build + Profiler).
    /// Batch : -executeMethod Gargote.EditorTools.AndroidBuild.DevApk  → Builds/Android/GargoteTycoon-dev.apk
    /// </summary>
    public static class AndroidBuild
    {
        public const string Output = "Builds/Android/GargoteTycoon-dev.apk";

        [MenuItem("Gargote/M2/3. Build dev APK (Android)")]
        public static void DevApkMenu() => Build(false);

        /// <summary>APK de mesure (démarrage à froid, mémoire) : Development sans connexion automatique au Profiler.</summary>
        [MenuItem("Gargote/M3/Build dev APK (mesures, sans Profiler)")]
        public static void MeasureApkMenu() => Build(false, false);

        public const string ReleaseProbeOutput = "Builds/Android/GargoteTycoon-release-probe.apk";

        /// <summary>
        /// APK release (sans Development) avec la sonde de démarrage (define GARGOTE_PROBE) : mesure du démarrage à froid
        /// dans les conditions du store. Snapshot mémoire indisponible (réservé aux builds de développement).
        /// </summary>
        [MenuItem("Gargote/M3/Build release APK (mesure démarrage)")]
        public static void ReleaseProbeApkMenu() => Build(false, false, true);

        public static void DevApk()
        {
            bool ok = Build(true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Build Windows de développement (portrait fenêtré) pour les captures et mesures sur PC :
        /// GargoteTycoon.exe -popupwindow -screen-width 540 -screen-height 1170 -gargoteCapture &lt;dossier&gt;
        /// </summary>
        public const string WindowsOutput = "Builds/Windows/GargoteTycoon.exe";

        public static void DevWindows()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1170;
            PlayerSettings.resizableWindow = true;
            AddressableAssetSettings.BuildPlayerContent(out var addressables);
            bool ok = string.IsNullOrEmpty(addressables.Error);
            if (ok)
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { M2Builder.ScenePath },
                    locationPathName = WindowsOutput,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development,
                });
                ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log("[M2] Windows " + report.summary.result + " -> " + WindowsOutput);
            }
            else Debug.LogError("[M2] Addressables: " + addressables.Error);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(bool batch, bool connectProfiler = true, bool release = false)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Smart IA Services";
            PlayerSettings.productName = "Gargote Tycoon";
            PlayerSettings.SetApplicationIdentifier(android, "com.smartiaservices.gargotetycoon");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.optimizedFramePacing = true;
            EditorUserBuildSettings.buildAppBundle = false;
            // L'attente de connexion du Profiler (≈ 10 s au démarrage) fausserait la mesure du démarrage à froid.
            EditorUserBuildSettings.connectProfiler = connectProfiler;
            EditorUserBuildSettings.waitForManagedDebugger = false;
            // Démarrage à froid < 3 s : pas d'écran « Made with Unity » (optionnel sur tous les plans depuis Unity 6).
            PlayerSettings.SplashScreen.show = false;

            AddressableAssetSettings.BuildPlayerContent(out var addressables);
            if (!string.IsNullOrEmpty(addressables.Error))
            {
                Debug.LogError("[M2] Addressables: " + addressables.Error);
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { M2Builder.ScenePath },
                locationPathName = release ? ReleaseProbeOutput : Output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = release ? BuildOptions.None
                    : BuildOptions.Development | (connectProfiler ? BuildOptions.ConnectWithProfiler : BuildOptions.None),
                extraScriptingDefines = release ? new[] { "GARGOTE_PROBE" } : null,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("[M2] APK " + summary.result + " — " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " Mo, " +
                      summary.totalTime.TotalSeconds.ToString("0") + " s → " + options.locationPathName);
            return summary.result == BuildResult.Succeeded;
        }
    }
}
