using System.IO;
using System.Text;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Editor
{
    public static class ViewFactoryModifier
    {
        private const string FACTORY_FILE_PATH = "Assets/Modules/Unique/Game/Core/UISystem/ViewDefaultDefinitionFactory.cs";

        public static void AddViewToFactory(string viewName)
        {
            if (!File.Exists(FACTORY_FILE_PATH))
            {
                Debug.LogError($"Factory file not found at path: '{FACTORY_FILE_PATH}'");
                return;
            }

            var fileContent = File.ReadAllText(FACTORY_FILE_PATH);
            var updatedContent = new StringBuilder(fileContent);

            string viewCasePattern = $"case {viewName}View:";
            string viewReturnPattern = $"return new {viewName}ViewDefinition();";

            if (!fileContent.Contains(viewCasePattern))
            {
                string methodPattern = "switch (view)";
                int methodStartIndex = fileContent.IndexOf(methodPattern);
                if (methodStartIndex != -1)
                {
                    int defaultIndex = fileContent.IndexOf("default:", methodStartIndex);
                    if (defaultIndex != -1)
                    {
                        updatedContent.Insert(defaultIndex,
                            $"{viewCasePattern}\n                    {viewReturnPattern}\n\n                ");
                    }
                }
            }

            string viewNameCasePattern = $"case ViewName.{viewName}:";
            string viewNameReturnPattern = $"return new {viewName}ViewDefinition();";

            if (!fileContent.Contains(viewNameCasePattern))
            {
                var updatedContentString = updatedContent.ToString();
                string methodPattern = "switch (viewName)";
                int methodStartIndex = updatedContentString.IndexOf(methodPattern);
                if (methodStartIndex != -1)
                {
                    int defaultIndex = updatedContentString.IndexOf("default:", methodStartIndex);
                    if (defaultIndex != -1)
                    {
                        updatedContent.Insert(defaultIndex,
                            $"{viewNameCasePattern}\n                    {viewNameReturnPattern}\n\n                ");
                    }
                }
            }

            File.WriteAllText(FACTORY_FILE_PATH, updatedContent.ToString());
            Debug.Log($"Successfully added '{viewName}' cases to factory file.");
        }
    }
}
