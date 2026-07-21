using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards
{
    public interface ICardCatalog
    {
        IReadOnlyList<Card> Cards { get; }
        Card GetRandom();
    }

    [CreateAssetMenu(menuName = "CardsChaos/Card Catalog", fileName = "CardCatalog")]
    public class CardCatalog : ScriptableObject, ICardCatalog
    {
        [SerializeField] private List<CardSetDefinition> sets = new List<CardSetDefinition>();

        // NonSerialized is load-bearing: Unity serializes private serializable fields
        // across domain reloads (including entering play mode), so without it a cached
        // list computed in edit mode leaks into every play session.
        [System.NonSerialized] private List<Card> _cards;

        public IReadOnlyList<Card> Cards => _cards ??= Flatten();

        public Card GetRandom()
        {
            IReadOnlyList<Card> cards = Cards;
            return cards.Count == 0 ? null : cards[Random.Range(0, cards.Count)];
        }

        private void OnDisable() => _cards = null;

#if UNITY_EDITOR
        /// <summary>Temporary deep-state dump for the empty-catalog investigation.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"[CardCatalog] '{name}' instanceID={GetInstanceID()} " +
                $"assetPath='{UnityEditor.AssetDatabase.GetAssetPath(this)}' sets={sets.Count}");

            for (int i = 0; i < sets.Count; i++)
            {
                if (sets[i] == null)
                {
                    sb.AppendLine($"  set[{i}]: NULL");
                    continue;
                }

                int nulls = 0;
                foreach (GameObject prefab in sets[i].Cards)
                {
                    if (prefab == null)
                        nulls++;
                }

                sb.AppendLine(
                    $"  set[{i}] '{sets[i].SetId}' cards={sets[i].Cards.Count} nulls={nulls} " +
                    $"path='{UnityEditor.AssetDatabase.GetAssetPath(sets[i])}'");
            }

            return sb.ToString();
        }
#endif

        private List<Card> Flatten()
        {
            var result = new List<Card>();

            foreach (CardSetDefinition set in sets)
            {
                if (set == null)
                    continue;

                foreach (GameObject prefab in set.Cards)
                {
                    if (prefab == null)
                    {
                        Debug.LogError($"[CardCatalog] Set '{set.SetId}' contains a broken card reference.", set);
                        continue;
                    }

                    if (prefab.TryGetComponent(out Card card))
                        result.Add(card);
                    else
                        Debug.LogError($"[CardCatalog] '{prefab.name}' in set '{set.SetId}' has no Card component.", prefab);
                }
            }

            return result;
        }
    }
}
