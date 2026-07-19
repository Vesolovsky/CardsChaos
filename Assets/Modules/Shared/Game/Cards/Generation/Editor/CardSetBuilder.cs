using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// End to end generation of the card assets: texture import settings, the shared mesh,
    /// the base prefab and one prefab variant per card in a set.
    /// </summary>
    public static class CardSetBuilder
    {
        private const string CardsRoot = "Assets/Modules/Shared/Game/Cards";
        private const string BaseFolder = CardsRoot + "/Base";
        private const string BaseArtFolder = BaseFolder + "/Art";
        private const string SetsRoot = CardsRoot + "/Sets";
        private const string ShaderName = "CardsChaos/Card Lit";

        private const string MeshPath = BaseArtFolder + "/CardMesh.asset";
        private const string BaseMaterialPath = BaseArtFolder + "/M_Card_Base.mat";
        private const string BasePrefabPath = BaseFolder + "/Card_Base.prefab";

        // Layout expected inside each Sets/<SetId> folder.
        private const string SetTexturesSubfolder = "Art/Sprites";
        private const string SetMaterialsSubfolder = "Art/Materials";
        private const string SetPrefabsSubfolder = "Prefabs";

        private const string BackTexturePrefix = "Revers_";

        [MenuItem("Tools/Cards/Build All Card Sets")]
        public static void BuildAll()
        {
            EnsureFolder(BaseArtFolder);

            Mesh mesh = BuildAndSaveMesh();
            Material baseMaterial = BuildBaseMaterial();
            GameObject basePrefab = BuildBasePrefab(mesh, baseMaterial);

            string[] setFolders = Directory.GetDirectories(SetsRoot);
            foreach (string setFolder in setFolders)
            {
                BuildSet(basePrefab, setFolder.Replace('\\', '/'));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CardSetBuilder] Done. Built {setFolders.Length} set(s).");
        }

        private static Mesh BuildAndSaveMesh()
        {
            Mesh generated = CardMeshBuilder.Build(CardMeshSettings.Default);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null)
            {
                // Keep the asset instance so existing references survive a rebuild.
                existing.Clear();
                existing.indexFormat = generated.indexFormat;
                existing.vertices = generated.vertices;
                existing.normals = generated.normals;
                existing.tangents = generated.tangents;
                existing.colors = generated.colors;
                existing.uv = generated.uv;
                existing.uv2 = generated.uv2;
                existing.triangles = generated.triangles;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(generated);
                Debug.Log($"[CardSetBuilder] Updated mesh: {existing.vertexCount} verts, {existing.triangles.Length / 3} tris.");
                return existing;
            }

            AssetDatabase.CreateAsset(generated, MeshPath);
            Debug.Log($"[CardSetBuilder] Created mesh: {generated.vertexCount} verts, {generated.triangles.Length / 3} tris.");
            return generated;
        }

        private static Material BuildBaseMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    $"Shader '{ShaderName}' not found. It may have failed to compile.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, BaseMaterialPath);
            }

            material.shader = shader;
            ApplyMaterialLook(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyMaterialLook(Material material)
        {
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.55f);
            material.SetFloat("_Metallic", 0f);
            material.SetColor("_EdgeTint", Color.white);
            material.SetFloat("_EdgeDarken", 0.18f);
            material.enableInstancing = true;
        }

        private static GameObject BuildBasePrefab(Mesh mesh, Material material)
        {
            CardMeshSettings settings = CardMeshSettings.Default;

            var root = new GameObject("Card_Base");
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(settings.Width, settings.Height, settings.Thickness);
            collider.center = Vector3.zero;

            root.AddComponent<CardIdentity>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[CardSetBuilder] Base prefab written to {BasePrefabPath}");
            return prefab;
        }

        private static void BuildSet(GameObject basePrefab, string setFolder)
        {
            string setId = Path.GetFileName(setFolder);
            string texturesFolder = $"{setFolder}/{SetTexturesSubfolder}";
            string materialFolder = $"{setFolder}/{SetMaterialsSubfolder}";
            string prefabFolder = $"{setFolder}/{SetPrefabsSubfolder}";

            if (!AssetDatabase.IsValidFolder(texturesFolder))
            {
                Debug.LogWarning(
                    $"[CardSetBuilder] Set '{setId}' has no '{SetTexturesSubfolder}' folder. Skipped.");
                return;
            }

            EnsureFolder(materialFolder);
            EnsureFolder(prefabFolder);

            string[] pngPaths = Directory.GetFiles(texturesFolder, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToArray();

            string backPath = pngPaths.FirstOrDefault(
                p => Path.GetFileName(p).StartsWith(BackTexturePrefix));

            if (backPath == null)
            {
                Debug.LogWarning($"[CardSetBuilder] Set '{setId}' has no {BackTexturePrefix}* texture. Skipped.");
                return;
            }

            foreach (string path in pngPaths)
                ConfigureTextureImporter(path);

            var backTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(backPath);
            var fronts = pngPaths.Where(p => p != backPath).ToArray();

            foreach (string frontPath in fronts)
            {
                string fileName = Path.GetFileNameWithoutExtension(frontPath);
                ParseCardName(fileName, out int number, out string displayName);

                var frontTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(frontPath);
                string safeName = $"{setId}_{fileName}";

                Material material = CreateCardMaterial(
                    $"{materialFolder}/M_Card_{safeName}.mat", frontTexture, backTexture);

                CreateCardVariant(
                    basePrefab,
                    $"{prefabFolder}/Card_{safeName}.prefab",
                    material,
                    setId,
                    number,
                    displayName);
            }

            Debug.Log($"[CardSetBuilder] Set '{setId}': {fronts.Length} card variants generated.");
        }

        private static Material CreateCardMaterial(string path, Texture2D front, Texture2D back)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find(ShaderName);
            ApplyMaterialLook(material);
            material.SetTexture("_FrontTex", front);
            material.SetTexture("_BackTex", back);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateCardVariant(
            GameObject basePrefab,
            string path,
            Material material,
            string setId,
            int number,
            string displayName)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = Path.GetFileNameWithoutExtension(path);
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;

            var identity = instance.GetComponent<CardIdentity>();
            var serialized = new SerializedObject(identity);
            serialized.FindProperty("setId").stringValue = setId;
            serialized.FindProperty("number").intValue = number;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Saving a prefab instance under a new path produces a variant of the base.
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        private static void ConfigureTextureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            bool changed = false;

            void Set<T>(ref T field, T value)
            {
                if (!EqualityComparer<T>.Default.Equals(field, value))
                {
                    field = value;
                    changed = true;
                }
            }

            var textureType = importer.textureType;
            var mipmap = importer.mipmapEnabled;
            var wrap = importer.wrapMode;
            var filter = importer.filterMode;
            var aniso = importer.anisoLevel;
            var srgb = importer.sRGBTexture;
            var alphaSource = importer.alphaSource;

            // The art is opaque RGB used on 3D geometry, so: default type, mips for
            // minification (thousands of cards on screen), clamp to stop edge bleeding.
            Set(ref textureType, TextureImporterType.Default);
            Set(ref mipmap, true);
            Set(ref wrap, TextureWrapMode.Clamp);
            Set(ref filter, FilterMode.Trilinear);
            Set(ref aniso, 8);
            Set(ref srgb, true);
            Set(ref alphaSource, TextureImporterAlphaSource.None);

            if (!changed)
                return;

            importer.textureType = textureType;
            importer.mipmapEnabled = mipmap;
            importer.wrapMode = wrap;
            importer.filterMode = filter;
            importer.anisoLevel = aniso;
            importer.sRGBTexture = srgb;
            importer.alphaSource = alphaSource;
            importer.alphaIsTransparency = false;

            importer.SaveAndReimport();
        }

        /// <summary>Turns "03_FlareHeron" into number 3 and display name "Flare Heron".</summary>
        private static void ParseCardName(string fileName, out int number, out string displayName)
        {
            number = 0;
            string remainder = fileName;

            int separator = fileName.IndexOf('_');
            if (separator > 0 && int.TryParse(fileName.Substring(0, separator), out int parsed))
            {
                number = parsed;
                remainder = fileName.Substring(separator + 1);
            }

            displayName = Regex.Replace(remainder, "(?<!^)([A-Z])", " $1");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            Directory.CreateDirectory(assetPath);
            AssetDatabase.Refresh();
        }
    }
}
