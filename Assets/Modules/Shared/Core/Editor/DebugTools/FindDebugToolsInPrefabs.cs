using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vesolovsky.Core.Utils.Editor
{
    /// <summary>
    /// Lists every prefab carrying an <see cref="IDebugTool"/>.
    ///
    /// The build's scene pass cannot help with prefabs: a prefab loaded at runtime is an asset, not
    /// part of any scene, so a tool left on one would ride into the build as a missing script and
    /// say so in the log every time the prefab is spawned. The tool itself would still not run -
    /// its code is not compiled in - but the noise is the tell that one has gone somewhere it
    /// should not.
    ///
    /// So: debug tools belong on scene objects. Run this before shipping to be sure none have
    /// wandered onto a prefab. It is a menu item rather than a build step because the project has
    /// well over a thousand card prefabs and reading them all is not something a build should do
    /// every time.
    /// </summary>
    public static class FindDebugToolsInPrefabs
    {
        [MenuItem("Vesolovsky/Debug Tools/Find Debug Tools In Prefabs")]
        private static void Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var found = new List<string>();

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Find Debug Tools In Prefabs", path, (i + 1) / (float)guids.Length);

                    if (cancelled)
                        return;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab == null)
                        continue;

                    foreach (IDebugTool tool in prefab.GetComponentsInChildren<IDebugTool>(true))
                    {
                        if (tool is Component component)
                            found.Add($"{component.GetType().Name} in '{path}'");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (found.Count == 0)
            {
                Debug.Log($"[DebugTools] Checked {guids.Length} prefab(s): no debug tools on any " +
                          "of them. Nothing will leak into a build.");

                return;
            }

            var message = new StringBuilder();
            message.AppendLine($"[DebugTools] {found.Count} debug tool(s) sitting on prefabs. Move " +
                               "them onto a scene object, or they will ship as missing scripts:");

            foreach (string entry in found)
                message.AppendLine($"  - {entry}");

            Debug.LogWarning(message.ToString());
        }
    }
}
