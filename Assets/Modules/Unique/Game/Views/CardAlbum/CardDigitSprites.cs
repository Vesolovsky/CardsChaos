using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// The ten digits the album spells card numbers out of, each with the inner shadow that sits
    /// over it.
    ///
    /// An asset rather than a field on the slot, because ten pairs of sprites are data, not
    /// layout: the slot prefab is cloned once per card on the open page, and every one of those
    /// copies would otherwise carry its own twenty references to the same art.
    /// </summary>
    [CreateAssetMenu(menuName = "CardsChaos/Card Digit Sprites", fileName = "CardDigitSprites")]
    public class CardDigitSprites : ScriptableObject
    {
        [System.Serializable]
        public struct Digit
        {
            public Sprite Glyph;
            public Sprite InnerShadow;
        }

        public const int Count = 10;

        [Tooltip("Zero through nine, in order. Fill it with the button in this asset's context " +
                 "menu rather than by hand - a digit dragged into the wrong slot is a bug you " +
                 "only find by counting an album page.")]
        [SerializeField] private Digit[] digits = new Digit[Count];

        [Tooltip("Where the artwork lives, for the fill button. Files are expected to be named " +
                 "0.png through 9.png with a matching 0_InnerShadow.png beside each.")]
        [SerializeField] private string sourceFolder =
            "Assets/Modules/Unique/Game/Views/CardAlbum/Art/Numbers";

        /// <summary>The art for one digit. False when the set has not been filled in properly.</summary>
        public bool TryGet(int value, out Digit digit)
        {
            if (value < 0 || value >= Count || digits == null || value >= digits.Length)
            {
                digit = default;
                return false;
            }

            digit = digits[value];
            return digit.Glyph != null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reads the ten digits straight out of the folder, by name.
        ///
        /// Anything it cannot find is named in the log rather than quietly skipped - a set with a
        /// hole in it draws a blank digit, which is far harder to trace back to a misnamed file
        /// than a line in the console.
        /// </summary>
        [ContextMenu("Fill From Source Folder")]
        private void FillFromSourceFolder()
        {
            if (digits == null || digits.Length != Count)
                digits = new Digit[Count];

            var missing = new List<string>();

            for (int value = 0; value < Count; value++)
            {
                string glyphPath = $"{sourceFolder}/{value}.png";
                string shadowPath = $"{sourceFolder}/{value}_InnerShadow.png";

                var glyph = AssetDatabase.LoadAssetAtPath<Sprite>(glyphPath);
                var shadow = AssetDatabase.LoadAssetAtPath<Sprite>(shadowPath);

                if (glyph == null)
                    missing.Add(glyphPath);

                if (shadow == null)
                    missing.Add(shadowPath);

                digits[value] = new Digit { Glyph = glyph, InnerShadow = shadow };
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            if (missing.Count == 0)
            {
                Debug.Log($"[{nameof(CardDigitSprites)}] All {Count} digits loaded from '{sourceFolder}'.", this);
                return;
            }

            Debug.LogError(
                $"[{nameof(CardDigitSprites)}] {missing.Count} file(s) not found. Rename them to " +
                $"match, then run this again:\n  {string.Join("\n  ", missing)}", this);
        }
#endif
    }
}
