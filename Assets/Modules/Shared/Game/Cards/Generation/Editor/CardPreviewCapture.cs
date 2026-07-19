using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Renders verification shots of a generated card prefab to disk. Used to confirm
    /// triangle winding, corner shape and that no artwork letterboxing bleeds onto the mesh.
    /// </summary>
    public static class CardPreviewCapture
    {
        private const string CardPrefabPath =
            "Assets/Modules/Shared/Game/Cards/Sets/BirdsOfTheSun/Prefabs/Card_BirdsOfTheSun_03_FlareHeron.prefab";

        public static void CaptureFromCommandLine()
        {
            string outputDir = GetArgument("-previewOut") ?? Path.Combine(Path.GetTempPath(), "CardPreview");
            Capture(outputDir);
        }

        [MenuItem("Tools/Cards/Capture Preview Shots")]
        public static void CaptureToTemp()
        {
            // Capturing replaces the open scene, so never let it discard unsaved work.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Capture(Path.Combine(Path.GetTempPath(), "CardPreview"));
        }

        private static void Capture(string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CardPreviewCapture] Prefab not found at {CardPrefabPath}");
                return;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.4f);

            var lightObject = new GameObject("Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            card.transform.position = Vector3.zero;

            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.0001f;
            camera.farClipPlane = 10f;

            // Front: the face normal is +Z, so look back down the Z axis.
            card.transform.rotation = Quaternion.identity;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, 0.2f), Quaternion.Euler(0f, 180f, 0f));
            camera.orthographicSize = 0.052f;
            Render(camera, 512, 768, Path.Combine(outputDir, "01_front.png"));

            // Back.
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -0.2f), Quaternion.identity);
            Render(camera, 512, 768, Path.Combine(outputDir, "02_back.png"));

            // Corner close-up: tight crop on the top-left corner of the front face.
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-0.0295f, 0.0455f, 0.2f), Quaternion.Euler(0f, 180f, 0f));
            camera.orthographicSize = 0.006f;
            Render(camera, 640, 640, Path.Combine(outputDir, "03_corner_closeup.png"));

            // Raking view of the rim so the bevel and thickness are visible.
            card.transform.rotation = Quaternion.Euler(0f, 68f, 0f);
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, 0.2f), Quaternion.Euler(0f, 180f, 0f));
            camera.orthographicSize = 0.05f;
            Render(camera, 640, 768, Path.Combine(outputDir, "04_rim_angle.png"));

            // Three quarter beauty shot.
            card.transform.rotation = Quaternion.Euler(-22f, 26f, 6f);
            camera.orthographicSize = 0.055f;
            Render(camera, 640, 768, Path.Combine(outputDir, "05_three_quarter.png"));

            Debug.Log($"[CardPreviewCapture] Shots written to {outputDir}");
        }

        private static void Render(Camera camera, int width, int height, string path)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 8,
            };

            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            File.WriteAllBytes(path, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }

        private static string GetArgument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }
    }
}
