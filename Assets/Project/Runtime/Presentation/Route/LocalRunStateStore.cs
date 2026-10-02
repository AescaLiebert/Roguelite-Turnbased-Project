using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Run;
using UnityEngine;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Offline development persistence only. Online runs must be read from the trusted run service.</summary>
    public sealed class LocalRunStateStore
    {
        private readonly string _path;
        public LocalRunStateStore(string fileName = "fighting-allstar-local-run.json")
        {
            var subjectId = PlayerInventoryService.Instance == null
                ? "guest:local-development"
                : PlayerInventoryService.Instance.SubjectId;
            using (var sha = SHA256.Create())
            {
                var key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(subjectId ?? "guest:local-development")))
                    .Replace("-", string.Empty).ToLowerInvariant();
                _path = Path.Combine(Application.persistentDataPath, "FightingAllstar", "Profiles", key, fileName);
            }
        }

        public void Save(RunState run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Local run profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            var backup = _path + ".bak";
            File.WriteAllText(temp, JsonUtility.ToJson(run));
            if (!File.Exists(_path))
            {
                File.Move(temp, _path);
                return;
            }
            if (File.Exists(backup)) File.Delete(backup);
            File.Replace(temp, _path, backup);
            try { if (File.Exists(backup)) File.Delete(backup); }
            catch (IOException) { /* The new snapshot is committed; a stale backup is safe to keep. */ }
        }

        public bool TryLoad(out RunState run)
        {
            run = null;
            var backup = _path + ".bak";
            var candidates = new[] { _path, backup };
            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    run = JsonUtility.FromJson<RunState>(File.ReadAllText(candidate));
                    if (run != null && !string.IsNullOrWhiteSpace(run.RunId)) return true;
                    run = null;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Local run snapshot could not be read from " + candidate + ": " + exception.Message);
                }
            }
            return false;
        }

        public void Clear()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_path + ".bak")) File.Delete(_path + ".bak");
            if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
        }
    }
}
