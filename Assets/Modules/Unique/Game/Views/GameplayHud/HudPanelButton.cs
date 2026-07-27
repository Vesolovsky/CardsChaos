using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// A HUD button that opens a screen and shows a one-line hint on hover - the Album and Upgrades
    /// buttons. The label reads a fixed phrase with the trigger key in brackets, "Album [B]".
    ///
    /// Hover in and out are read here rather than off the button so the hint keeps working the same
    /// however the button is styled; the click is handed to whoever wires the button, so this stays
    /// out of what opening a screen means.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Panel Button")]
    public class HudPanelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private VButton button;
        [SerializeField] private HudSlideLabel label;

        [Tooltip("The hint, with {0} where the trigger key goes. e.g. \"Album [{0}]\".")]
        [SerializeField] private string labelFormat = "Album [{0}]";

        /// <summary>
        /// Wires the click and writes the hint with the given trigger-key text. The key display is
        /// passed in rather than resolved here so every HUD hint reads from the one input asset.
        /// </summary>
        public void Initialize(Action onClick, string keyDisplay)
        {
            if (label != null)
                label.SetText(string.Format(labelFormat, keyDisplay));

            if (button != null && onClick != null)
                button.Bind(onClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (label != null)
                label.Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (label != null)
                label.Hide();
        }
    }
}
