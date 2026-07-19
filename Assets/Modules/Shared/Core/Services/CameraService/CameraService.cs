using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public interface ICameraService
    {
        public Camera MainCamera { get; }
        public Vector2 ScreenToWorldPoint(Vector2 screenPoint);
        public Ray SceenPointToRay(Vector2 screenPoint);
        public Vector2 GetScreenBounds();
        public Ray ViewPointToRay(Vector3 point);
    }

    public class CameraService : ICameraService
    {
        private Camera _mainCamera;

        public Camera MainCamera => _mainCamera;

        [Inject]
        public CameraService(MainCamera mainCamera)
        {
            _mainCamera = mainCamera.Camera;
        }

        public Vector2 ScreenToWorldPoint(Vector2 screenPoint)
        {
            return _mainCamera.ScreenToWorldPoint(screenPoint);
        }

        public Vector2 GetScreenBounds()
        {
            float screenHeight = 2f * _mainCamera.orthographicSize;
            float screenWidth = screenHeight * _mainCamera.aspect;

            return new Vector2(screenWidth / 2f, screenHeight / 2f);
        }

        public Ray SceenPointToRay(Vector2 screenPoint)
        {
            return _mainCamera.ScreenPointToRay(screenPoint);
        }

        public Ray ViewPointToRay(Vector3 point)
        {
            return _mainCamera.ViewportPointToRay(point);
        }
    }
}
