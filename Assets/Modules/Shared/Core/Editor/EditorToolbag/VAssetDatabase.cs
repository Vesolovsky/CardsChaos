using UnityEditor;

namespace Vesolovsky.Core.EditorToolbag
{
    public static class VAssetDatabase 
    {
        public enum AssetType
        {
            All = 0,
            Script,
        }

        public static string GetTypeFilter(AssetType type)
        {
            return type switch
            {
                AssetType.All => string.Empty,
                AssetType.Script => "t:Script",
                _ => string.Empty,
            };
        }

        public static bool DoesAssetExist(string assetName, AssetType assetType = AssetType.All)
        {
            string[] guids = AssetDatabase.FindAssets($"{GetTypeFilter(assetType)} {assetName}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (fileName == assetName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
