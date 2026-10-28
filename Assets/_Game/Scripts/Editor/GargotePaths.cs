using System.IO;
using UnityEngine;

namespace Gargote.EditorTools
{
    internal static class GargotePaths
    {
        public const string DataRoot = "Assets/_Game/Data";
        public const string SeedJson = DataRoot + "/Seed/economy_seed.json";
        public const string GameDatabase = DataRoot + "/GameDatabase.asset";
        public const string EconomyConfig = DataRoot + "/EconomyConfig.asset";
        public const string PrestigeConfig = DataRoot + "/PrestigeConfig.asset";
        public const string SimulationConfig = DataRoot + "/Simulation/SimulationConfig.asset";

        public static string CityFolder(string displayName) => DataRoot + "/Cities/" + displayName;

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string SimulationOutput => Path.Combine(ProjectRoot, "Tools", "simulation", "output");
    }
}
