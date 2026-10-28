using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Gargote.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gargote.Services.Save
{
    /// <summary>
    /// Sérialisation JSON versionnée + migrations + enveloppe avec empreinte anti-édition.
    /// Format fichier : { "version": n, "payload": "&lt;json&gt;", "checksum": "&lt;sha256 hex&gt;" }.
    /// L'empreinte décourage l'édition à la main ; la vraie validation serveur arrive avec Cloud Code (V1.1).
    /// </summary>
    public static class SaveSerializer
    {
        // Sel local : n'est pas un secret cryptographique, juste un frein à l'édition triviale.
        private const string ChecksumSalt = "gargote-tycoon/teranga/v1";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            FloatFormatHandling = FloatFormatHandling.String,
        };

        /// <summary>Migrations de la version n vers n+1, appliquées dans l'ordre sur le JSON brut.</summary>
        private static readonly Dictionary<int, Func<JObject, JObject>> Migrations = new Dictionary<int, Func<JObject, JObject>>
        {
            // Exemple pour la V2 : { 1, o => { o["NewField"] = 0; return o; } },
        };

        public static string Serialize(SaveData data)
        {
            data.Version = SaveData.CurrentVersion;
            return JsonConvert.SerializeObject(data, Settings);
        }

        public static SaveData Deserialize(string json) => Deserialize(json, Migrations);

        internal static SaveData Deserialize(string json, IReadOnlyDictionary<int, Func<JObject, JObject>> migrations)
        {
            var obj = JObject.Parse(json);
            int version = obj.Value<int?>(nameof(SaveData.Version)) ?? 0;
            if (version > SaveData.CurrentVersion)
                throw new InvalidOperationException("Save version " + version + " is newer than supported " + SaveData.CurrentVersion);

            while (version < SaveData.CurrentVersion)
            {
                if (!migrations.TryGetValue(version, out var migrate))
                    throw new InvalidOperationException("No save migration from version " + version);
                obj = migrate(obj);
                version++;
                obj[nameof(SaveData.Version)] = version;
            }
            return obj.ToObject<SaveData>(JsonSerializer.Create(Settings));
        }

        public static string Wrap(SaveData data)
        {
            string payload = Serialize(data);
            var envelope = new JObject
            {
                ["version"] = SaveData.CurrentVersion,
                ["payload"] = payload,
                ["checksum"] = Checksum(payload),
            };
            return envelope.ToString(Formatting.None);
        }

        /// <summary>Faux si l'enveloppe est illisible ou si l'empreinte ne correspond pas.</summary>
        public static bool TryUnwrap(string text, out SaveData data)
        {
            data = null;
            try
            {
                var envelope = JObject.Parse(text);
                string payload = envelope.Value<string>("payload");
                string checksum = envelope.Value<string>("checksum");
                if (payload == null || checksum == null) return false;
                if (!string.Equals(Checksum(payload), checksum, StringComparison.OrdinalIgnoreCase)) return false;
                data = Deserialize(payload);
                return data != null;
            }
            catch (Exception e) when (e is JsonException || e is InvalidOperationException || e is FormatException)
            {
                return false;
            }
        }

        public static string Checksum(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ChecksumSalt + payload));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
