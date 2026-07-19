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
            await SaveData();
            Saved?.Invoke();
        }

        public abstract void ClearSave();

        private async UniTask SaveData()
        {
            string json = JsonConvert.SerializeObject(_currentSave, Formatting.Indented);

            var dirName = Path.GetDirectoryName(SAVED_FILE_PATH);
            if (Directory.Exists(dirName) == false)
            {
                Directory.CreateDirectory(dirName);
            }

            await File.WriteAllTextAsync(SAVED_FILE_PATH, json);
        }
    }
}