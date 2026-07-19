using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vesolovsky.Core.EditorToolbag
{
    public static class EnumFileModifier
    {
        public static void AddEntryToEnum(string enumFilePath, string entryName)
        {
            if (!File.Exists(enumFilePath))
            {
                Debug.LogError($"View Enum file not found at path: '{enumFilePath}'");
                return;
            }

            var lines = File.ReadAllLines(enumFilePath).ToList();

            if(lines.Any(line => line.Contains(entryName)))
            {
                Debug.LogWarning($"Enum at path: '{enumFilePath}' already contains entry with name: '{entryName}'");
                return;
            }

            int enumStartIndex = lines.FindIndex(line => line.Contains("public enum ViewName"));
            int enumEndIndex = lines.FindIndex(enumStartIndex, line => line.Contains("}"));

            if (enumStartIndex == -1 || enumEndIndex == -1)
            {
                Debug.LogError("Enum definition not found in the file.");
                return;
            }

            var uniqueEnumId = GenerateUniqueEnumId(lines, enumStartIndex, enumEndIndex);
            string newEntry = $"        {entryName} = {uniqueEnumId},";
            lines.Insert(enumEndIndex, newEntry);

            File.WriteAllLines(enumFilePath, lines);

            Debug.Log($"Added new enum entry: {newEntry}");
        }

        private static int GenerateUniqueEnumId(List<string> lines, int enumStartIndex, int enumEndIndex)
        {
            var existingValues = new HashSet<int>();
            for (int i = enumStartIndex + 1; i < enumEndIndex; i++)
            {
                string line = lines[i].Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || !line.Contains("="))
                    continue;
                string[] parts = line.Split('=');
                if (parts.Length < 2)
                    continue;

                if (int.TryParse(parts[1].Split(',')[0].Trim(), out int value))
                {
                    existingValues.Add(value);
                }
            }

            int uniqueId;
            var random = new System.Random();
            do
            {
                uniqueId = random.Next(100000, 999999);
            } while (existingValues.Contains(uniqueId));
            return uniqueId;
        }
    }
}
