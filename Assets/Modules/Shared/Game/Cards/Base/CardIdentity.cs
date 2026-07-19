using UnityEngine;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Identifies which card a prefab variant represents. Set by the card set builder.
    /// </summary>
    public sealed class CardIdentity : MonoBehaviour
    {
        [SerializeField] private string setId;
        [SerializeField] private int number;
        [SerializeField] private string displayName;

        public string SetId => setId;
        public int Number => number;
        public string DisplayName => displayName;
    }
}
