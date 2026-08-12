using System.Collections.Generic;
using CardsChaos.Cards;
using UnityEngine;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>
    /// The run of cards a trailer take steps through in the close-up, in the order they should
    /// appear.
    ///
    /// Entries are card prefabs - the same variants the catalog's sets are built from - so a list
    /// can be put together by dragging them out of the project window. It is an asset rather than a
    /// scene component so that cards added while the game runs (see
    /// <see cref="TrailerCardReel.AddSelectedCard"/>) are still there after play mode ends: picking
    /// the good-looking cards out is far easier with them in your hand than by their file names.
    /// </summary>
    [CreateAssetMenu(menuName = "CardsChaos/Trailer/Card List", fileName = "TrailerCardList")]
    public class TrailerCardList : ScriptableObject
    {
        [Tooltip("Card prefabs, in the order the close-up should step through them.")]
        [SerializeField] private List<Card> cards = new List<Card>();

        public IReadOnlyList<Card> Cards => cards;

        public int Count => cards.Count;

        public bool Contains(Card card) => card != null && cards.Contains(card);

        /// <summary>Appends a card prefab. Duplicates are refused - a reel repeats nothing.</summary>
        public bool Add(Card card)
        {
            if (card == null || Contains(card))
                return false;

            cards.Add(card);
            MarkDirty();
            return true;
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
