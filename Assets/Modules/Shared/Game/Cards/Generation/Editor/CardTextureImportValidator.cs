using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Verifies both the requested importer settings and, for one representative card, Unity's
    /// actual imported payload. A requested BC7 format is not enough: unsupported combinations
    /// can silently fall back to an uncompressed texture.
    /// </summary>
    public static class CardTextureImportValidator
    {
        private const string CardsRoot = "Assets/Modules/Shared/Game/Cards/Sets";
        private const string DefaultRepresentativePath =
            CardsRoot + "/AnimalsInTheRain/Art/Sprites/1_PuddleBunny.png";

        [MenuItem("Tools/Cards/Validate All Card Texture Import Settings")]
        public static void ValidateAllCardTextureImportSettings()
        {
            string[] paths = Directory.GetFiles(CardsRoot, "*.png", SearchOption.AllDirectories);
            int validated = 0;

            foreach (string rawPath in paths)
            {
                string path = rawPath.Replace('\\', '/');
                if (!path.Contains("/Art/Sprites/"))
                    continue;

                ValidateImporter(path);
                validated++;
            }

            if (validated == 0)
                throw new InvalidOperationException("No card artwork textures were found.");

            Debug.Log(
                $"[CardTextureImportValidator] {validated} card artwork importers are configured " +
                "as streamed Default/ToLarger/BC7 textures.");
        }

        [MenuItem("Tools/Cards/Validate Representative Imported Card Texture")]
        public static void ValidateRepresentativeImportedCardTexture()
        {
            ValidateAllCardTextureImportSettings();

            string path = GetCommandLineValue("-cardTexturePath") ?? DefaultRepresentativePath;
            ValidateImporter(path);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Sprite spriteSubAsset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var failures = new List<string>();

            if (texture == null)
            {
                failures.Add("the Texture2D asset is missing");
            }
            else
            {
                if (!Mathf.IsPowerOfTwo(texture.width) || !Mathf.IsPowerOfTwo(texture.height))
                    failures.Add($"actual size is not POT ({texture.width}x{texture.height})");

                if (texture.mipmapCount <= 1)
                    failures.Add($"actual mip count is {texture.mipmapCount}");

                if (!texture.streamingMipmaps)
                    failures.Add("the imported texture is not streamable");

                BuildTargetGroup activeGroup =
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

                if (activeGroup == BuildTargetGroup.Standalone && texture.format != TextureFormat.BC7)
                {
                    failures.Add(
                        $"actual Standalone format is {texture.format} ({(int)texture.format}), not BC7");
                }
            }

            if (spriteSubAsset != null)
                failures.Add("a serialized Sprite sub-asset still exists");

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Card texture '{path}' failed imported-payload validation: " +
                    string.Join("; ", failures));
            }

            Debug.Log(
                $"[CardTextureImportValidator] Imported payload valid: path={path}; " +
                $"format={texture.format}({(int)texture.format}); size={texture.width}x{texture.height}; " +
                $"mips={texture.mipmapCount}; streaming={texture.streamingMipmaps}; Sprite=<none>.",
                texture);
        }

        private static void ValidateImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter exists for '{path}'.");

            TextureImporterPlatformSettings standalone =
                importer.GetPlatformTextureSettings("Standalone");

            var failures = new List<string>();

            if (importer.textureType != TextureImporterType.Default)
                failures.Add($"textureType={importer.textureType}");
            if (importer.npotScale != TextureImporterNPOTScale.ToLarger)
                failures.Add($"npotScale={importer.npotScale}");
            if (!importer.mipmapEnabled)
                failures.Add("mipmaps disabled");
            if (!importer.streamingMipmaps)
                failures.Add("mip streaming disabled");
            if (importer.isReadable)
                failures.Add("Read/Write enabled");
            if (importer.alphaSource != TextureImporterAlphaSource.None)
                failures.Add($"alphaSource={importer.alphaSource}");
            if (importer.wrapMode != TextureWrapMode.Clamp)
                failures.Add($"wrapMode={importer.wrapMode}");
            if (importer.filterMode != FilterMode.Trilinear)
                failures.Add($"filterMode={importer.filterMode}");
            if (!standalone.overridden)
                failures.Add("Standalone override disabled");
            if (standalone.maxTextureSize != 2048)
                failures.Add($"Standalone maxTextureSize={standalone.maxTextureSize}");
            if (standalone.resizeAlgorithm != TextureResizeAlgorithm.Mitchell)
                failures.Add($"Standalone resizeAlgorithm={standalone.resizeAlgorithm}");
            if (standalone.format != TextureImporterFormat.BC7)
                failures.Add($"Standalone format={standalone.format}");
            if (standalone.ignorePlatformSupport)
                failures.Add("Standalone ignorePlatformSupport enabled");

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Card texture importer '{path}' is invalid: " + string.Join("; ", failures));
            }
        }

        private static string GetCommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }

            return null;
        }
    }
}
