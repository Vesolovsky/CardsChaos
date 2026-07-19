using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vesolovsky.Core.Audio;
using Zenject;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    //TODO: add to the core
    public class VSlider : Slider
    {
        private IAudioService _audioService;

        [Inject]
        private void Inject(IAudioService audioService)
        {
            _audioService = audioService;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _audioService.Play(AudioSFXKey.ClickButtonUp);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _audioService.Play(AudioSFXKey.ClickButtonDown);
        }
    }
}
