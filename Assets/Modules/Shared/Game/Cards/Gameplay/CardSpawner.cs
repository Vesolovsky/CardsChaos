using UnityEngine;
using VInspector;
using Zenject;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Scatters random cards from the catalog inside a box volume,
    /// drawn as a gizmo for easy placement in the scene view.
    /// </summary>
    [AddComponentMenu("CardsChaos/Card Spawner")]
    public class CardSpawner : MonoBehaviour
    {
        [SerializeField] private int spawnCount = 40;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Area (local space)")]
        [SerializeField] private Vector3 areaCenter = Vector3.zero;
        [SerializeField] private Vector3 areaSize = new Vector3(1.2f, 0.4f, 0.8f);

        [Header("Orientation")]
        [Tooltip("Spawn cards roughly flat so they settle instead of tumbling.")]
        [SerializeField] private bool lieFlat = true;
        [SerializeField] private float maxTilt = 12f;

        [Header("Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(0.35f, 0.8f, 1f, 1f);

        private ICardCatalog _catalog;
        private ICardFactory _factory;

        [Inject]
        public void Construct(ICardCatalog catalog, ICardFactory factory)
        {
            _catalog = catalog;
            _factory = factory;
        }

        private void Start()
        {
            if (_catalog == null)
            {
                Debug.LogError($"[CardSpawner] Not injected - is CardsInstaller in the SceneContext?", this);
                return;
            }

            if (spawnOnStart)
                Spawn();
        }

        [Button]
        public void Spawn()
        {
            int spawned = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                if (_factory.Create(_catalog.GetRandom(), RandomPosition(), RandomRotation()) != null)
                    spawned++;
            }

            if (spawned < spawnCount)
            {
                Debug.LogError(
                    $"[CardSpawner] Spawned only {spawned}/{spawnCount} cards " +
                    $"(catalog contains {_catalog.Cards.Count}).", this);

#if UNITY_EDITOR
                if (_catalog is CardCatalog concrete)
                    Debug.LogError(concrete.Describe());
                else
                    Debug.LogError($"[CardSpawner] Catalog is not a CardCatalog: {_catalog.GetType().FullName}");
#endif
            }
        }

        private Vector3 RandomPosition()
        {
            var local = areaCenter + new Vector3(
                Random.Range(-0.5f, 0.5f) * areaSize.x,
                Random.Range(-0.5f, 0.5f) * areaSize.y,
                Random.Range(-0.5f, 0.5f) * areaSize.z);

            return transform.TransformPoint(local);
        }

        private Quaternion RandomRotation()
        {
            if (!lieFlat)
                return Random.rotation;

            // The face points along +Z: pitch -90 lays the card face up, +90 face down.
            float pitch = Random.value < 0.5f ? -90f : 90f;

            return Quaternion.Euler(
                pitch + Random.Range(-maxTilt, maxTilt),
                Random.Range(0f, 360f),
                Random.Range(-maxTilt, maxTilt));
        }

        private void OnDrawGizmos() => DrawArea(0.55f, filled: false);

        private void OnDrawGizmosSelected() => DrawArea(1f, filled: true);

        private void DrawArea(float alpha, bool filled)
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            if (filled)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.12f);
                Gizmos.DrawCube(areaCenter, areaSize);
            }

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, alpha);
            Gizmos.DrawWireCube(areaCenter, areaSize);
        }
    }
}
