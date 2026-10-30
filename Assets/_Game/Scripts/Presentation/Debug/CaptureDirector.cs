#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gargote.Core;
using Gargote.Services.Save;
using Unity.Profiling;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Pilote de capture (build de développement uniquement, jamais en Release) : rejoue un scénario scripté de la rue
    /// (tap, billets, gérant, palier, déblocage, foule, client spécial, nuit, panneau, retour hors ligne), prend des captures
    /// et mesure les performances en qualité Bas et Haut. Activé seulement par l'argument « -gargoteCapture &lt;dossier&gt; ».
    /// </summary>
    public sealed class CaptureDirector : MonoBehaviour
    {
        private const string Arg = "-gargoteCapture";
        private string _folder;
        private GameRoot _root;
        private readonly StringBuilder _report = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string folder = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == Arg) folder = args[i + 1];
#if UNITY_ANDROID && !UNITY_EDITOR
            // adb shell am start -n <package>/<activity> --ez gargoteCapture true → <persistentDataPath>/capture
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                if (intent.Call<bool>("getBooleanExtra", "gargoteCapture", false))
                    folder = Path.Combine(Application.persistentDataPath, "capture");
#endif
            if (folder == null) return;
            GameRoot.SaveServiceOverride = () => new InMemorySaveService();
            GameRoot.SkipOfflinePanel = true;
            Application.runInBackground = true; // la capture continue si la fenêtre perd le focus
            var go = new GameObject("CaptureDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CaptureDirector>()._folder = folder;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_folder);
            while ((_root = FindAnyObjectByType<GameRoot>()) == null || !_root.Ready) yield return null;
            var businesses = _root.Session.Businesses;
            _report.AppendLine("Gargote Tycoon — M2 capture (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            _report.AppendLine("Écran " + Screen.width + "x" + Screen.height + ", " + SystemInfo.deviceModel + ", " +
                               SystemInfo.graphicsDeviceName + ", RAM " + SystemInfo.systemMemorySize + " Mo, " + SystemInfo.processorType);
            _report.AppendLine("Qualité détectée : " + _root.Quality.Tier);

            _root.GameTime.ForcedHour = 9.5d;
            yield return Wait(2.5f);
            yield return Shot("01_depart");

            // Tap : cycle, barre de progression, billets en arc, popup.
            for (int k = 0; k < 6; k++)
            {
                Tap(0);
                yield return Wait(k == 3 ? 0.25f : 0.4f);
                if (k == 3) yield return Shot("02_tap_billets");
            }

            Give(1e4);
            businesses.BuyLevels(0, 10 - businesses[0].Level);
            businesses.BuyLevels(1, 1);
            yield return Wait(1.2f);
            businesses.HireManager(0);
            yield return Wait(0.45f);
            yield return Shot("03_gerant_arrive");
            yield return Wait(2f);

            Give(1e6);
            businesses.BuyLevels(0, 25 - businesses[0].Level);
            yield return Wait(0.3f);
            yield return Shot("04_palier_vitesse_x2");
            yield return Wait(1.5f);

            Give(1e6);
            businesses.BuyLevels(2, 1);
            yield return Wait(0.35f);
            yield return Shot("05_deblocage_rideau");
            yield return Wait(2f);

            // Rue développée : 8 commerces à des stades différents, gérants, foule.
            Give(1e18);
            long[] levels = { 420, 320, 210, 130, 70, 30, 12, 0 };
            for (int i = 0; i < businesses.Count; i++)
            {
                if (levels[i] > businesses[i].Level) businesses.BuyLevels(i, levels[i] - businesses[i].Level);
                if (i < 6) businesses.HireManager(i);
            }
            Focus(0);
            yield return Wait(7f);
            yield return Shot("06_rue_matin_bas");
            Focus(4);
            yield return Wait(5f);
            yield return Shot("07_rue_matin_haut");

            Focus(2);
            _root.Specials.ForceSpawn(3);
            yield return Wait(3f);
            yield return Shot("08_client_special");

            _root.GameTime.ForcedHour = 21.5d;
            yield return Wait(5f);
            yield return Shot("09_nuit");

            _root.Sheet.SetExpanded(true);
            _root.Sheet.Select(2, true);
            yield return Wait(1f);
            yield return Shot("10_panneau_achat");
            _root.Sheet.SetExpanded(false);
            yield return Wait(0.8f);

            yield return Measure(QualityTier.Low, 1f, "11_perf_bas");
            yield return Measure(QualityTier.Medium, 1f, "12_perf_moyen");
            yield return Measure(QualityTier.High, 1f, "12_perf_haut");
            yield return Measure(QualityTier.High, 100f, "13_perf_haut_x100");
            _root.GameTime.Speed = 1f;

            _root.ShowOffline(8 * 3600d);
            yield return Wait(1.6f);
            yield return Shot("14_retour_hors_ligne");

            File.WriteAllText(Path.Combine(_folder, "perf.txt"), _report.ToString());
            Debug.Log("[M2] Capture terminée : " + _folder);
            Application.Quit();
        }

        private void Give(double amount) => _root.Session.Economy.Earn(BigDouble.FromDouble(amount));

        private void Focus(int business) => _root.Rig.FocusOn(_root.Presenter.Views[business].transform.position.z);

        private void Tap(int business)
        {
            Vector3 screen = _root.mainCamera.WorldToScreenPoint(_root.Presenter.Views[business].PickCenter);
            _root.Presenter.HandleTap(screen);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), texture.EncodeToPNG());
            Destroy(texture);
        }

        private IEnumerator Measure(QualityTier tier, float speed, string shot)
        {
            _root.ApplyQuality(tier);
            _root.GameTime.Speed = speed;
            _root.GameTime.ForcedHour = 21.5d;
            Focus(3);
            yield return Wait(4f);

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            using (var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            using (var gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count"))
            using (var used = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"))
            {
                var frames = new List<float>(2048);
                long maxBatches = 0, maxSetPass = 0, maxTriangles = 0, maxGc = 0, framesWithGc = 0, maxCustomers = 0;
                float end = Time.realtimeSinceStartup + 15f;
                yield return null;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime);
                    maxBatches = Math.Max(maxBatches, batches.LastValue);
                    maxSetPass = Math.Max(maxSetPass, setPass.LastValue);
                    maxTriangles = Math.Max(maxTriangles, triangles.LastValue);
                    maxGc = Math.Max(maxGc, gcAlloc.LastValue);
                    if (gcCount.LastValue > 0) framesWithGc++;
                    maxCustomers = Math.Max(maxCustomers, _root.Presenter.Crowd.ActiveCount);
                }
                frames.Sort();
                float sum = 0f;
                foreach (float f in frames) sum += f;
                float avg = sum / Mathf.Max(1, frames.Count);
                float p95 = frames.Count > 0 ? frames[(int)(frames.Count * 0.95f)] : 0f;
                float worst = frames.Count > 0 ? frames[frames.Count - 1] : 0f;

                _report.AppendLine();
                _report.AppendLine("[" + tier + " x" + speed + "] " + frames.Count + " frames / 15 s, écran " + Screen.currentResolution.refreshRateRatio.value.ToString("0") + " Hz, nuit, 8 commerces, " +
                                   "gérants 6/8, clients max " + maxCustomers + ", plafond qualité " + _root.Quality.Settings.maxCustomers);
                _report.AppendLine("  FPS moyen " + (1f / avg).ToString("0.0") + " | frame p95 " + (p95 * 1000f).ToString("0.0") +
                                   " ms | pire " + (worst * 1000f).ToString("0.0") + " ms (cible 60 FPS, mini 30)");
                _report.AppendLine("  Batches max " + maxBatches + " (cible < 80) | SetPass max " + maxSetPass +
                                   " | triangles max " + maxTriangles + " (cible < 100K)");
                _report.AppendLine("  GC : frames avec allocation " + framesWithGc + "/" + frames.Count + ", max " + maxGc + " o/frame (cible 0)");
                _report.AppendLine("  Mémoire système utilisée " + (used.LastValue / (1024 * 1024)) + " Mo (cible < 400 Mo sur téléphone)");

                // Répartition des batches : on coupe successivement l'UI puis les personnages (quelques frames chacun).
                long all = batches.LastValue;
                var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in canvases) c.enabled = false;
                for (int f = 0; f < 5; f++) yield return null;
                long noUi = batches.LastValue;
                _root.Persons.Enabled = false;
                for (int f = 0; f < 5; f++) yield return null;
                long noPersons = batches.LastValue;
                _root.Persons.Enabled = true;
                foreach (var c in canvases) c.enabled = true;
                _report.AppendLine("  Répartition batches : UI " + (all - noUi) + ", personnages " + (noUi - noPersons) +
                                   " (" + _root.Persons.DrawCalls + " variantes), monde " + noPersons);

                // Marge : FPS sans vsync ni plafond (la frame plafonnée à 60 ne dit rien du coût réel).
                int vSync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 1000;
                for (int f = 0; f < 10; f++) yield return null;
                int uncappedFrames = 0;
                float uncappedEnd = Time.realtimeSinceStartup + 5f, uncappedStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup < uncappedEnd) { uncappedFrames++; yield return null; }
                float fps = uncappedFrames / Mathf.Max(0.001f, Time.realtimeSinceStartup - uncappedStart);
                QualitySettings.vSyncCount = vSync;
                Application.targetFrameRate = target;
                _report.AppendLine("  Sans plafond : " + fps.ToString("0") + " FPS (" + (1000f / Mathf.Max(1f, fps)).ToString("0.00") + " ms/frame)");
            }
            yield return Shot(shot);
        }
    }
}
#endif
