using System;
using System.IO;
using System.Text;
using Gargote.Systems;

namespace Gargote.Services.Save
{
    /// <summary>
    /// Sauvegarde fichier atomique : écriture dans un .tmp, puis remplacement du fichier principal
    /// (l'ancien devient .bak). Au chargement : principal, sinon .bak, sinon partie neuve.
    /// Le dossier est injecté par le composition root (Application.persistentDataPath en jeu).
    /// </summary>
    public sealed class FileSaveService : ISaveService
    {
        private readonly string _mainPath;
        private readonly string _tempPath;
        private readonly string _backupPath;

        public FileSaveService(string directory, string fileName = "gargote_save.json")
        {
            Directory.CreateDirectory(directory);
            _mainPath = Path.Combine(directory, fileName);
            _tempPath = _mainPath + ".tmp";
            _backupPath = _mainPath + ".bak";
        }

        public string MainPath => _mainPath;
        public string BackupPath => _backupPath;

        public SaveLoadStatus TryLoad(out SaveData data)
        {
            data = null;
            bool hasMain = File.Exists(_mainPath);
            bool hasBackup = File.Exists(_backupPath);
            if (!hasMain && !hasBackup) return SaveLoadStatus.NoSave;

            if (hasMain && TryRead(_mainPath, out data)) return SaveLoadStatus.Loaded;
            if (hasBackup && TryRead(_backupPath, out data)) return SaveLoadStatus.LoadedFromBackup;
            return SaveLoadStatus.Corrupted;
        }

        public void Save(SaveData data)
        {
            string text = SaveSerializer.Wrap(data);
            using (var stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(_mainPath))
            {
                try
                {
                    File.Replace(_tempPath, _mainPath, _backupPath, true);
                    return;
                }
                catch (PlatformNotSupportedException) { }
                catch (IOException) { }

                // Repli si File.Replace n'est pas disponible : copie de secours puis déplacement.
                File.Copy(_mainPath, _backupPath, true);
                File.Delete(_mainPath);
            }
            File.Move(_tempPath, _mainPath);
        }

        public void Delete()
        {
            foreach (var path in new[] { _mainPath, _tempPath, _backupPath })
                if (File.Exists(path)) File.Delete(path);
        }

        private static bool TryRead(string path, out SaveData data)
        {
            data = null;
            try
            {
                return SaveSerializer.TryUnwrap(File.ReadAllText(path, Encoding.UTF8), out data);
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
