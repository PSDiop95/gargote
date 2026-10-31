using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Gargote.Simulation;

namespace Gargote.Tools.Sim
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string root = args.Length > 0 ? args[0] : FindProjectRoot();
            string outDir = args.Length > 1 ? args[1] : Path.Combine(root, "Tools", "simulation", "output");
            string seedPath = args.Length > 2 ? args[2] : Path.Combine(root, "Assets", "_Game", "Data", "Seed", "economy_seed.json");

            var seed = JsonSerializer.Deserialize<GameDataSeed>(File.ReadAllText(seedPath),
                new JsonSerializerOptions { IncludeFields = true });
            var definitions = EconomySimulator.WithProxyCities(seed.ToDefinitions(), 1 + seed.simulation.proxyCities,
                seed.simulation.ToScaling());
            var settings = seed.simulation.ToSettings();
            // Méta M3 : lue à côté du seed économique (absente = économie M1 seule, ou « --no-meta »).
            string metaPath = Path.Combine(Path.GetDirectoryName(seedPath), "meta_seed.json");
            Gargote.Core.MetaDefinitions meta = null;
            if (File.Exists(metaPath) && Array.IndexOf(args, "--no-meta") < 0)
                meta = JsonSerializer.Deserialize<MetaSeed>(File.ReadAllText(metaPath), new JsonSerializerOptions { IncludeFields = true })
                    .ToDefinitions(definitions);

            var watch = Stopwatch.StartNew();
            var report = new EconomySimulator(definitions, settings, meta).Run();
            report.WriteAll(outDir);

            foreach (var p in report.Profiles)
            {
                Console.WriteLine($"== {p.Profile.Name}  (mur max {p.LongestWallMinutes:0.#} min après {p.LongestWallAfter} @ {p.LongestWallAtActiveMinutes:0.#} min actives)");
                foreach (var c in p.Checks)
                    Console.WriteLine($"   {(c.Pass ? "OK  " : "FAIL")} {c.Metric,-32} {c.Value,-12} cible {c.Target}");
            }
            foreach (var g in report.AdGaps)
                Console.WriteLine($"   gap {g.Pair,-12} ville2 x{g.City2Ratio:0.00}  prestiges x{g.PrestigeRatio:0.00}");
            Console.WriteLine($"Simulation: {watch.Elapsed.TotalSeconds:0.0}s -> {outDir}");
            return report.AllTargetsPass ? 0 : 1;
        }

        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
