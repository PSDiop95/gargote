using System.IO;
using Gargote.Data;
using Gargote.Simulation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Importe economy_seed.json dans les ScriptableObjects (création ou mise à jour, idempotent).
    /// Après l'import, les assets sont la source de vérité ; le JSON sert de point de départ versionné.
    /// Batchmode : -executeMethod Gargote.EditorTools.DataSeeder.SeedFromJson
    /// </summary>
    public static class DataSeeder
    {
        [MenuItem("Gargote/Data/Import economy_seed.json into ScriptableObjects")]
        public static void SeedFromJson()
        {
            var seed = JsonConvert.DeserializeObject<GameDataSeed>(File.ReadAllText(GargotePaths.SeedJson));
            seed.ToDefinitions(); // valide les données avant d'écrire quoi que ce soit

            var economy = LoadOrCreate<EconomyConfigAsset>(GargotePaths.EconomyConfig);
            var e = seed.economy;
            economy.milestones = e.milestones;
            economy.milestoneSpeedMultiplier = e.milestoneSpeedMultiplier;
            economy.minCycleSeconds = e.minCycleSeconds;
            economy.globalMilestones = e.globalMilestones ?? new long[0];
            economy.globalMilestoneMultiplier = e.globalMilestoneMultiplier;
            economy.rewardedFullValuePerDay = e.rewardedFullValuePerDay;
            economy.rewardedDecayPerExtraAd = e.rewardedDecayPerExtraAd;
            economy.dayLengthSeconds = e.dayLengthSeconds;
            economy.morningStartHour = e.morningStartHour;
            economy.morningEndHour = e.morningEndHour;
            economy.nightStartHour = e.nightStartHour;
            economy.nightEndHour = e.nightEndHour;
            economy.affinityMultiplier = e.affinityMultiplier;
            economy.offlineBaseCapSeconds = e.offlineBaseCapSeconds;
            economy.offlineMaxCapSeconds = e.offlineMaxCapSeconds;
            economy.offlineEfficiency = e.offlineEfficiency;
            economy.offlineAdMultiplier = e.offlineAdMultiplier;
            economy.adBoostMultiplier = e.adBoostMultiplier;
            economy.adBoostDurationSeconds = e.adBoostDurationSeconds;
            economy.adBoostMaxStackSeconds = e.adBoostMaxStackSeconds;
            economy.adBoostAppliesOffline = e.adBoostAppliesOffline;
            EditorUtility.SetDirty(economy);

            var prestige = LoadOrCreate<PrestigeConfigAsset>(GargotePaths.PrestigeConfig);
            prestige.k = seed.prestige.k;
            prestige.threshold = seed.prestige.threshold;
            prestige.bonusPerStar = seed.prestige.bonusPerStar;
            prestige.minStarsToPrestige = seed.prestige.minStarsToPrestige;
            prestige.softCapStars = seed.prestige.softCapStars;
            prestige.softCapExponent = seed.prestige.softCapExponent > 0 ? seed.prestige.softCapExponent : 0.5;
            prestige.cityCarryOver = seed.prestige.cityCarryOver;
            EditorUtility.SetDirty(prestige);

            var cities = new CityDefinitionAsset[seed.cities.Length];
            for (int c = 0; c < seed.cities.Length; c++)
            {
                var citySeed = seed.cities[c];
                string folder = GargotePaths.CityFolder(citySeed.displayName);
                var city = LoadOrCreate<CityDefinitionAsset>(folder + "/" + citySeed.displayName + ".asset");
                city.id = citySeed.id;
                city.displayName = citySeed.displayName;
                city.goalLifetimeEarnings = citySeed.goalLifetimeEarnings;
                city.startingMoney = citySeed.startingMoney;
                city.starThreshold = citySeed.starThreshold;
                city.businesses = new BusinessDefinitionAsset[citySeed.businesses.Length];
                for (int b = 0; b < citySeed.businesses.Length; b++)
                {
                    var bs = citySeed.businesses[b];
                    string path = folder + "/Businesses/" + (b + 1).ToString("00") + "_" + bs.id + ".asset";
                    var business = LoadOrCreate<BusinessDefinitionAsset>(path);
                    business.id = bs.id;
                    business.displayName = bs.displayName;
                    business.baseCost = bs.baseCost;
                    business.growth = bs.growth;
                    business.baseRevenue = bs.baseRevenue;
                    business.baseCycleSeconds = bs.baseCycleSeconds;
                    business.managerCost = bs.managerCost;
                    business.affinity = BusinessSeed.ParseAffinity(bs.affinity);
                    EditorUtility.SetDirty(business);
                    city.businesses[b] = business;
                }
                EditorUtility.SetDirty(city);
                cities[c] = city;
            }

            var database = LoadOrCreate<GameDatabase>(GargotePaths.GameDatabase);
            database.cities = cities;
            database.economy = economy;
            database.prestige = prestige;
            EditorUtility.SetDirty(database);

            var simulation = LoadOrCreate<SimulationConfigAsset>(GargotePaths.SimulationConfig);
            simulation.settings = seed.simulation;
            EditorUtility.SetDirty(simulation);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            database.ToDefinitions(); // contrôle final : les assets produisent des définitions valides
            Debug.Log("[Gargote] Seed imported: " + cities.Length + " city(ies).");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir)) CreateFolders(dir);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateFolders(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
