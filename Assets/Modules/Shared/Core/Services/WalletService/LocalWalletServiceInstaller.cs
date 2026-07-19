using Zenject;
using Vesolovsky.Game.Services.Wallet;

namespace Vesolovsky.Core.Services.Wallet
{
    public class LocalWalletServiceInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<LocalWallet>().AsSingle();
            Container.BindInterfacesAndSelfTo<WalletService>().AsSingle();
        }
    }
}