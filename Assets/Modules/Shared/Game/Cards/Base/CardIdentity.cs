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

        [Tooltip("The face texture shared by the world material and every flat UI view. The UI " +
                 "gets a lightweight cached Sprite wrapper around this same texture, never a " +
                 "second texture allocation.")]
        [SerializeField] private Texture2D artwork;

        public string SetId => setId;
        public int Number => number;
        public string DisplayName => displayName;
        public Texture2D ArtworkTexture => artwork;
        public Sprite Artwork => CardArtworkSprites.Get(artwork);
    }
}
