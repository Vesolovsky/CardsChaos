using UnityEngine;
using UnityEngine.EventSystems;
using Vesolovsky.Core.Audio;
using Zenject;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Plays the shared UI click sound on pointer-down, for a press that answers at once. Safe to
    /// share a GameObject with a Selectable (a Slider, a Dropdown) - both down handlers run - but
    /// never put it on a child that a parent Selectable needs the pointer-down from, or it will
    /// intercept that press (the event system stops at the first handler up the hierarchy).
    ///
    /// Wire it in the Inspector (Zenject injects it) or add it at runtime and call
    /// <see cref="Initialize"/>, which AddComponent does not run injection for.
    /// </summary>
    [AddComponentMenu("Vesolovsky/UI/Pointer Click Audio")]
    public class PointerClickAudio : MonoBehaviour, IPointerDownHandler
    {
        private IAudioService _audioService;

        [Inject]
        private void Inject(IAudioService audioService)
        {
            _audioService = audioService;
        }

        public void Initialize(IAudioService audioService)
        {
            _audioService = audioService;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _audioService?.Play(AudioSFXKey.ButtonClick);
        }
    }
}
