using System.Reflection;
using System;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor;
using UnityEngine;
using Zenject;
using System.Collections.Generic;

namespace Vesolovsky.Core.UISystem.Editor
{
    public static class ViewPrefabFileCreator
    {
        private const string PREFAB_PATH = "Assets/Modules/Shared/Core/UISystem/Prototypes/ViewPrototype.prefab";

        public static GameObject CreateViewFromPrefab(string fullViewName, string prefabSaveDirectoryPath, string viewNamespace)
        {
            GameObject prefab = LoadPrefab();
            if (prefab == null) return null;

            GameObject viewObject = InstantiatePrefab(prefab, fullViewName);
            if (viewObject == null) return null;

            try
            {
                AddViewComponent(viewObject, fullViewName, viewNamespace);
                AddInstallerComponent(viewObject, fullViewName, viewNamespace);
                AddInstallerToContext(viewObject);
                string prefabPath = SavePrefab(viewObject, prefabSaveDirectoryPath);
                AddToAddressables(fullViewName, prefabPath);
            }
            catch (Exception ex)
            {
                HandleCreationError(viewObject, ex);
                return null;
            }

            return viewObject;
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"Prefab not found at path: '{PREFAB_PATH}'");
            }
            return prefab;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, string fullViewName)
        {
            GameObject viewObject = GameObject.Instantiate(prefab);
            viewObject.name = fullViewName;
            return viewObject;
        }

        private static void AddViewComponent(GameObject viewObject, string fullViewName, string viewNamespace)
        {
            string viewTypeName = $"{viewNamespace}.{fullViewName}";
            Type viewType = Type.GetType($"{viewTypeName}, Assembly-CSharp");
            if (viewType == null)
            {
                throw new Exception($"View type '{viewTypeName}' not found.");
            }

            var viewComponent = viewObject.AddComponent(viewType);
            InvokeFindRoot(viewComponent, viewType);
        }

        private static void InvokeFindRoot(Component viewComponent, Type viewType)
        {
            var findRootMethod = viewType.BaseType?.GetMethod("FindRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            if (findRootMethod != null)
            {
                findRootMethod.Invoke(viewComponent, null);
            }
            else
            {
                Debug.LogError($"Method 'FindRoot' not found in base type of '{viewType.FullName}'.");
            }
        }

        private static void AddInstallerComponent(GameObject viewObject, string fullViewName, string viewNamespace)
        {
            string installerTypeName = $"{viewNamespace}.{fullViewName}Installer, Assembly-CSharp";
            Type installerType = Type.GetType(installerTypeName);
            if (installerType == null)
            {
                throw new Exception($"Installer type '{installerTypeName}' not found.");
            }

            viewObject.AddComponent(installerType);
        }

        private static void AddInstallerToContext(GameObject viewObject)
        {
            var context = viewObject.GetComponent<GameObjectContext>();
            if (context == null)
            {
                throw new Exception("GameObjectContext not found on the prefab.");
            }

            var monoInstallers = GetMonoInstallersList(context);
            AddInstallerToList(context, monoInstallers);
        }

        private static List<MonoInstaller> GetMonoInstallersList(GameObjectContext context)
        {
            var monoInstallersField = typeof(GameObjectContext).BaseType?.BaseType
                ?.GetField("_monoInstallers", BindingFlags.NonPublic | BindingFlags.Instance);

            if (monoInstallersField == null)
            {
                Debug.LogError("_monoInstallers field not found in the class hierarchy.");
                return new List<MonoInstaller>();
            }

            return (List<MonoInstaller>)monoInstallersField.GetValue(context);
        }

        private static void AddInstallerToList(GameObjectContext context, List<MonoInstaller> monoInstallers)
        {
            var installerComponent = context.GetComponent<MonoInstaller>();
            monoInstallers.Add(installerComponent);
            UpdateMonoInstallersField(context, monoInstallers);
        }

        private static void UpdateMonoInstallersField(GameObjectContext context, List<MonoInstaller> monoInstallers)
        {
            var monoInstallersField = typeof(GameObjectContext).BaseType?.BaseType
                ?.GetField("_monoInstallers", BindingFlags.NonPublic | BindingFlags.Instance);
            monoInstallersField?.SetValue(context, monoInstallers);
        }

        private static string SavePrefab(GameObject viewObject, string prefabSaveDirectoryPath)
        {
            string prefabPath = $"{prefabSaveDirectoryPath}/{viewObject.name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(viewObject, prefabPath, InteractionMode.UserAction);
            Debug.Log($"Prefab saved at '{prefabPath}'.");
            return prefabPath;
        }

        private static void AddToAddressables(string viewName, string viewPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("AddressableAssetSettings not found.");
                return;
            }

            var group = FindOrCreateAddressableGroup(settings);
            CreateAddressableEntry(settings, group, viewName, viewPath);
        }

        private static AddressableAssetGroup FindOrCreateAddressableGroup(AddressableAssetSettings settings)
        {
            string groupName = "Views";
            return settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema));
        }

        private static void CreateAddressableEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string viewName, string viewPath)
        {
            var viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(viewPath);
            string assetPath = AssetDatabase.GetAssetPath(viewPrefab);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
            entry.address = viewName;
            Debug.Log($"Added '{viewName}' to Addressables.");
            SaveAddressablesSettings(settings);
        }

        private static void SaveAddressablesSettings(AddressableAssetSettings settings)
        {
            EditorUtility.SetDirty(settings);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        }

        private static void HandleCreationError(GameObject viewObject, Exception ex)
        {
            Debug.LogError($"Error while creating view: {ex.Message}");
            GameObject.DestroyImmediate(viewObject);
        }
    }
}
