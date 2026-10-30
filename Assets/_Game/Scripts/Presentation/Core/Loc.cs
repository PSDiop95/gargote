using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Gargote.Presentation
{
    /// <summary>
    /// Accès aux textes de la table « Gargote » (Unity Localization). Les chaînes sont mises en cache
    /// à la première lecture : aucune allocation en jeu après le chargement.
    /// </summary>
    public static class Loc
    {
        public const string TableName = "Gargote";
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(256);
        private static StringTable _table;

        public static bool IsReady => _table != null;

        /// <summary>Initialisation synchrone (hors WebGL). Sans table, <see cref="Get"/> renvoie la clé.</summary>
        public static void Initialize()
        {
            Cache.Clear();
            _table = null;
            try
            {
                LocalizationSettings.InitializationOperation.WaitForCompletion();
                _table = LocalizationSettings.StringDatabase.GetTable(TableName);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Loc] Localization unavailable, keys will be shown: " + e.Message);
            }
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            string value = key;
            if (_table != null)
            {
                var entry = _table.GetEntry(key);
                if (entry != null) value = entry.GetLocalizedString();
            }
            Cache[key] = value;
            return value;
        }

        /// <summary>Compte les lignes « prefix.1 », « prefix.2 »… présentes dans la table.</summary>
        public static int CountLines(string prefix, int max = 12)
        {
            int n = 0;
            for (int i = 1; i <= max; i++)
            {
                string key = prefix + "." + i;
                if (Get(key) == key) break;
                n++;
            }
            return n;
        }
    }
}
