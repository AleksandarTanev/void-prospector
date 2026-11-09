using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VoidProspector.Core
{
    /// <summary>
    /// Reads and writes the save file without ever leaving a half-written file in its place:
    /// the new text goes to <c>.tmp</c> and is flushed to disk, the old save becomes <c>.bak</c>, then the temp
    /// file is moved into place. A crash at any point leaves either the new save, or the previous one as backup.
    /// </summary>
    public sealed class SaveStore
    {
        public string FilePath { get; }
        public string TempPath => FilePath + ".tmp";
        public string BackupPath => FilePath + ".bak";

        public SaveStore(string filePath) => FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        public bool Exists => File.Exists(FilePath) || File.Exists(BackupPath);

        public void Write(string text)
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (File.Exists(FilePath))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(FilePath, BackupPath);
            }
            File.Move(TempPath, FilePath);
        }

        /// <summary>The save first, then the backup: the caller uses the first one that parses.</summary>
        public IEnumerable<(string path, string text)> ReadCandidates()
        {
            foreach (var path in new[] { FilePath, BackupPath })
            {
                string text = null;
                try { if (File.Exists(path)) text = File.ReadAllText(path, Encoding.UTF8); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                if (text != null) yield return (path, text);
            }
        }

        /// <summary>Deletes the save, its backup and any leftover temp file.</summary>
        public void Delete()
        {
            foreach (var path in new[] { FilePath, BackupPath, TempPath })
                if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// Loads the newest save that parses. Returns null for a new player (or if nothing usable was found;
        /// <paramref name="problems"/> then explains why each file was rejected).
        /// </summary>
        public SaveData Load(List<string> problems = null)
        {
            foreach (var (path, text) in ReadCandidates())
            {
                var data = SaveSystem.FromJson(text, out var error);
                if (data != null) return data;
                problems?.Add($"{Path.GetFileName(path)}: {error}");
            }
            return null;
        }
    }
}
