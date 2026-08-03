using System;
using UnityEngine;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// Connects one authored KeyBind row to an Input System action. Rows stay static in the prefab;
    /// only their displayed draft binding changes at runtime.
    /// </summary>
    public class KeyBindEntry : MonoBehaviour
    {
        [Tooltip("Exact Input System action name, e.g. Sprint or ToggleAlbum.")]
        [SerializeField] private string actionName;
        [SerializeField] private VButton rebindButton;
        [SerializeField] private VButton resetButton;
        [SerializeField] private VText actionLabel;
        [SerializeField] private VText currentBindingText;

        public string ActionName => actionName;
        public string DisplayName => actionLabel != null && !string.IsNullOrWhiteSpace(actionLabel.text)
            ? actionLabel.text
            : actionName;

        public void Bind(Action<KeyBindEntry> rebind, Action<KeyBindEntry> reset)
        {
            if (rebindButton != null)
                rebindButton.Bind(() => rebind?.Invoke(this));

            if (resetButton != null)
                resetButton.Bind(() => reset?.Invoke(this));
        }

        public void SetBindingText(string value)
        {
            if (currentBindingText != null)
                currentBindingText.SetText(string.IsNullOrWhiteSpace(value) ? "-" : value);
        }
    }
}
