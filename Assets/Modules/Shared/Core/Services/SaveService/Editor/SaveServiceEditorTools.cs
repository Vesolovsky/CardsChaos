using System.IO;
using UnityEditor;
using UnityEngine;
using Vesolovsky.Game.Services.Save;

namespace Vesolovsky.Core.Services.Save.Editor
{
    public class SaveServiceEditorTools
    {
        [MenuItem("Vesolovsky/Clear Save", false)]
        public static void ClearSaveEditor()
        {
            ClearSave();
        }
        
        private static void ClearSave()
        {
            PlayerPrefs.DeleteAll();
            var savePath = SaveService<IGameSave>.SAVED_FILE_PATH;
            var dirName = Path.GetDirectoryName(savePath);
            if (Directory.Exists(dirName) == false)
            {
                Directory.CreateDirectory(dirName);
            }

            File.WriteAllText(savePath, "");
        }

        //TODO: Add to the core
        [MenuItem("Vesolovsky/Open Save Location", false)]
        public static void OpenSaveLocation()
        {
            string path = GameSaveService.SAVED_FILE_PATH;

            EditorUtility.RevealInFinder(path);
        }
    }
}