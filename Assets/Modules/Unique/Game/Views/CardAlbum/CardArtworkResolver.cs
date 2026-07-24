using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// Turns a card's name back into its face.
    ///
    /// The album stores what it is holding as set-and-number, because that is what survives being
    /// written to disk. Everything that has to draw one of those cards needs the same two-step
    /// lookup back through the catalog, so it lives here once.
    /// </summary>
    public class CardArtworkResolver
    {
        private readonly ICardCatalog _catalog;

        public CardArtworkResolver(ICardCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>The card's face, or null when the save names a card the catalog has lost.</summary>
        public Sprite Resolve(CardRef card)
        {
            return Find(card, out CardIdentity identity) ? identity.Artwork : null;
        }

        /// <summary>
        /// The prefab of the card, for putting one back out into the world when the player takes
        /// it out of the album. Null when nothing matches.
        /// </summary>
        public Card ResolvePrefab(CardRef card)
        {
            if (!Find(card, out CardIdentity identity))
                return null;

            if (identity.TryGetComponent(out Card prefab))
                return prefab;

            Debug.LogError($"[{nameof(CardArtworkResolver)}] {card} has no {nameof(Card)} component.", identity);
            return null;
        }

        private bool Find(CardRef card, out CardIdentity identity)
        {
            identity = null;

            if (!card.IsValid)
                return false;

            CardSetDefinition set = _catalog.FindSet(card.SetId);
            if (set == null)
            {
                // Reachable from a save written before a set was renamed or dropped. Worth
                // saying out loud, because the slot it came from will silently draw as empty.
                Debug.LogWarning(
                    $"[{nameof(CardArtworkResolver)}] No set '{card.SetId}' in the catalog; " +
                    $"{card} cannot be drawn.");

                return false;
            }

            if (set.TryGetCard(card.Number, out identity))
                return true;

            Debug.LogWarning($"[{nameof(CardArtworkResolver)}] Set '{card.SetId}' has no card {card.Number}.");
            return false;
        }
    }
}
