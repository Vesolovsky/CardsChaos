using UnityEngine;

namespace Vesolovsky.Core.Utils
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleDestroyer : MonoBehaviour
    {
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            if (!_particleSystem.IsAlive())
            {
                Destroy(gameObject);
            }
        }
    }
}