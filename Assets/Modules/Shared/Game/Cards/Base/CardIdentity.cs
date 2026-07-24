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

        [Tooltip("The face, as a Sprite, for anywhere the card is drawn flat - the album grid and " +
                 "the hand pile inside it. Same imported texture the material samples in the " +
                 "world; a Sprite-type import gives both, so there is only ever one copy.")]
        [SerializeField] private Sprite artwork;

        public string SetId => setId;
        public int Number => number;
        public string DisplayName => displayName;
        public Sprite Artwork => artwork;
    }
}
