using Unity.VisualScripting;
using UnityEngine;
using Vesolovsky.Core.Services;
using Zenject;

namespace Vesolovsky
{
    public class LookAtCamera : MonoBehaviour
    {
        private ICameraService _cameraService;

        [Inject]
        private void Inject(ICameraService cameraService)
        {
            _cameraService = cameraService;
        }

        private void Update()
        {
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraService.MainCamera.transform.position);
        }
    }
}
