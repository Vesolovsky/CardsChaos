using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Batch-mode probe that enters real play mode in the gameplay scene and reports what
    /// the card system sees at runtime. Survives the play-mode domain reload via SessionState.
    /// </summary>
    public static class CardPlayModeProbe
    {
        private const string ActiveKey = "CardPlayModeProbe.Active";
        private const string FramesKey = "CardPlayModeProbe.Frames";
        private const string ScenePath = "Assets/Modules/Unique/Game/GameplayScene.unity";
        private const string CatalogPath = "Assets/Modules/Shared/Game/Cards/CardCatalog.asset";
        private const int SettleFrames = 180;

        private static string ReportPath => Path.Combine(
            Path.GetTempPath(), "CardPlayModeProbe_report.txt");

        public static void Run()
        {
            File.WriteAllText(ReportPath, "probe started\n");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FramesKey, 0);

            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(ActiveKey, false))
                return;

            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;

            int frames = SessionState.GetInt(FramesKey, 0) + 1;
            SessionState.SetInt(FramesKey, frames);
            if (frames < SettleFrames)
                return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);

            var report = new System.Text.StringBuilder();
            report.AppendLine("========== PLAY MODE PROBE ==========");

            var catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
            {
                report.AppendLine("catalog asset: NULL");
            }
            else
            {
                var setsField = typeof(CardCatalog).GetField(
                    "sets",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sets = (System.Collections.Generic.List<CardSetDefinition>)setsField.GetValue(catalog);

                report.AppendLine($"catalog live sets count: {sets.Count}");
                for (int i = 0; i < sets.Count; i++)
                {
                    report.AppendLine(sets[i] == null
                        ? $"  set[{i}]: NULL"
                        : $"  set[{i}] '{sets[i].SetId}': cards={sets[i].Cards.Count}");
                }

                report.AppendLine($"ICardCatalog.Cards.Count: {((ICardCatalog)catalog).Cards.Count}");
            }

            var spawner = Object.FindObjectOfType<CardSpawner>();
            report.AppendLine($"spawner in scene: {(spawner != null ? spawner.name : "NULL")}");

            Card[] spawnedCards = Object.FindObjectsOfType<Card>();
            report.AppendLine($"Card instances in scene: {spawnedCards.Length}");

            bool success = spawnedCards.Length > 0;
            report.AppendLine(success ? "RESULT: CARDS SPAWNED" : "RESULT: NO CARDS");
            report.AppendLine("=====================================");

            File.AppendAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());

            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
