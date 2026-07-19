using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.UISystem.Init
{
    //TODO: Add to the core (project and scene context initializators should be installed otherwise init order won't work)
    public class ContextInitializatorInstaller : MonoInstaller
    {
        [SerializeField] private ContextInitializator initializator;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<ContextInitializator>()
                .FromInstance(initializator)
                .AsSingle();
        }
    }
}
