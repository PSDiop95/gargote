using System.Collections.Generic;
using System.IO;
using Gargote.Core;
using Gargote.Data;
using Gargote.Simulation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Importe meta_seed.json (M3) dans les ScriptableObjects (gérants, coffres, recettes, MetaConfig) et écrit
    /// les textes FR / EN / WO dans la table « Gargote ». Idempotent. Les ids sont stables : ne jamais les renommer
    /// après publication (clés de sauvegarde).
    /// Batchmode : -executeMethod Gargote.EditorTools.MetaDataSeeder.Seed
    /// </summary>
    public static class MetaDataSeeder
    {
        [MenuItem("Gargote/Data/Import meta_seed.json into ScriptableObjects")]
        public static void Seed()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GargotePaths.GameDatabase);
            if (database == null) throw new IOException("GameDatabase missing: import economy_seed.json first.");
            var game = database.ToDefinitions();
            var seed = JsonConvert.DeserializeObject<MetaSeed>(File.ReadAllText(GargotePaths.MetaSeedJson));
            var defs = seed.ToDefinitions(game); // valide avant d'écrire quoi que ce soit

            var config = DataSeeder.LoadOrCreate<MetaConfigAsset>(GargotePaths.MetaConfig);

            config.managers = new ManagerDefinitionAsset[defs.Managers.Length];
            for (int i = 0; i < defs.Managers.Length; i++)
            {
                var m = defs.Managers[i];
                var asset = DataSeeder.LoadOrCreate<ManagerDefinitionAsset>(
                    GargotePaths.ManagersFolder + "/" + m.CityId + "/" + m.Rarity + "_" + m.Id + ".asset");
                asset.definition = m;
                EditorUtility.SetDirty(asset);
                config.managers[i] = asset;
            }

            config.chests = new ChestDefinitionAsset[defs.Chests.Length];
            for (int i = 0; i < defs.Chests.Length; i++)
            {
                var asset = DataSeeder.LoadOrCreate<ChestDefinitionAsset>(
                    GargotePaths.ChestsFolder + "/" + (i + 1).ToString("00") + "_" + defs.Chests[i].Id + ".asset");
                asset.definition = defs.Chests[i];
                EditorUtility.SetDirty(asset);
                config.chests[i] = asset;
            }

            config.recipes = new RecipeDefinitionAsset[defs.Recipes.Length];
            for (int i = 0; i < defs.Recipes.Length; i++)
            {
                var asset = DataSeeder.LoadOrCreate<RecipeDefinitionAsset>(
                    GargotePaths.RecipesFolder + "/" + (i + 1).ToString("00") + "_" + defs.Recipes[i].Id + ".asset");
                asset.definition = defs.Recipes[i];
                asset.steps = seed.recipes[i].stepsFr?.Length ?? 0;
                EditorUtility.SetDirty(asset);
                config.recipes[i] = asset;
            }

            config.managerLevels = defs.ManagerLevels;
            config.chestRules = defs.ChestRules;
            config.missions = defs.Missions;
            config.challenges = defs.Challenges;
            config.missionRules = defs.MissionRules;
            config.login = defs.Login;
            config.wheel = defs.Wheel;
            config.playerLevels = defs.PlayerLevels;
            config.albumCityBonusMultiplier = defs.AlbumCityBonusMultiplier;
            config.achievements = defs.Achievements;
            config.notifications = defs.Notifications;
            config.cauris = defs.Cauris;
            config.minSecondsBetweenReminders = defs.MinSecondsBetweenReminders;
            EditorUtility.SetDirty(config);

            database.meta = config;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            LocalizationSetup.Setup(); // locales FR / EN / WO et textes M2
            LocalizationSetup.Write(Texts(seed));
            database.ToMetaDefinitions(game); // contrôle final : les assets produisent des définitions valides
            Debug.Log("[Gargote] Meta seed imported: " + defs.Managers.Length + " managers, " + defs.Chests.Length + " chests, " +
                      defs.Recipes.Length + " recipes, " + defs.Achievements.Length + " achievements.");
        }

        /// <summary>Toutes les chaînes M3 : interface + contenus du seed méta + noms d'upgrades et de commerces du seed économie.</summary>
        private static List<(string, string, string, string)> Texts(MetaSeed seed)
        {
            var list = new List<(string, string, string, string)>(512);
            foreach (var e in MetaStrings.Ui) list.Add((e.Key, e.Fr, e.En, null));
            foreach (var e in MetaStrings.Stats) list.Add(("stat." + e.Stat, e.Fr, e.En, null));

            foreach (var m in seed.managers)
            {
                list.Add(("manager." + m.id + ".name", m.nameFr, m.nameEn, null));
                list.Add(("manager." + m.id + ".line", m.lineFr, m.lineEn, m.lineWo));
            }
            foreach (var r in seed.recipes)
            {
                list.Add(("recipe." + r.id + ".name", r.nameFr, r.nameEn, r.nameWo));
                list.Add(("recipe." + r.id + ".anecdote", r.anecdoteFr, r.anecdoteEn, null));
                int steps = r.stepsFr?.Length ?? 0;
                for (int s = 0; s < steps; s++)
                {
                    string en = r.stepsEn != null && s < r.stepsEn.Length ? r.stepsEn[s] : r.stepsFr[s];
                    list.Add(("recipe." + r.id + ".step." + (s + 1), r.stepsFr[s], en, null));
                }
            }
            foreach (var a in seed.achievements) list.Add(("achievement." + a.id + ".name", a.nameFr, a.nameEn, null));

            // Seed économie : noms des commerces des villes suivantes, des upgrades FCFA et des upgrades d'étoiles.
            // Les commerces de Dakar ont déjà leurs noms FR / EN (M2) : on ne les écrase pas.
            var m2 = new HashSet<string>();
            foreach (var e in LocalizationSetup.Entries) m2.Add(e.Key);
            var eco = JsonConvert.DeserializeObject<GameDataSeed>(File.ReadAllText(GargotePaths.SeedJson));
            foreach (var city in eco.cities)
            {
                foreach (var b in city.businesses)
                    if (!m2.Contains("business." + b.id)) list.Add(("business." + b.id, b.displayName, b.displayName, null));
                if (city.upgrades != null)
                    foreach (var u in city.upgrades) list.Add(("upgrade." + u.id, u.displayName, u.displayName, null));
            }
            if (eco.prestige.starUpgrades != null)
                foreach (var u in eco.prestige.starUpgrades) list.Add(("star_upgrade." + u.id, u.displayName, u.displayName, null));
            return list;
        }
    }
}
