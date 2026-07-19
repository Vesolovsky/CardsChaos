using UnityEngine;
using UnityEngine.Assertions;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    [RequireComponent(typeof(VButton))]
    public class ViewShowButton : MonoBehaviour
    {
        [SerializeField] private GameObject viewObject;

        private VButton _button;
        private IView _view;

        private bool _isOpening = false;

        private void Awake()
        {
            _button = GetComponent<VButton>();
            _view = viewObject.GetComponent<IView>();
            Assert.IsNotNull(_button);
        }

        private void OnEnable()
        {
            _button.Bind(OpenView);
        }

        private async void OpenView()
        {
            if (_isOpening) return;

            _isOpening = true;
            await _view.Show(destroyCancellationToken);
            _isOpening = false;
        }
    }
}