using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Utils;
using Zenject;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    [AddComponentMenu("Vesolovsky/UI/VButton", 0)]
    [SelectionBase]
    public class VButton : Button, IDisposable
    {
        public event Action PointerEnter;
        public event Action PointerExit;

        [SerializeField] Optional<VText> text;
        [SerializeField] private bool antiSmasherEnabled;
        [SerializeField] private int clickBlockTimeMS = 500;
        
        private UnityAction _currentAction;
        private bool _clicksBlocked = false;

        private IAudioService _audioService;

        [Inject]
        private void Inject(IAudioService audioService)
        {
            _audioService = audioService;
        }

        public void Bind(Action clickAction)
        {
            Unbind();
            _currentAction = new UnityAction(clickAction);
            onClick.AddListener(OnClickHandler);
        }

        public void Unbind()
        {
            if (_currentAction == null) return;

            onClick.RemoveListener(OnClickHandler);
            _currentAction = null;
        }

        public void SetText(string value)
        {
            if(text.Enabled == false || text.Value == null)
            {
                Debug.LogError($"Can't set text for a button object: '{gameObject.name}' as it's text is not enabled or is null.");
                return;
            }

            text.Value.SetText(value);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if(interactable)
            {
                PointerEnter?.Invoke();
            }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (interactable)
            {
                PointerExit?.Invoke();
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (interactable)
            {
                _audioService.Play(AudioSFXKey.ClickButtonUp);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if(interactable)
            {
                _audioService.Play(AudioSFXKey.ClickButtonDown);
            }
        }

        private void OnClickHandler()
        {
            if (!_clicksBlocked && _currentAction != null)
            {
                _currentAction.Invoke();

                if (antiSmasherEnabled)
                {
                    BlockButtonSmashing();
                }
            }
        }

        private async void BlockButtonSmashing()
        {
            _clicksBlocked = true;
            await UniTask.Delay(clickBlockTimeMS, cancellationToken: destroyCancellationToken).SuppressCancellationThrow();
            _clicksBlocked = false;
        }

        public void Dispose()
        {
            Unbind();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Dispose();
        }
    }
}