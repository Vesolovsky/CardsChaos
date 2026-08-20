using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Album;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Services.Stats;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class SaveServiceInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<GameSaveService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SaveCoordinator<GameSave>>().AsSingle();

            // Persistent (ProjectContext) so it catches the quit whatever scene is loaded. Its own
            // GameObject; nothing to wire in the Inspector.
            Container.Bind<ApplicationSaveHandler>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();

            // The album is nothing but a view over the save, so it is bound where the save is
            // rather than with the scene that happens to draw it. Filed cards outlive the
            // gameplay scene.
            Container.BindInterfacesAndSelfTo<LocalCardAlbum>().AsSingle();

            // Same reasoning: a finished game's closing tally is written into the save and read
            // back from the main menu long after the room it happened in is gone.
            Container.BindInterfacesAndSelfTo<LocalEndgameRecord>().AsSingle();
        }
    }
}
