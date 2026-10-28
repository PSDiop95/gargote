using System;
using System.Text;
using Gargote.Data;
using Gargote.Simulation;
using UnityEditor;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Lance la simulation 30 jours depuis les ScriptableObjects et écrit les CSV dans Tools/simulation/output.
    /// Batchmode : -executeMethod Gargote.EditorTools.SimulationRunner.RunBatch (code de sortie 1 si une cible échoue).
    /// </summary>
    public static class SimulationRunner
    {
        [MenuItem("Gargote/Simulation/Run economy simulation (30 days)")]
        public static void RunFromMenu()
        {
            var report = Run(out string log);
            Debug.Log(log);
            EditorUtility.RevealInFinder(GargotePaths.SimulationOutput);
            if (!report.AllTargetsPass) Debug.LogWarning("[Gargote] Some progression targets fail — see simulation_summary.csv");
        }

        public static void RunBatch()
        {
            int code = 1;
            try
            {
                var report = Run(out string log);
                Console.WriteLine(log);
                code = report.AllTargetsPass ? 0 : 1;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                code = 2;
            }
            EditorApplication.Exit(code);
        }

        public static SimulationReport Run(out string log)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GargotePaths.GameDatabase);
            var config = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(GargotePaths.SimulationConfig);
            if (database == null || config == null)
                throw new InvalidOperationException("Missing GameDatabase or SimulationConfig — run Gargote > Data > Import first.");

            var definitions = EconomySimulator.WithProxyCities(database.ToDefinitions(), 1 + config.settings.proxyCities,
                config.settings.ToScaling());
            var report = new EconomySimulator(definitions, config.settings.ToSettings()).Run();
            report.WriteAll(GargotePaths.SimulationOutput);

            var sb = new StringBuilder("[Gargote] Simulation written to " + GargotePaths.SimulationOutput + "\n");
            foreach (var p in report.Profiles)
            {
                sb.AppendLine("== " + p.Profile.Name);
                foreach (var c in p.Checks)
                    sb.AppendLine("   " + (c.Pass ? "OK   " : "FAIL ") + c.Metric + " = " + c.Value + " (target " + c.Target + ")");
            }
            foreach (var g in report.AdGaps)
                sb.AppendLine("ad gap " + g.Pair + ": city2 x" + g.City2Ratio?.ToString("0.00") + ", prestiges x" + g.PrestigeRatio?.ToString("0.00"));
            log = sb.ToString();
            return report;
        }
    }
}
