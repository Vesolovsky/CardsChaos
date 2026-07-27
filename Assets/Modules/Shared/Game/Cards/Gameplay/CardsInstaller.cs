using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Zenject;

namespace CardsChaos.Cards
{
    public class CardsInstaller : MonoInstaller
    {
        [SerializeField] private CardCatalog catalog;
        [SerializeField] private CardHand hand;
        [SerializeField] private CardInspectSettings inspectSettings = new CardInspectSettings();

        [Tooltip("Optional. Left empty the close-up simply uses whatever light the room offers.")]
        [SerializeField] private CardInspectLight inspectLight;

        [Tooltip("The game's one input schema - every rebindable key lives here. Read by the card " +
                 "table, the album, the upgrades screen, the skills and the HUD.")]
        [SerializeField] private InputActionAsset inputActions;

        public override void InstallBindings()
        {
            // Kept enabled for the whole scene and disposed with it. NonLazy so the enable happens
            // at startup rather than waiting for the first thing to read a key.
            if (inputActions != null)
                Container.BindInstance(inputActions);
            else
                Debug.LogError($"[{nameof(CardsInstaller)}] No {nameof(InputActionAsset)} assigned; " +
                               "gameplay keys will not fire.", this);

            Container.BindInterfacesTo<InputActions>().AsSingle().NonLazy();

            Container.Bind<ICardCatalog>().FromInstance(catalog).AsSingle();
            Container.Bind<CardHand>().FromInstance(hand).AsSingle();
            Container.BindInstance(inspectSettings).AsSingle();
            Container.Bind<ICardFactory>().To<CardFactory>().AsSingle();

            // Bound only when it is actually there, so the inspector's optional dependency stays
            // unresolved rather than resolving to a null it would have to guard against anyway.
            if (inspectLight != null)
                Container.Bind<ICardInspectLight>().FromInstance(inspectLight).AsSingle();

            Container.BindInterfacesTo<CardInputController>().AsSingle();
            Container.BindInterfacesTo<CardInspector>().AsSingle();

            // The table has to see the inspector's state before the inspector can clear it,
            // otherwise the click that leaves the close-up also grabs a card.
            Container.BindExecutionOrder<CardInputController>(-10);
        }
    }
}
