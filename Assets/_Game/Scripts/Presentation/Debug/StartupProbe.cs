#if UNITY_EDITOR || DEVELOPMENT_BUILD || GARGOTE_PROBE
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Gargote.Presentation
{
    /// <summary>
    /// Mesures de démarrage et de mémoire (builds de développement, ou release avec le define GARGOTE_PROBE).
    /// Démarrage à froid = début du processus (Android : Process.getStartUptimeMillis) → première frame rendue avec la
    /// rue jouable (GameRoot prêt). Résultat dans le log (« [GargoteStartup] ») et dans &lt;persistentDataPath&gt;/startup.txt.
    /// Avec l'extra « gargoteMemSnap » (Android) ou l'argument « -gargoteMemSnap » : snapshot Memory Profiler
    /// (&lt;persistentDataPath&gt;/memory.snap) 10 s après le démarrage, à ouvrir dans le package Memory Profiler.
    /// </summary>
    public sealed class StartupProbe : MonoBehaviour
    {
        /// <summary>Appelé par GameRoot à la fin de Awake (rue construite) : la mesure est prise à la première frame rendue.</summary>
        public static void Attach(GameObject host)
        {
            if (host.GetComponent<StartupProbe>() == null) host.AddComponent<StartupProbe>();
        }

        private static readonly StringBuilder Marks = new StringBuilder(128);

        /// <summary>Jalon de démarrage (ms depuis le lancement de Unity), ajouté au rapport.</summary>
        public static void Mark(string label)
        {
            Marks.Append(' ').Append(label).Append("_ms=").Append((Time.realtimeSinceStartupAsDouble * 1000d).ToString("0", CultureInfo.InvariantCulture));
        }

        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame(); // première frame de la rue jouable effectivement rendue
            double sinceUnity = Time.realtimeSinceStartupAsDouble * 1000d;
            double coldStart = sinceUnity;
            bool snapshot = Array.IndexOf(Environment.GetCommandLineArgs(), "-gargoteMemSnap") >= 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var process = new AndroidJavaClass("android.os.Process"))
                using (var clock = new AndroidJavaClass("android.os.SystemClock"))
                    coldStart = clock.CallStatic<long>("uptimeMillis") - process.CallStatic<long>("getStartUptimeMillis");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    snapshot |= intent.Call<bool>("getBooleanExtra", "gargoteMemSnap", false);
            }
            catch (Exception e)
            {
                Write("process_start_unavailable " + e.Message);
            }
#endif
            var report = new StringBuilder();
            report.Append("cold_start_ms=").Append(coldStart.ToString("0", CultureInfo.InvariantCulture));
            report.Append(" since_unity_ms=").Append(sinceUnity.ToString("0", CultureInfo.InvariantCulture));
            report.Append(Marks);
            AppendMemory(report, "boot");
            Write(report.ToString());

            yield return new WaitForSecondsRealtime(10f);
            report.Clear().Append("after_10s");
            AppendMemory(report, "steady");
            Write(report.ToString());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!snapshot) yield break;
            string path = Path.Combine(Application.persistentDataPath, "memory.snap");
            Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(path, (file, ok) => Write("memory_snapshot " + (ok ? "ok " : "failed ") + file),
                Unity.Profiling.Memory.CaptureFlags.ManagedObjects | Unity.Profiling.Memory.CaptureFlags.NativeObjects |
                Unity.Profiling.Memory.CaptureFlags.NativeAllocations | Unity.Profiling.Memory.CaptureFlags.NativeAllocationSites);
#else
            if (snapshot) Write("memory_snapshot unavailable (release)");
#endif
        }

        private static void AppendMemory(StringBuilder sb, string label)
        {
            const double mb = 1024d * 1024d;
            sb.Append(' ').Append(label).Append("_allocated_mb=").Append((Profiler.GetTotalAllocatedMemoryLong() / mb).ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(' ').Append(label).Append("_reserved_mb=").Append((Profiler.GetTotalReservedMemoryLong() / mb).ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(' ').Append(label).Append("_mono_mb=").Append((Profiler.GetMonoUsedSizeLong() / mb).ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(' ').Append(label).Append("_gfx_mb=").Append((Profiler.GetAllocatedMemoryForGraphicsDriver() / mb).ToString("0.0", CultureInfo.InvariantCulture));
        }

        private static void Write(string line)
        {
            Debug.Log("[GargoteStartup] " + line);
            try
            {
                File.AppendAllText(Path.Combine(Application.persistentDataPath, "startup.txt"), line + "\n");
            }
            catch (Exception) { }
        }
    }
}
#endif
