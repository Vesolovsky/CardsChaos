using System;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.Utils
{
    public class FileHandler 
    {
        private static string JSON_EXTENSION = ".json";

        #region Save
        public static async Task SaveToJSON<T>(T dataToSave, string path)
        {
            if (!path.Contains(JSON_EXTENSION))
            {
                path += JSON_EXTENSION;
            }

            var content = JsonConvert.SerializeObject(dataToSave);
            await WriteFile(path, content);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        public static async UniTask WriteFile(string fullPath, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException());
            FileStream fileStream = new FileStream(fullPath, FileMode.Create);
            StreamWriter writer = new StreamWriter(fileStream);
            await writer.WriteAsync(content).AsUniTask();
            writer.Close();
            fileStream.Close();
        }

        public static async UniTask SaveTexture(Texture2D texture, string folderPath, string textureName)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            byte[] fileData = texture.EncodeToPNG();

            using (var writer = new FileStream(folderPath + textureName, FileMode.Create, FileAccess.Write))
            {
                await writer.WriteAsync(fileData, 0, fileData.Length).AsUniTask();
            }
        }

        #endregion

        #region Load
        public static async UniTask<T> ReadFromJSON<T>(string path)
        {
            if(!File.Exists(path))
            {
                return default;
            }

            var content = await ReadFile(path);
            return JsonConvert.DeserializeObject<T>(content);
        }

        public static async UniTask<string> ReadFile(string path)
        {
            using (StreamReader reader = new StreamReader(path))
            {
                var content = await reader.ReadToEndAsync().AsUniTask();
                return content;
            }
        }

        public static Texture2D ReadTexture(string path)
        {
            if (!File.Exists(path))
            {
                Debug.Log($"File at path '{path}' does not exist. Could not load texture.");
                return null;
            }

            var textureBytes = File.ReadAllBytes(path);

            Texture2D texture = new Texture2D(1, 1);

            if (texture.LoadImage(textureBytes))
            {
                return texture;
            }
            else
            {
                Debug.LogError($"Could not load texture file from path '{path}'");
                return null;
            }
        }

        #endregion

        public static void DeleteDirectory(string path)
        {
#if UNITY_EDITOR
            if (AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
                Debug.Log($"Directory at path '{path}' was deleted.");
            }
            else
            {
                Debug.LogError($"Trying to delete folder at path '{path}' but it does not exist!");
            }
#endif
        }
    }
}
