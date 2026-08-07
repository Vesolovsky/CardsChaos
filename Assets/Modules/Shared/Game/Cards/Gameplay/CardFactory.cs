using UnityEngine;
using Zenject;

namespace CardsChaos.Cards
{
    public interface ICardFactory
    {
        Card Create(Card prefab, Vector3 position, Quaternion rotation);
    }

    public class CardFactory : ICardFactory
    {
        private const string RootName = "Cards";

        private readonly DiContainer _container;

        private Transform _root;

        [Inject]
        public CardFactory(DiContainer container)
        {
            _container = container;
        }

        public Card Create(Card prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;

            _root ??= new GameObject(RootName).transform;

            // Instantiated through the container rather than Object.Instantiate so the card's
            // dependencies (its audio service, for the landing sound) are injected. Scene-placed
            // cards get the same treatment from the SceneContext; this is the runtime-spawn path.
            Card instance = _container.InstantiatePrefabForComponent<Card>(
                prefab, position, rotation, _root);

            // Card prefabs are deliberately stored in their cheap resting state. A factory spawn
            // is the exceptional case that begins in the air, so opt it into physics explicitly.
            instance.BeginFlight();
            return instance;
        }
    }
}
