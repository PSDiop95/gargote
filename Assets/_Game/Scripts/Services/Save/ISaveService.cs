using Gargote.Systems;

namespace Gargote.Services.Save
{
    public enum SaveLoadStatus
    {
        NoSave = 0,
        Loaded = 1,
        /// <summary>Fichier principal corrompu ou modifié : la sauvegarde de secours a été utilisée.</summary>
        LoadedFromBackup = 2,
        /// <summary>Aucun fichier valide : partie neuve.</summary>
        Corrupted = 3,
    }

    public interface ISaveService
    {
        SaveLoadStatus TryLoad(out SaveData data);
        void Save(SaveData data);
        void Delete();
    }

    /// <summary>Mock en mémoire pour les tests et l'éditeur.</summary>
    public sealed class InMemorySaveService : ISaveService
    {
        private string _json;

        public int SaveCount { get; private set; }

        public SaveLoadStatus TryLoad(out SaveData data)
        {
            data = null;
            if (_json == null) return SaveLoadStatus.NoSave;
            data = SaveSerializer.Deserialize(_json);
            return SaveLoadStatus.Loaded;
        }

        public void Save(SaveData data)
        {
            _json = SaveSerializer.Serialize(data);
            SaveCount++;
        }

        public void Delete() => _json = null;
    }
}
