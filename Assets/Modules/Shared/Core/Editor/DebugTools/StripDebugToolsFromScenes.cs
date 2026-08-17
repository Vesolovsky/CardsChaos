using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vesolovsky.Core.Utils.Editor
{
    /// <summary>
    /// Takes every <see cref="IDebugTool"/> out of the scenes on their way into a build - unless the
    /// build was asked for them (see <see cref="DebugToolsBuild"/>).
    ///
    /// This is the other half of compiling the tools out. Without it a build would still carry the
    /// Cheats object and the trailer rig as objects pointing at scripts that are no longer there,
    /// and every one of them would announce itself in the player log on load.
    ///
    /// Unity hands this a throwaway copy of the scene, so nothing here touches the scene asset on
    /// disk. The same callback fires on entering play mode, with no report - that case is left
    /// alone, because the editor is where the tools are supposed to work.
    /// </summary>
    public class StripDebugToolsFromScenes : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null)
                return;

            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(report.summary.platform));

            if (DebugToolsBuild.IsIncludedIn(target))
                return;

            var removed = new List<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (IDebugTool tool in root.GetComponentsInChildren<IDebugTool>(true))
                {
                    var component = tool as Component;

                    // Already gone: a tool sharing an object with one taken out below. Unity's
                    // null, not C#'s, which is why this is a comparison and not a pattern.
                    if (component == null)
                        continue;

                    GameObject owner = component.gameObject;
                    removed.Add($"{component.GetType().Name} on '{PathOf(owner)}'");

                    Object.DestroyImmediate(component);

                    // An object that existed only to carry a tool goes with it, and so does whatever
                    // it was built out of - a dolly track's waypoints are a dozen empty transforms
                    // that mean nothing without the track. An object still carrying real work (a
                    // scroll view, say) stays exactly as it was.
                    if (IsBare(owner))
                        Object.DestroyImmediate(owner);
                }
            }

            if (removed.Count == 0)
                return;

            var message = new StringBuilder();
            message.AppendLine($"[DebugTools] Stripped {removed.Count} debug tool(s) from " +
                               $"'{scene.name}' on the way into the build:");

            foreach (string entry in removed)
                message.AppendLine($"  - {entry}");

            Debug.Log(message.ToString());
        }

        /// <summary>
        /// Whether an object and everything under it is nothing but transforms - scaffolding the
        /// tool was hanging on, with no work of its own left to do.
        /// </summary>
        private static bool IsBare(GameObject target)
        {
            foreach (Component component in target.GetComponentsInChildren<Component>(true))
            {
                if (component is not Transform)
                    return false;
            }

            return true;
        }

        private static string PathOf(GameObject target)
        {
            string path = target.name;

            for (Transform parent = target.transform.parent; parent != null; parent = parent.parent)
                path = $"{parent.name}/{path}";

            return path;
        }
    }
}
