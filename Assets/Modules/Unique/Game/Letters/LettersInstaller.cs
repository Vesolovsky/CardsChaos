using UnityEngine;
using Zenject;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// Wires the readable-letters feature into the gameplay scene: the cursor driver that outlines
    /// and opens a letter, the read mode it opens into, and the collection that remembers which
    /// letters have been read.
    ///
    /// Add this component to the gameplay SceneContext (or list it under that context's Mono
    /// Installers), the same place the card and upgrade installers sit. It needs the save bindings
    /// (ISaveService&lt;GameSave&gt;, ISaveCoordinator), the input asset and the scene views service,
    /// all already provided to the scene container.
    /// </summary>
    public class LettersInstaller : MonoInstaller
    {
        [Tooltip("The letters' shared look, in one place: hover outline colour and width, plus the " +
                 "roster of authors (each with the name that signs their notes and their handwriting " +
                 "font).")]
        [SerializeField] private LetterSettings settings = new LetterSettings();

        public override void InstallBindings()
        {
            Container.BindInstance(settings).AsSingle();

            // NonLazy so its load-time pass runs and hides the letters a past session already read,
            // even though nothing resolves it directly.
            Container.BindInterfacesAndSelfTo<LetterCollection>().AsSingle().NonLazy();

            // NonLazy so it starts watching for milestones and restores the arrival queue on load
            // without anything resolving it.
            Container.BindInterfacesTo<LetterAppearanceService>().AsSingle().NonLazy();

            Container.BindInterfacesTo<LetterInspector>().AsSingle();
            Container.BindInterfacesTo<LetterInteractionController>().AsSingle();
        }
    }
}
