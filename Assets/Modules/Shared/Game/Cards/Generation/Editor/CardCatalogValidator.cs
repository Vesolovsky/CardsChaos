using UnityEditor;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    public static class CardCatalogValidator
    {
        private const string CatalogPath = "Assets/Modules/Shared/Game/Cards/CardCatalog.asset";

        [MenuItem("Tools/Cards/Validate Catalog")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[CardCatalogValidator] No catalog at {CatalogPath}");
                return;
            }

            int total = 0;
            int resolved = 0;

            using (var serialized = new SerializedObject(catalog))
            {
                SerializedProperty sets = serialized.FindProperty("sets");
                for (int s = 0; s < sets.arraySize; s++)
                {
                    var set = sets.GetArrayElementAtIndex(s).objectReferenceValue as CardSetDefinition;
                    if (set == null)
                    {
                        Debug.LogError($"[CardCatalogValidator] Set {s} reference is broken.");
                        continue;
                    }

                    int setResolved = 0;
                    foreach (GameObject prefab in set.Cards)
                    {
                        total++;
                        if (prefab != null && prefab.TryGetComponent<Card>(out _))
                        {
                            setResolved++;
                            resolved++;
                        }
                    }

                    string status = setResolved == set.Cards.Count ? "OK" : "BROKEN";
                    Debug.Log($"[CardCatalogValidator] {set.SetId}: {setResolved}/{set.Cards.Count} {status}");
                }
            }

            Debug.Log($"[CardCatalogValidator] Total: {resolved}/{total} card references resolve.");
        }
    }
}
