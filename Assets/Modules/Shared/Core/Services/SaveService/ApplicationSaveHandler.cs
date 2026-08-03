using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services.Save
{
    /// <summary>
    /// Last-chance save when the application is quitting. Uses the synchronous save path because an
    /// async write is not guaranteed to finish before the process exits, and covers every quit
    /// route - the OS window-close button as well as an in-game Quit button.
    ///
    /// Deliberately does not save on OnApplicationPause: on desktop that fires on focus loss
    /// (alt-tab) and would serialize the whole room on every tab-out. A mobile build would add it.
    /// </summary>
    public class ApplicationSaveHandler : MonoBehaviour
    {
        private ISaveCoordinator _saveCoordinator;

        [Inject]
        public void Construct(ISaveCoordinator saveCoordinator)
        {
            _saveCoordinator = saveCoordinator;
        }

        private void OnApplicationQuit()
        {
            _saveCoordinator?.SaveBlocking();
        }
    }
}
