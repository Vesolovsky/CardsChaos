using UnityEngine;
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

        public override void InstallBindings()
        {
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
