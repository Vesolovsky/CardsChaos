using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services.Steam
{
    /// <summary>
    /// The player-loop half of the Steam session: it pumps the callback queue every frame and closes
    /// the session on the way out.
    ///
    /// Lives on its own object under the project context, the same shape as
    /// <see cref="Vesolovsky.Core.Services.Save.ApplicationSaveHandler"/>, so it survives every scene
    /// load and there is nothing to wire in the Inspector. Both quit routes are covered: OnDestroy is
    /// what catches leaving play mode in the editor, where OnApplicationQuit alone would leave the
    /// native session open and the next play session unable to start.
    /// </summary>
    public class SteamRuntime : MonoBehaviour
    {
        private SteamService _steam;
        private bool _shutDown;

        [Inject]
        public void Construct(SteamService steam)
        {
            _steam = steam;
        }

        private void Update()
        {
            _steam?.RunCallbacks();
        }

        private void OnApplicationQuit() => ShutdownOnce();

        private void OnDestroy() => ShutdownOnce();

        private void ShutdownOnce()
        {
            if (_shutDown)
                return;

            _shutDown = true;
            _steam?.Shutdown();
        }
    }
}
