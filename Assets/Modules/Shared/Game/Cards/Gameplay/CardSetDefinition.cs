using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards
{
    [CreateAssetMenu(menuName = "CardsChaos/Card Set", fileName = "CardSet")]
    public class CardSetDefinition : ScriptableObject
    {
        [SerializeField] private string setId;
        [SerializeField] private List<GameObject> cards = new List<GameObject>();

        public string SetId => setId;
        public IReadOnlyList<GameObject> Cards => cards;
    }
}
