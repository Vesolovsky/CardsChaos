#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vesolovsky.Core.EditorToolbag
{
    public static class ScriptTemplatesSetting
    {
        private static readonly string SettingsPath = "Project/Vesolovsky/Script templates";

        private static readonly List<ScriptTemplate> defaultTemplates = new();

        private static ScriptTemplate selectedTemplate;

        public static readonly string TemplateFolderPath = Path.Combine(EditorApplication.applicationContentsPath, "Resources/ScriptTemplates");

        static ScriptTemplatesSetting()
        {
            var templateFiles = Directory.GetFiles(TemplateFolderPath, "*.cs.txt");
            defaultTemplates = templateFiles.Select(filePath => new ScriptTemplate(Path.GetFileNameWithoutExtension(filePath), Path.GetFileName(filePath))).ToList();
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Built-in Script Templates",
                guiHandler = (searchContext) => Draw(),
                keywords = new HashSet<string>(new[] { "Script", "Templates" }),
            };

            return provider;
        }

        private static void Draw()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            {
                ShowScriptList();
            }

            GUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(selectedTemplate == null);
            {
                ShowScript();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
        }

        private static void ShowScriptList()
        {
            var normalStyle = new GUIStyle(EditorStyles.toolbarButton);
            normalStyle.alignment = TextAnchor.MiddleLeft;

            var selectedStyle = new GUIStyle(EditorStyles.selectionRect);
            selectedStyle.fontStyle = FontStyle.Bold;
            selectedStyle.alignment = TextAnchor.MiddleLeft;

            foreach (var template in defaultTemplates)
            {
                if (GUILayout.Button(template.Name, template == selectedTemplate ? selectedStyle : normalStyle, GUILayout.ExpandWidth(true)))
                {
                    if (selectedTemplate != null && selectedTemplate != template && selectedTemplate.IsDirty)
                    {
                        if (EditorUtility.DisplayDialog($"{selectedTemplate.Name} edited", $"Do you want to save changes in {selectedTemplate.Name}", "Yes", "No"))
                        {
                            selectedTemplate.Save();
                        }
                    }

                    selectedTemplate = template;
                    selectedTemplate.Refresh();
                }
                GUI.enabled = true;
            }
        }

        private static void ShowScript()
        {
            if(selectedTemplate == null)
            {
                selectedTemplate = defaultTemplates[0];
            }

            selectedTemplate.UserEdits = GUILayout.TextArea(selectedTemplate.UserEdits, GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Reset to Default", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    selectedTemplate.Reset();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open directory", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    selectedTemplate.OpenDirectory();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open...", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    selectedTemplate.Open();

                GUI.enabled = selectedTemplate.IsDirty;
                if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    selectedTemplate.Save();

                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }
    }

    public sealed class ScriptTemplate
    {
        public ScriptTemplate(string name, string fileName)
        {
            Name = name;
            FilePath = Path.Combine(ScriptTemplatesSetting.TemplateFolderPath, fileName);
            DefaultFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileName);
            Content = File.ReadAllText(FilePath);
            UserEdits = Content;

            if (File.Exists(DefaultFile))
                return;

            if (!Directory.Exists(Path.GetDirectoryName(DefaultFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(DefaultFile));

            File.WriteAllText(DefaultFile, Content);
        }

        public string Name { get; }
        public string FilePath { get; }
        public string DefaultFile { get; }
        public string Content { get; set; }
        public string UserEdits { get; set; }
        public bool IsDirty => Content != UserEdits;

        public void Open()
        {
            Process.Start(FilePath);
        }

        public void OpenDirectory()
        {
            Process.Start(ScriptTemplatesSetting.TemplateFolderPath);
        }

        public void Save()
        {
            string scriptPath = FileUtil.GetPhysicalPath(@"Assets\Modules\Shared\Core\EditorTools\ScriptTemplateEditor\WriteFileAdmin.ps1");
            string command = $"-WindowStyle hidden -ExecutionPolicy Bypass -File \"{scriptPath}\" -FilePath \"{FilePath}\" -Content \"{UserEdits}\"";

            Process process = new()
            {
                StartInfo = new(scriptPath)
                {
                    FileName = "powershell.exe",
                    Arguments = command,
                    Verb = "runas",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            UnityEngine.Debug.Log($"Saving...\nFile name = {Name}\nPath = {FilePath}\nDefault file path = {DefaultFile}");

            Content = UserEdits;
        }

        public void Reset()
        {
            UserEdits = File.ReadAllText(DefaultFile);
            Save();
        }

        public void Refresh()
        {
            UserEdits = Content;
        }
    }
}
#endif