using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Utils
{
    //TODO: Add this to the core
    public class VPrefabFactory : IFactory<GameObject, Vector3?, Quaternion?, Transform, GameObject>
    {
        private DiContainer _container;

        [Inject]
        public VPrefabFactory(DiContainer container)
        {
            _container = container;
        }

        public GameObject Create(GameObject prefab, Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
        {
            return _container.InstantiatePrefab(
                prefab,
                position ?? Vector3.zero,
                rotation ?? Quaternion.identity,
                parent
            );
        }
    }
}
