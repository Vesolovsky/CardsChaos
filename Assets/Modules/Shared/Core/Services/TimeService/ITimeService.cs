using UniRx;

namespace Vesolovsky.Core.Services
{
    public interface ITimeService
    {
        public void SetTimeScale(float timeScale);
        public IReadOnlyReactiveProperty<float> TimeScale { get; }
    }
}
