using UnityEngine;
using Vesolovsky.Core.Services.Achievements;
using Vesolovsky.Core.Services.Steam;
using Vesolovsky.Game.Utils;
using Zenject;

namespace Vesolovsky.Game
{
    /// <summary>
    /// Brings Steam up with the project context, so it is running before any scene loads and stays
    /// up for the whole session.
    ///
    /// The session is started here, in InstallBindings, rather than through the async init pass:
    /// Steam has to be up before anything can ask it a question, and unlike the save it is not
    /// something the game waits on - it either came up or it did not, and the game plays either way.
    /// </summary>
    public class SteamworksInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            var steam = new SteamService(GlobalData.STEAM_APP_ID);

            if (!steam.Boot())
            {
                // Steam is relaunching the game through the client; this process is on its way out,
                // so there is nothing worth wiring up.
                Application.Quit();
                return;
            }

            // Both the interface (what game code depends on) and the concrete type (what the
            // per-frame runtime needs, for RunCallbacks and Shutdown) resolve to this one instance.
            Container.BindInterfacesAndSelfTo<SteamService>().FromInstance(steam).AsSingle();

            // Its own persistent object, the same shape as ApplicationSaveHandler: it pumps the
            // Steam callback queue every frame and closes the session on quit. NonLazy because
            // nothing resolves it and without it no Steam callback would ever arrive.
            Container.Bind<SteamRuntime>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();

            // Bound whether or not Steam came up: with no session it degrades to a console line per
            // award, which is how the achievement conditions are exercised in the editor.
            Container.BindInterfacesAndSelfTo<SteamAchievementService>().AsSingle();
        }
    }
}
