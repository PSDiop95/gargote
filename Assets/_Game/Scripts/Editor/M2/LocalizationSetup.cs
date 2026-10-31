using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Table « Gargote » (FR, EN) des textes M2. Contenu placeholder : chaque réplique est listée dans
    /// docs/CULTURE_REVIEW.md pour relecture humaine avant toute version publique.
    /// </summary>
    internal static class LocalizationSetup
    {
        public const string Folder = "Assets/_Game/Localization";

        /// <summary>clé → (FR, EN).</summary>
        public static readonly (string Key, string Fr, string En)[] Entries =
        {
            ("business.forokh_thiaya", "Forokh Thiaya", "Forokh Thiaya"),
            ("business.tangana", "Tangana / Ndéki", "Tangana / Ndeki"),
            ("business.maiga", "Maïga", "Maiga"),
            ("business.gargote", "Gargote", "Gargote"),
            ("business.dibiterie", "Dibiterie", "Dibiterie"),
            ("business.fataya_nems", "Plateau Salé", "Savoury Platter"),
            ("business.restaurant_teranga", "Restaurant Teranga", "Teranga Restaurant"),
            ("business.traiteur_evenements", "Kiki Traiteur", "Kiki Catering"),

            ("manager.forokh_thiaya.1", "Pain chaud, viande chaude, client content !", "Hot bread, hot meat, happy customer!"),
            ("manager.forokh_thiaya.2", "Ma brouette, c'est mon bureau.", "My cart is my office."),
            ("manager.forokh_thiaya.3", "Jërëjëf, à demain !", "Jërëjëf, see you tomorrow!"),
            ("manager.tangana.1", "Le café Touba est prêt !", "The Touba coffee is ready!"),
            ("manager.tangana.2", "Omelette ou niébé ? Les deux, bien sûr.", "Omelette or beans? Both, of course."),
            ("manager.tangana.3", "Le matin, ici, c'est la meilleure adresse.", "Mornings, this is the place to be."),
            ("manager.maiga.1", "La nuit est jeune, les brochettes aussi !", "The night is young, so are the skewers!"),
            ("manager.maiga.2", "Sous l'ampoule, tout est plus bon.", "Under the bulb, everything tastes better."),
            ("manager.maiga.3", "Une petite faim ? On a ce qu'il faut.", "A little hungry? We've got you."),
            ("manager.gargote.1", "Le thiéb' du jour sort de la marmite !", "Today's thieb is out of the pot!"),
            ("manager.gargote.2", "Assieds-toi, mange, c'est la maison.", "Sit down, eat, you're home."),
            ("manager.gargote.3", "Le secret ? La patience… et le riz cassé.", "The secret? Patience… and broken rice."),
            ("manager.dibiterie.1", "Le dibi est grillé à point.", "The dibi is grilled just right."),
            ("manager.dibiterie.2", "Oignons, moutarde, et le tour est joué !", "Onions, mustard, and you're set!"),
            ("manager.dibiterie.3", "Le charbon chante, c'est bon signe.", "The charcoal is singing, good sign."),
            ("manager.fataya_nems.1", "Fatayas tout chauds, attention les doigts !", "Hot fatayas, mind your fingers!"),
            ("manager.fataya_nems.2", "Pastels, nems, fatayas : on a tout.", "Pastels, nems, fatayas: we have it all."),
            ("manager.fataya_nems.3", "Un pour toi, un pour la route.", "One for you, one for the road."),
            ("manager.restaurant_teranga.1", "Bienvenue chez nous, la Teranga d'abord !", "Welcome, hospitality comes first!"),
            ("manager.restaurant_teranga.2", "Table pour combien ?", "Table for how many?"),
            ("manager.restaurant_teranga.3", "Le dessert est offert… aux habitués.", "Dessert is on us… for regulars."),
            ("manager.traiteur_evenements.1", "Trois cents invités ? Pas de souci.", "Three hundred guests? No problem."),
            ("manager.traiteur_evenements.2", "Les marmites sont en route !", "The pots are on their way!"),
            ("manager.traiteur_evenements.3", "Une fête réussie, ça se mange.", "A good party is one you can eat."),

            ("special.official", "Fonctionnaire pressé", "Hurried Official"),
            ("special.tourist", "Touriste perdu", "Lost Tourist"),
            ("special.wrestler", "Lutteur affamé", "Hungry Wrestler"),
            ("special.wedding", "Délégation de mariage", "Wedding Delegation"),

            ("hud.per_second", "/s", "/s"),
            ("hud.goal", "Objectif Dakar :", "Dakar goal:"),
            ("hud.welcome", "Bienvenue x3", "Welcome x3"),
            ("hud.ad_boost", "Boost", "Boost"),
            ("sheet.level", "Niv.", "Lv."),
            ("sheet.open", "Ouvrir", "Open"),
            ("sheet.manager", "Gérant", "Manager"),
            ("sheet.max", "MAX", "MAX"),
            ("sheet.next_milestone", "Palier", "Next"),
            ("sheet.buy_best", "Acheter le meilleur", "Buy the best"),
            ("badge.morning", "x2 MATIN", "x2 MORNING"),
            ("badge.night", "x2 NUIT", "x2 NIGHT"),
            ("tab.street", "Rue", "Street"),
            ("tab.managers", "Gérants", "Managers"),
            ("tab.upgrades", "Upgrades", "Upgrades"),
            ("tab.cities", "Villes", "Cities"),
            ("tab.shop", "Boutique", "Shop"),
            ("tab.coming_soon", "Bientôt disponible", "Coming soon"),
            ("banner.unlock", "OUVERT !", "OPEN!"),
            ("banner.speed_x2", "VITESSE x2 !", "SPEED x2!"),
            ("banner.revenue_x2", "REVENUS x2 !", "PROFIT x2!"),
            ("banner.global", "TOUTE LA RUE x1,25 !", "WHOLE STREET x1.25!"),
            ("offline.title", "Pendant ton absence…", "While you were away…"),
            ("offline.away", "Absent", "Away"),
            ("offline.claim", "Récupérer", "Collect"),
            ("offline.double", "x2 (pub)", "x2 (ad)"),
        };

        /// <summary>Français (référence), anglais, wolof (répliques ; sinon repli sur le français).</summary>
        public static readonly string[] Codes = { "fr", "en", "wo" };

        public static void Setup()
        {
            Directory.CreateDirectory(Folder + "/Locales");
            Directory.CreateDirectory(Folder + "/Tables");
            AddressableAssetSettingsDefaultObject.GetSettings(true);

            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(Folder + "/LocalizationSettings.asset");
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    AssetDatabase.CreateAsset(settings, Folder + "/LocalizationSettings.asset");
                }
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            var locales = EnsureLocales();

            // Repli : français (langue de référence du jeu) si la langue système n'est pas supportée.
            foreach (var selector in settings.GetStartupLocaleSelectors())
                if (selector is SpecificLocaleSelector specific) specific.LocaleId = new LocaleIdentifier("fr");
            EditorUtility.SetDirty(settings);

            var collection = LocalizationEditorSettings.GetStringTableCollection("Gargote") ??
                             LocalizationEditorSettings.CreateStringTableCollection("Gargote", Folder + "/Tables", locales);
            EditorUtility.SetDirty(collection);
            var all = new List<(string, string, string, string)>(Entries.Length);
            foreach (var e in Entries) all.Add((e.Key, e.Fr, e.En, null));
            Write(all);
        }

        private static List<Locale> EnsureLocales()
        {
            var locales = new List<Locale>();
            foreach (var code in Codes)
            {
                var id = new LocaleIdentifier(code);
                var locale = LocalizationEditorSettings.GetLocale(id);
                if (locale == null)
                {
                    locale = Locale.CreateLocale(id);
                    AssetDatabase.CreateAsset(locale, Folder + "/Locales/" + code + ".asset");
                    LocalizationEditorSettings.AddLocale(locale, false);
                }
                locales.Add(locale);
            }
            return locales;
        }

        /// <summary>Écrit (ou met à jour) des entrées clé → (FR, EN, WO). WO vide : repli sur le français.</summary>
        public static void Write(IReadOnlyList<(string Key, string Fr, string En, string Wo)> entries)
        {
            EnsureLocales();
            var collection = LocalizationEditorSettings.GetStringTableCollection("Gargote");
            if (collection == null) { Setup(); collection = LocalizationEditorSettings.GetStringTableCollection("Gargote"); }
            foreach (var code in Codes)
            {
                var id = new LocaleIdentifier(code);
                var table = collection.GetTable(id) as StringTable;
                if (table == null) table = collection.AddNewTable(id) as StringTable;
                foreach (var e in entries)
                {
                    string value = code == "fr" ? e.Fr : code == "en" ? e.En : string.IsNullOrEmpty(e.Wo) ? e.Fr : e.Wo;
                    var entry = table.GetEntry(e.Key);
                    if (entry == null) table.AddEntry(e.Key, value);
                    else entry.Value = value;
                }
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
        }
    }
}
