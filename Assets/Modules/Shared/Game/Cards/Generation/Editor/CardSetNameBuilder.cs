using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Fills in the readable set names the album's buttons show, derived from the folder ids.
    ///
    /// Deliberately a separate menu item from Build All Card Sets. The derived name is a decent
    /// first draft and nothing more - it cannot know that Ballon'dOrs wants to read "Ballon d'Or",
    /// or that MagicalButerrflies is a typo - so the rebuild that runs constantly must not touch
    /// the field, and the pass that overwrites it must be something you choose to run.
    ///
    /// It does overwrite, so every change is logged as before/after and the whole run is a single
    /// undo step.
    /// </summary>
    public static class CardSetNameBuilder
    {
        [MenuItem("Tools/Cards/Build Set Names")]
        public static void BuildAll()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CardSetDefinition)}");
            var changes = new List<string>();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Set Names");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CardSetDefinition>(path);

                if (definition == null)
                    continue;

                var serialized = new SerializedObject(definition);
                SerializedProperty nameProperty = serialized.FindProperty("setName");

                string previous = nameProperty.stringValue;
                string generated = Humanize(serialized.FindProperty("setId").stringValue);

                if (previous == generated)
                    continue;

                nameProperty.stringValue = generated;

                // ApplyModifiedProperties rather than the WithoutUndo variant the rest of the
                // generation uses: this is the one pass that can destroy hand-written text, so
                // Ctrl+Z has to be able to bring it back.
                serialized.ApplyModifiedProperties();

                changes.Add(string.IsNullOrEmpty(previous)
                    ? $"  {definition.SetId}: -> \"{generated}\""
                    : $"  {definition.SetId}: \"{previous}\" -> \"{generated}\"");
            }

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();

            if (changes.Count == 0)
            {
                Debug.Log($"[CardSetNameBuilder] {guids.Length} set(s) checked, all names already current.");
                return;
            }

            Debug.Log(
                $"[CardSetNameBuilder] Rewrote {changes.Count} of {guids.Length} set name(s). " +
                $"Undo restores them.\n{string.Join("\n", changes)}");
        }

        /// <summary>
        /// Turns a folder id into a sentence: "18WheelsOfFutureSteel" reads
        /// "18 Wheels of future steel", "MyFirstPainting!" reads "My first painting!".
        ///
        /// Words are cut at the capitals, then everything past the first word is lowered, which
        /// is what makes a long id read as a title rather than as a stack of proper nouns.
        /// </summary>
        public static string Humanize(string setId)
        {
            if (string.IsNullOrEmpty(setId))
                return string.Empty;

            var builder = new StringBuilder(setId.Length + 8);

            for (int i = 0; i < setId.Length; i++)
            {
                if (i > 0 && StartsWord(setId, i))
                    builder.Append(' ');

                builder.Append(setId[i]);
            }

            return LowerAfterFirstWord(builder.ToString());
        }

        /// <summary>
        /// True when the capital at <paramref name="index"/> opens a new word.
        ///
        /// A capital only starts one when it follows a letter or a digit - which is what keeps
        /// "Fur-Ever&amp;Always" in one piece, since the hyphen and ampersand are already doing
        /// the separating. Two capitals in a row are a word boundary only when the second is
        /// followed by lowercase, so "ABench" splits into "A Bench" while an acronym would not
        /// be cut into letters.
        /// </summary>
        private static bool StartsWord(string text, int index)
        {
            if (!char.IsUpper(text[index]))
                return false;

            char previous = text[index - 1];

            if (char.IsLower(previous) || char.IsDigit(previous))
                return true;

            return char.IsUpper(previous)
                   && index + 1 < text.Length
                   && char.IsLower(text[index + 1]);
        }

        /// <summary>
        /// Lowers every word after the first one that actually starts with a letter, so a leading
        /// number keeps the following word capitalised: "18 Wheels of future steel".
        /// </summary>
        private static string LowerAfterFirstWord(string text)
        {
            string[] words = text.Split(' ');
            bool seenWord = false;

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0)
                    continue;

                if (!seenWord)
                {
                    // A leading "18" is not the word that gets to keep its capital - the one
                    // after it is.
                    seenWord = char.IsLetter(words[i][0]);
                    continue;
                }

                words[i] = words[i].ToLowerInvariant();
            }

            return string.Join(" ", words);
        }
    }
}
