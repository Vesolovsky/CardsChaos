using UnityEngine;
using Zenject;

namespace CardsChaos.Cards
{
    public class CardsInstaller : MonoInstaller
    {
        [SerializeField] private CardCatalog catalog;
        [SerializeField] private CardHand hand;

        public override void InstallBindings()
        {
            Container.Bind<ICardCatalog>().FromInstance(catalog).AsSingle();
            Container.Bind<CardHand>().FromInstance(hand).AsSingle();
            Container.Bind<ICardFactory>().To<CardFactory>().AsSingle();
            Container.BindInterfacesTo<CardInputController>().AsSingle();
        }
    }
}
