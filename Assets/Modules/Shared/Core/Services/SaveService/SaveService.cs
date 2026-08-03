using System.IO;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem.Init;
using Newtonsoft.Json;
using UnityEngine;
using System;

namespace Vesolovsky.Core.Services.Save
{
    public abstract class SaveService<T> : ISaveService<T>, IAsyncInitializable where T : IGameSave
    {
        public event Action Saved;

        public static readonly string SAVED_FILE_PATH = Path.Combine(Application.persistentDataPath, "gameSave.json");

        private T _currentSave;

        public T CurrentSave => _currentSave;


        public async UniTask Initialize()
        {
            _currentSave = await LoadData();
            if (_currentSave == null || SaveRequireReset()) // so it's first player game
            {
                _currentSave = CreateInitialSave();
            }
        }

        protected abstract bool SaveRequireReset();

        protected abstract T CreateInitialSave();

        private async UniTask<T> LoadData()
        {
            T data = default;

            if (File.Exists(SAVED_FILE_PATH) == false)
            {
                return default;
            }

            var json = await File.ReadAllTextAsync(SAVED_FILE_PATH);
            if (json.Length > 0)
            {
                data = JsonConvert.DeserializeObject<T>(json);
            }

            return data;
        }

        public async UniTask Save()
        {
            // The live save is mutated in place by gameplay (wallet, album). Take an isolated deep
            // copy on the main thread first, so serializing it on a background thread cannot read a
            // half-changed object. Compact formatting keeps a save with hundreds of cards small.
            var snapshot = (T)_currentSave.Clone();

            string json = await UniTask.RunOnThreadPool(
                () => JsonConvert.SerializeObject(snapshot, Formatting.None));

            EnsureDirectory();
            await File.WriteAllTextAsync(SAVED_FILE_PATH, json);

            // The work above may leave us on a thread-pool thread; Saved listeners touch Unity
            // objects, so hand the continuation back to the main thread before raising it.
            await UniTask.SwitchToMainThread();
            Saved?.Invoke();
        }

        public void SaveBlocking()
        {
            // The quit path. Nothing else runs while this blocks, so there is no race to guard
            // against and no copy is needed - serialize the live save straight to disk.
            string json = JsonConvert.SerializeObject(_currentSave, Formatting.None);

            EnsureDirectory();
            File.WriteAllText(SAVED_FILE_PATH, json);

            Saved?.Invoke();
        }

        public abstract void ClearSave();

        private static void EnsureDirectory()
        {
            var dirName = Path.GetDirectoryName(SAVED_FILE_PATH);
            if (Directory.Exists(dirName) == false)
            {
                Directory.CreateDirectory(dirName);
            }
        }
    }
}