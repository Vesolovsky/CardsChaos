using CardsChaos.Cards;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Game.Services.Stats;
using Zenject;

namespace Vesolovsky.Game.MainMenu
{
    /// <summary>
    /// The few pieces of the game the main menu needs on its own.
    ///
    /// Most of what the menu reads - the save, the album, audio, settings, the scene transition -
    /// already lives on the project context and is simply there. What is missing here is the
    /// handful of things the gameplay scene binds for itself, and that two of the menu's screens
    /// turn out to need:
    ///
    /// <list type="bullet">
    /// <item>The <b>card catalog</b>, because the album has to know what the sets are and what
    /// each card looks like before it can show a single one.</item>
    /// <item>The <b>input asset</b>, so the Settings screen can rebind keys from the menu rather
    /// than only from inside a game.</item>
    /// <item>A <b>saved-stats reader</b>, so the finished album's closing spread can put up the
    /// player's tally without the room's tracker being here to count it.</item>
    /// </list>
    ///
    /// Deliberately not bound: the hand, the card factory and the world lock. The album asks for
    /// all three optionally and reads their absence as "this is a display case", which is exactly
    /// what the menu's album is.
    /// </summary>
    public class MainMenuSceneInstaller : MonoInstaller
    {
        [Tooltip("The same catalog asset the gameplay scene uses. Without it the album opens empty.")]
        [SerializeField] private CardCatalog catalog;

        [Tooltip("The game's input schema. Only needed so Settings can rebind keys from the menu; " +
                 "left empty the Settings screen simply offers nothing to rebind.")]
        [SerializeField] private InputActionAsset inputActions;

        public override void InstallBindings()
        {
            if (catalog != null)
            {
                Container.Bind<ICardCatalog>().FromInstance(catalog).AsSingle();
            }
            else
            {
                Debug.LogError($"[{nameof(MainMenuSceneInstaller)}] No {nameof(CardCatalog)} " +
                               "assigned; the album opened from the menu will have no sets.", this);
            }

            if (inputActions != null)
            {
                Container.BindInstance(inputActions);
                Container.BindInterfacesTo<InputActions>().AsSingle().NonLazy();
            }

            // Read-only by construction: nothing in this scene counts anything, it only reports
            // what the last session left behind.
            Container.BindInterfacesTo<SavedPlayerStats>().AsSingle();
        }
    }
}
