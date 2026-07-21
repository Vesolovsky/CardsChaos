using UnityEngine;

namespace CardsChaos.Cards
{
    public interface ICardFactory
    {
        Card Create(Card prefab, Vector3 position, Quaternion rotation);
    }

    public class CardFactory : ICardFactory
    {
        private const string RootName = "Cards";

        private Transform _root;

        public Card Create(Card prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;

            _root ??= new GameObject(RootName).transform;

            return Object.Instantiate(prefab, position, rotation, _root);
        }
    }
}
