using Cysharp.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.UISystem.Init;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Core.Analytics
{
    //TODO: Add to the core
    public interface IUnityAnalyticsService
    {
        public void SendEvent(Event ev);
        public void EnableAnalytics();
        public void DisableAnalytics();
    }

    public class UnityAnalyticsService : IUnityAnalyticsService, IAsyncInitializable
    {
        private readonly GameSaveService _saveService;
        private readonly ISaveCoordinator _saveCoordinator;

        [Inject]
        public UnityAnalyticsService(GameSaveService saveService, ISaveCoordinator saveCoordinator)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
        }

        public async UniTask Initialize()
        {
            if(UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if(_saveService.CurrentSave.IsAnalyticsAllowed)
            {
                AnalyticsService.Instance.StartDataCollection();
            }
        }

        public void EnableAnalytics()
        {
            _saveService.CurrentSave.IsAnalyticsAllowed = true;
            _saveCoordinator.MarkDirty();
            AnalyticsService.Instance.StartDataCollection();
        }

        public void DisableAnalytics()
        {
            _saveService.CurrentSave.IsAnalyticsAllowed = false;
            _saveCoordinator.MarkDirty();
            AnalyticsService.Instance.StopDataCollection();
        }

        public void SendEvent(Event ev)
        {
            if (_saveService.CurrentSave.IsAnalyticsAllowed == false) return;
            AnalyticsService.Instance.RecordEvent(ev);
        }
    }
}
