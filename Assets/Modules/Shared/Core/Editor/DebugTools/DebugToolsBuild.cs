using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Vesolovsky.Core.Utils.Editor
{
    /// <summary>
    /// Whether development tools - the cheats and the trailer rig, everything marked
    /// <see cref="IDebugTool"/> - are part of the build being made.
    ///
    /// The answer is one scripting define, <see cref="Define"/>. Without it the tools' code is not
    /// compiled into the player at all (each file is written behind
    /// <c>#if UNITY_EDITOR || CARDSCHAOS_DEBUG_TOOLS</c>) and
    /// <see cref="StripDebugToolsFromScenes"/> takes their objects out of the scenes on the way
    /// through, so nothing is left pointing at a missing script. With it, both come back and the
    /// build can cheat and film exactly like the editor can.
    ///
    /// Nothing here changes what the editor does: in the editor the tools are always available,
    /// which is the whole point of them.
    /// </summary>
    public static class DebugToolsBuild
    {
        public const string Define = "CARDSCHAOS_DEBUG_TOOLS";

        private const string MenuPath = "Vesolovsky/Debug Tools/Include In Builds";

        /// <summary>The platform the build window is currently pointed at.</summary>
        public static NamedBuildTarget SelectedTarget =>
            NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        public static bool IsIncludedIn(NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);

            foreach (string define in defines)
            {
                if (define == Define)
                    return true;
            }

            return false;
        }

        public static void SetIncludedIn(NamedBuildTarget target, bool included)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);

            var kept = new System.Collections.Generic.List<string>(defines.Length + 1);

            foreach (string define in defines)
            {
                if (define != Define)
                    kept.Add(define);
            }

            if (included)
                kept.Add(Define);

            PlayerSettings.SetScriptingDefineSymbols(target, kept.ToArray());

            Debug.Log($"[DebugTools] {(included ? "Included in" : "Excluded from")} builds for " +
                      $"{target.TargetName}. Scripts are recompiling.");
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            NamedBuildTarget target = SelectedTarget;
            SetIncludedIn(target, !IsIncludedIn(target));
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, IsIncludedIn(SelectedTarget));
            return true;
        }
    }
}
