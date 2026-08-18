using Steamworks;
using UnityEngine;
using Vesolovsky.Core.Services.Steam;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Core
{
    //TODO: add to the core
    [RequireComponent(typeof(VButton))]
    public class URLOpenButton : MonoBehaviour
    {
        [SerializeField] private string URL = "";

        private VButton _button;

        private void Awake()
        {
            _button = GetComponent<VButton>();
        }

        private void Start()
        {
            _button.Bind(OpenURL);
        }
        private void OpenURL()
        {
            // The static read rather than an injected ISteamService: this component is dropped into
            // UI prefabs that are not always built through a Zenject factory, so there is no context
            // guaranteed to inject it - and whether Steam is running is process state anyway.
            if (SteamService.IsRunning)
            {
                SteamFriends.ActivateGameOverlayToWebPage(URL);
            }
            else
            {
                Application.OpenURL(URL);
            }
        }
    }
}
