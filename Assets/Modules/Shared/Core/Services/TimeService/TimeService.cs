using UniRx;
using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class TimeService : ITimeService, IInitializable
    {
        private const float SCENE_INITIAL_TIME_SCALE = 1;

        private IReactiveProperty<float> _timeScale;

        public IReadOnlyReactiveProperty<float> TimeScale => _timeScale;


        public void SetTimeScale(float timeScale)
        {
            _timeScale.Value = timeScale;
            Time.timeScale = _timeScale.Value;
        }

        public void Initialize()
        {
            _timeScale = new ReactiveProperty<float>();
            SetTimeScale(SCENE_INITIAL_TIME_SCALE);
        }
    }
}
