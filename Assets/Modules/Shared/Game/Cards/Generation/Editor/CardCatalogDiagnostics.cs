using System.Text;
using UnityEditor;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Batch-mode diagnostics for the "catalog contains 0 cards" investigation.
    /// Prints the state of every link in the chain: catalog -> sets -> prefabs -> Card.
    /// </summary>
    public static class CardCatalogDiagnostics
    {
        private const string CatalogPath = "Assets/Modules/Shared/Game/Cards/CardCatalog.asset";

        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine("========== CARD CATALOG DIAGNOSTICS ==========");

            var catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
            {
                report.AppendLine($"FATAL: no CardCatalog at {CatalogPath}");
                Finish(report, success: false);
                return;
            }

            // Read the actual deserialized field, bypassing SerializedObject entirely.
            var setsField = typeof(CardCatalog).GetField(
                "sets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var sets = (System.Collections.Generic.List<CardSetDefinition>)setsField.GetValue(catalog);

            report.AppendLine($"catalog instance: '{catalog.name}', deserialized sets field count: {sets.Count}");

            int totalRefs = 0;
            int nullRefs = 0;
            int noComponent = 0;

            for (int i = 0; i < sets.Count; i++)
            {
                CardSetDefinition set = sets[i];
                if (set == null)
                {
                    report.AppendLine($"  set[{i}]: NULL after deserialization");
                    continue;
                }

                int setNull = 0;
                int setNoCard = 0;
                foreach (GameObject prefab in set.Cards)
                {
                    totalRefs++;
                    if (prefab == null)
                    {
                        setNull++;
                        nullRefs++;
                    }
                    else if (!prefab.TryGetComponent<Card>(out _))
                    {
                        setNoCard++;
                        noComponent++;
                    }
                }

                report.AppendLine(
                    $"  set[{i}] '{set.SetId}': entries={set.Cards.Count} null={setNull} noCard={setNoCard}");
            }

            report.AppendLine($"TOTAL: refs={totalRefs} null={nullRefs} noCardComponent={noComponent}");

            // The exact runtime path the spawner uses.
            report.AppendLine($"ICardCatalog.Cards.Count (runtime path): {((ICardCatalog)catalog).Cards.Count}");
            Card random = catalog.GetRandom();
            report.AppendLine($"GetRandom(): {(random != null ? random.name : "NULL")}");

            // Factory path: instantiate one card like CardFactory does.
            if (random != null)
            {
                Card instance = Object.Instantiate(random, Vector3.zero, Quaternion.identity);
                report.AppendLine($"Instantiate check: {(instance != null ? "OK -> " + instance.name : "FAILED")}");
                if (instance != null)
                    Object.DestroyImmediate(instance.gameObject);
            }

            bool healthy = totalRefs > 0 && nullRefs == 0 && noComponent == 0 && random != null;
            Finish(report, healthy);
        }

        private static void Finish(StringBuilder report, bool success)
        {
            report.AppendLine(success ? "RESULT: HEALTHY" : "RESULT: BROKEN");
            report.AppendLine("==============================================");
            Debug.Log(report.ToString());
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
