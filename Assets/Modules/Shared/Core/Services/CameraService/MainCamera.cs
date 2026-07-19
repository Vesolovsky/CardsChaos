using UnityEngine;

namespace Vesolovsky.Core.Services
{
    public class MainCamera : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        public Camera Camera => _camera;

        private void Reset()
        {
            _camera = GetComponent<Camera>();
        }
    }
}
