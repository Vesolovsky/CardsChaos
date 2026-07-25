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
        private const string UIShaderName = "CardsChaos/Card UI";

        private const string MeshPath = BaseArtFolder + "/CardMesh.asset";
        private const string BaseMaterialPath = BaseArtFolder + "/M_Card_Base.mat";
        private const string UIMaterialPath = BaseArtFolder + "/M_Card_UI.mat";
        private const string BasePrefabPath = BaseFolder + "/Card_Base.prefab";
        private const string CatalogPath = CardsRoot + "/CardCatalog.asset";

        /// <summary>
        /// PhysX only starts generating contacts once surfaces are this close, so the value
        /// doubles as the gap resting cards keep from each other. The previous 0.2 mm was
        /// tighter than the 0.5 mm project default and left the solver almost no band to
        /// work in, which is how cards ended up sinking through one another. At 0.8 mm the
        /// separation is still invisible on a 63 mm card.
        /// </summary>
        private const float CardContactOffset = 0.0008f;

        /// <summary>
        /// The collider is a touch thicker than the plate so a resting card floats a few
        /// tenths of a millimetre above the table instead of sitting coplanar with it. That
        /// keeps the flat face from z-fighting the floor, and gives stacked or overlapping
        /// cards a guaranteed gap between their faces (min separation = this value).
        /// </summary>
        private const float CardColliderClearance = 0.001f;

        // Inspect-time material response, written per card variant at build time.
        //
        // A dark face carries a strong metallic sheen well, but the same numbers on pale
        // artwork blow the specular out to white as the card turns in the inspector. So the
        // values are dialled back as the measured face gets brighter, between the two
        // luminance bounds below - outside them the response is flat.
        private const float DarkFaceMetallic = 0.845f;
        private const float BrightFaceMetallic = 0.12f;
        private const float DarkFaceSmoothness = 0.5f;
        private const float BrightFaceSmoothness = 0.3f;

        private const float LuminanceKnee = 0.35f;
        private const float LuminanceCeiling = 0.8f;

        // Layout expected inside each Sets/<SetId> folder.
        private const string SetTexturesSubfolder = "Art/Sprites";
        private const string SetMaterialsSubfolder = "Art/Materials";
        private const string SetPrefabsSubfolder = "Prefabs";

        private const string BackTexturePrefix = "Revers_";

        // Set icons live loose in the set folder, beside the definition asset.
        private const string IconSuffix = "_Icon.png";
        private const string IconInnerShadowSuffix = "_IconInnerShadow.png";

        [MenuItem("Tools/Cards/Build All Card Sets")]
        public static void BuildAll() => Build(rebuildExisting: true);

        /// <summary>
        /// Only touches sets that have no generated definition yet. Everything already built
        /// (mesh, base prefab, existing sets) is reused as is, which is why this is the fast path.
        /// </summary>
        [MenuItem("Tools/Cards/Build New Card Sets Only")]
        public static void BuildNewOnly() => Build(rebuildExisting: false);

        private static void Build(bool rebuildExisting)
        {
            EnsureFolder(BaseArtFolder);

            GameObject basePrefab = rebuildExisting
                ? BuildBasePrefab(BuildAndSaveMesh(), BuildBaseMaterial())
                : LoadOrBuildBasePrefab();

            // Cheap, and it has to run on both paths: the flat card's silhouette is derived from
            // the same measurements as the mesh, and the two drifting apart is exactly the bug
            // this generation step exists to prevent.
            BuildUIMaterial();

            var setDefinitions = new List<CardSetDefinition>();
            int built = 0;
            int reused = 0;

            string[] setFolders = Directory.GetDirectories(SetsRoot);
            foreach (string folder in setFolders)
            {
                string setFolder = folder.Replace('\\', '/');

                if (!rebuildExisting)
                {
                    CardSetDefinition existing = LoadGeneratedSet(setFolder);
                    if (existing != null)
                    {
                        setDefinitions.Add(existing);
                        reused++;
                        continue;
                    }
                }

                CardSetDefinition definition = BuildSet(basePrefab, setFolder);
                if (definition != null)
                {
                    setDefinitions.Add(definition);
                    built++;
                }
            }

            BuildCatalog(setDefinitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(rebuildExisting
                ? $"[CardSetBuilder] Done. Built {built} set(s)."
                : $"[CardSetBuilder] Done. Built {built} new set(s), reused {reused} existing.");
        }

        /// <summary>
        /// The generated definition for a set, or null when the set still needs building.
        /// An empty definition counts as not built - it is the leftover of a failed run.
        /// </summary>
        private static CardSetDefinition LoadGeneratedSet(string setFolder)
        {
            string setId = Path.GetFileName(setFolder);
            var definition = AssetDatabase.LoadAssetAtPath<CardSetDefinition>($"{setFolder}/{setId}.asset");

            if (definition == null)
                return null;

            return definition.Cards.Count > 0 ? definition : null;
        }

        private static GameObject LoadOrBuildBasePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (prefab != null)
                return prefab;

            Debug.Log("[CardSetBuilder] No base prefab yet - building it before the new sets.");
            return BuildBasePrefab(BuildAndSaveMesh(), BuildBaseMaterial());
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

        /// <summary>
        /// The one material every flat card is drawn with - in a slot, on the pile, and in
        /// flight between them.
        ///
        /// Its corner is written from <see cref="CardMeshSettings"/>, the same numbers the mesh
        /// is cut from, so the card has one silhouette rather than two that happen to look alike
        /// until someone retunes the measurement. Sharing a single material also keeps every
        /// card in one batch.
        /// </summary>
        private static Material BuildUIMaterial()
        {
            Shader shader = Shader.Find(UIShaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    $"Shader '{UIShaderName}' not found. It may have failed to compile.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(UIMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, UIMaterialPath);
            }

            material.shader = shader;

            CardMeshSettings settings = CardMeshSettings.Default;

            // Radius as a fraction of the width, because that is the form it was measured in -
            // 48.5 px across a 1024 px face.
            material.SetFloat("_CornerRadius", settings.CornerRadius / settings.Width);
            material.SetFloat("_Squareness", settings.Squareness);
            material.SetFloat("_Aspect", settings.Width / settings.Height);
            material.SetVector("_UvInset", new Vector4(
                settings.UvInsetPixels / settings.TextureSize.x,
                settings.UvInsetPixels / settings.TextureSize.y,
                0f,
                0f));

            EditorUtility.SetDirty(material);
            Debug.Log($"[CardSetBuilder] UI card material written to {UIMaterialPath}");

            return material;
        }

        private static void ApplyMaterialLook(Material material)
        {
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.55f);
            material.SetFloat("_Metallic", 0f);
            material.SetColor("_EdgeTint", Color.white);
            material.SetFloat("_EdgeDarken", 0.18f);
            // The hover/selected outline is driven per card from a property block, so the
            // material has to keep it switched off. Left unset the value is whatever the
            // asset happened to be saved with - M_Card_Base was stuck at the 0.01 maximum,
            // which put a permanent white shell on anything using it.
            material.SetFloat("_OutlineWidth", 0f);
            material.SetColor("_OutlineColor", Color.white);
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
            collider.size = new Vector3(settings.Width, settings.Height, settings.Thickness + CardColliderClearance);
            collider.center = Vector3.zero;
            collider.contactOffset = CardContactOffset;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.005f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Speculative contacts were letting cards pass through each other; sweeping
            // against the other dynamic cards is what actually stops it, at the cost of a
            // more expensive broadphase.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // A pile of thin plates is the worst case for a stacking solver - the 6/1
            // project defaults let it settle through itself.
            body.solverIterations = 16;
            body.solverVelocityIterations = 4;
            // Push overlaps apart gently rather than firing the card out of the pile.
            body.maxDepenetrationVelocity = 1f;

            root.AddComponent<CardIdentity>();
            root.AddComponent<Card>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[CardSetBuilder] Base prefab written to {BasePrefabPath}");
            return prefab;
        }

        private static CardSetDefinition BuildSet(GameObject basePrefab, string setFolder)
        {
            string setId = Path.GetFileName(setFolder);
            string texturesFolder = $"{setFolder}/{SetTexturesSubfolder}";
            string materialFolder = $"{setFolder}/{SetMaterialsSubfolder}";
            string prefabFolder = $"{setFolder}/{SetPrefabsSubfolder}";

            if (!AssetDatabase.IsValidFolder(texturesFolder))
            {
                Debug.LogWarning(
                    $"[CardSetBuilder] Set '{setId}' has no '{SetTexturesSubfolder}' folder. Skipped.");
                return null;
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
                return null;
            }

            foreach (string path in pngPaths)
                ConfigureTextureImporter(path);

            var backTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(backPath);

            // Already a sprite - the back is one of the pngs the import pass above ran over - so
            // this only picks up the sub-asset the album's inspect flips a card over to.
            var backSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backPath);
            if (backSprite == null)
            {
                Debug.LogError(
                    $"[CardSetBuilder] '{backPath}' produced no Sprite. Cards in this set cannot " +
                    "be turned over in the album's inspect.", backTexture);
            }

            var fronts = pngPaths.Where(p => p != backPath).ToArray();
            var cards = new List<(int Number, GameObject Prefab)>(fronts.Length);
            float minLuminance = 1f;
            float maxLuminance = 0f;
            float totalLuminance = 0f;

            foreach (string frontPath in fronts)
            {
                string fileName = Path.GetFileNameWithoutExtension(frontPath);
                ParseCardName(fileName, out int number, out string displayName);

                var frontTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(frontPath);

                // Loaded after the import pass above has run, so the sub-asset is guaranteed to
                // exist even on the run that first flips a set over to sprites.
                var frontSprite = AssetDatabase.LoadAssetAtPath<Sprite>(frontPath);
                if (frontSprite == null)
                {
                    Debug.LogError(
                        $"[CardSetBuilder] '{frontPath}' produced no Sprite. The album will have " +
                        "nothing to draw for this card.", frontTexture);
                }

                string safeName = $"{setId}_{fileName}";

                Material material = CreateCardMaterial(
                    $"{materialFolder}/M_Card_{safeName}.mat", frontTexture, backTexture);

                float luminance = MeasureFaceLuminance(frontPath);
                minLuminance = Mathf.Min(minLuminance, luminance);
                maxLuminance = Mathf.Max(maxLuminance, luminance);
                totalLuminance += luminance;

                cards.Add((number, CreateCardVariant(
                    basePrefab,
                    $"{prefabFolder}/Card_{safeName}.prefab",
                    material,
                    frontSprite,
                    setId,
                    number,
                    displayName,
                    luminance)));
            }

            // Sorted by number rather than by filename, which orders 10 before 2. Nothing reads
            // the list positionally, but an asset whose inspector runs 1, 10, 11, 2 is a trap
            // for anyone checking a set by eye.
            cards.Sort((left, right) => left.Number.CompareTo(right.Number));
            ValidateNumbering(setId, cards);

            // The spread is worth reading back: it is what the luminance bounds are tuned
            // against, and a set that lands entirely on one side of the knee gets no
            // variation at all.
            string luminanceReport = cards.Count > 0
                ? $" Face luminance {minLuminance:F2}-{maxLuminance:F2}, mean {totalLuminance / cards.Count:F2}."
                : string.Empty;

            Debug.Log($"[CardSetBuilder] Set '{setId}': {cards.Count} card variants generated.{luminanceReport}");

            return CreateSetDefinition(
                $"{setFolder}/{setId}.asset",
                setId,
                cards.Select(card => card.Prefab).ToList(),
                LoadSetIcon(setFolder, setId, IconSuffix),
                LoadSetIcon(setFolder, setId, IconInnerShadowSuffix),
                backSprite);
        }

        /// <summary>
        /// The album lays a set out as slots 1..N and puts card number K in slot K, so a gap or
        /// an out-of-range number leaves a slot that no card can ever fill. Cheaper to catch here,
        /// against the filenames, than to work out from a permanently empty square in the album.
        /// </summary>
        private static void ValidateNumbering(string setId, List<(int Number, GameObject Prefab)> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                int expected = i + 1;
                if (cards[i].Number == expected)
                    continue;

                Debug.LogError(
                    $"[CardSetBuilder] Set '{setId}' is not numbered 1..{cards.Count}: expected " +
                    $"{expected} but found {cards[i].Number} ('{cards[i].Prefab.name}'). Rename the " +
                    "source PNGs so the numbers run without gaps or duplicates.", cards[i].Prefab);

                return;
            }
        }

        /// <summary>
        /// Found by suffix rather than by building "{setId}_Icon.png", because the two do not
        /// always agree - the MagicalButerrflies folder carries MagicalButterflies icons, and a
        /// name-built path would silently come back empty.
        /// </summary>
        private static Sprite LoadSetIcon(string setFolder, string setId, string suffix)
        {
            string[] matches = Directory.GetFiles(setFolder, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .Where(p => p.EndsWith(suffix, System.StringComparison.Ordinal))
                .OrderBy(p => p)
                .ToArray();

            if (matches.Length == 0)
            {
                Debug.LogWarning(
                    $"[CardSetBuilder] Set '{setId}' has no '*{suffix}' next to its definition. " +
                    "Its album button and empty card slots will show no icon.");

                return null;
            }

            if (matches.Length > 1)
            {
                Debug.LogWarning(
                    $"[CardSetBuilder] Set '{setId}' has {matches.Length} files ending in " +
                    $"'{suffix}'. Using '{Path.GetFileName(matches[0])}'.");
            }

            ConfigureIconImporter(matches[0]);
            return AssetDatabase.LoadAssetAtPath<Sprite>(matches[0]);
        }

        private static CardSetDefinition CreateSetDefinition(
            string path, string setId, List<GameObject> cards, Sprite icon, Sprite iconInnerShadow,
            Sprite backArtwork)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CardSetDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CardSetDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("setId").stringValue = setId;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("iconInnerShadow").objectReferenceValue = iconInnerShadow;
            serialized.FindProperty("backArtwork").objectReferenceValue = backArtwork;

            // setName is deliberately untouched. It is the one field a human is expected to
            // correct - the generated name cannot know that Ballon'dOrs wants to read
            // "Ballon d'Or" - so it is owned by Tools/Cards/Build Set Names, which only ever
            // writes it on request. See CardSetNameBuilder.
            SerializedProperty cardsProperty = serialized.FindProperty("cards");
            cardsProperty.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++)
                cardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void BuildCatalog(List<CardSetDefinition> setDefinitions)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty setsProperty = serialized.FindProperty("sets");
            setsProperty.arraySize = setDefinitions.Count;
            for (int i = 0; i < setDefinitions.Count; i++)
                setsProperty.GetArrayElementAtIndex(i).objectReferenceValue = setDefinitions[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static GameObject CreateCardVariant(
            GameObject basePrefab,
            string path,
            Material material,
            Sprite artwork,
            string setId,
            int number,
            string displayName,
            float faceLuminance)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = Path.GetFileNameWithoutExtension(path);
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;

            ApplyInspectLook(instance, faceLuminance);

            var identity = instance.GetComponent<CardIdentity>();
            var serialized = new SerializedObject(identity);
            serialized.FindProperty("setId").stringValue = setId;
            serialized.FindProperty("number").intValue = number;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("artwork").objectReferenceValue = artwork;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Saving a prefab instance under a new path produces a variant of the base.
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            // Reload through the asset database so the stored reference points at the
            // imported artifact. Referencing the prefab root rather than a component is
            // deliberate: component references into prefab variants do not survive
            // deserialization at runtime.
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// Card faces are imported as sprites, and that is the whole trick behind the album not
        /// needing a second copy of the art: a Sprite-type import still produces the Texture2D
        /// the card material samples in the world, with the Sprite hanging off it as a sub-asset
        /// for the UI. One file, one texture in memory, both uses served.
        ///
        /// Everything the 3D side needs is kept alongside it - mips for the thousands of cards on
        /// the floor, clamped wrapping, anisotropy - none of which a sprite import forbids.
        /// </summary>
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
            var spriteMode = importer.spriteImportMode;
            var mipmap = importer.mipmapEnabled;
            var wrap = importer.wrapMode;
            var filter = importer.filterMode;
            var aniso = importer.anisoLevel;
            var srgb = importer.sRGBTexture;
            var alphaSource = importer.alphaSource;
            var npot = importer.npotScale;

            Set(ref textureType, TextureImporterType.Sprite);
            Set(ref spriteMode, SpriteImportMode.Single);
            Set(ref mipmap, true);
            Set(ref wrap, TextureWrapMode.Clamp);
            Set(ref filter, FilterMode.Trilinear);
            Set(ref aniso, 8);
            Set(ref srgb, true);
            // The faces are opaque, so there is no alpha worth storing - and dropping it keeps
            // the tight-mesh question below moot as well.
            Set(ref alphaSource, TextureImporterAlphaSource.None);
            // A sprite import would otherwise be free to rescale a non-power-of-two face, which
            // on a 1024x1536 card means resampling the artwork for nothing.
            Set(ref npot, TextureImporterNPOTScale.None);

            // Tight is the sprite default and would hand the UI a mesh cut to the alpha shape.
            // On an opaque card that is the full rect anyway, so all it buys is a slower import
            // and a silhouette that changes if the art ever gains a transparent corner.
            var spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);

            if (spriteSettings.spriteMeshType != SpriteMeshType.FullRect)
            {
                spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(spriteSettings);
                changed = true;
            }

            if (!changed)
                return;

            importer.textureType = textureType;
            importer.spriteImportMode = spriteMode;
            importer.mipmapEnabled = mipmap;
            importer.wrapMode = wrap;
            importer.filterMode = filter;
            importer.anisoLevel = aniso;
            importer.sRGBTexture = srgb;
            importer.alphaSource = alphaSource;
            importer.npotScale = npot;
            importer.alphaIsTransparency = false;

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Set icons are already authored as sprites with a transparent surround, so this only
        /// insists on the parts the album depends on and leaves the alpha exactly as drawn -
        /// running them through <see cref="ConfigureTextureImporter"/> would flatten it away.
        /// </summary>
        private static void ConfigureIconImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Mean perceived brightness of a card face, 0..1.
        ///
        /// The PNG is decoded straight off disk rather than through the imported texture:
        /// the importer deliberately leaves Read/Write off, and flipping it on per card
        /// would cost two extra reimports each. Luma is taken on the stored sRGB values,
        /// because "this card looks bright" is a perceptual statement, not a linear one.
        /// </summary>
        private static float MeasureFaceLuminance(string assetPath)
        {
            const float fallback = 0.5f;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(assetPath)))
                {
                    Debug.LogWarning($"[CardSetBuilder] Could not decode '{assetPath}'. Treating it as mid grey.");
                    return fallback;
                }

                Color32[] pixels = texture.GetPixels32();
                if (pixels.Length == 0)
                    return fallback;

                // Every 16th pixel is far more than enough to average a 1024x1536 face.
                const int stride = 16;

                double sum = 0.0;
                int count = 0;

                for (int i = 0; i < pixels.Length; i += stride)
                {
                    Color32 pixel = pixels[i];
                    sum += (0.2126 * pixel.r + 0.7152 * pixel.g + 0.0722 * pixel.b) / 255.0;
                    count++;
                }

                return count > 0 ? (float)(sum / count) : fallback;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Writes the brightness compensated inspect look onto a card variant.</summary>
        private static void ApplyInspectLook(GameObject instance, float luminance)
        {
            var card = instance.GetComponent<Card>();
            if (card == null)
                return;

            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LuminanceKnee, LuminanceCeiling, luminance));

            var serialized = new SerializedObject(card);
            serialized.FindProperty("inspectMetallic").floatValue =
                Mathf.Lerp(DarkFaceMetallic, BrightFaceMetallic, t);
            serialized.FindProperty("inspectSmoothness").floatValue =
                Mathf.Lerp(DarkFaceSmoothness, BrightFaceSmoothness, t);

            // Kept raw as well as baked into the two values above: the close-up lamps have to
            // make the same call about how bright this face is, and re-deriving it from a
            // smoothness that has already been clamped at both ends would not get it back.
            serialized.FindProperty("faceLuminance").floatValue = luminance;

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
