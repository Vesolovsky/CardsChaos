using UnityEngine;
using UnityEngine.EventSystems;
using Vesolovsky.Core.Audio;
using Zenject;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Plays the shared UI hover sound when the pointer enters this element. Kept apart from the
    /// click sound on purpose: this can then sit on a slider's handle alone, where a pointer-down
    /// handler would instead swallow the press the slider itself needs to begin a drag.
    ///
    /// Wire it in the Inspector (Zenject injects it like any view component) or add it at runtime
    /// and call <see cref="Initialize"/>, which AddComponent does not run injection for.
    /// </summary>
    [AddComponentMenu("Vesolovsky/UI/Pointer Hover Audio")]
    public class PointerHoverAudio : MonoBehaviour, IPointerEnterHandler
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            _audioService?.Play(AudioSFXKey.ButtonHover);
        }
    }
}
