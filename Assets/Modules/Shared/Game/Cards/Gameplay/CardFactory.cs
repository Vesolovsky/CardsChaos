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

            Card instance = Object.Instantiate(prefab, position, rotation, _root);
            // Card prefabs are deliberately stored in their cheap resting state. A factory spawn
            // is the exceptional case that begins in the air, so opt it into physics explicitly.
            instance.BeginFlight();
            return instance;
        }
    }
}
