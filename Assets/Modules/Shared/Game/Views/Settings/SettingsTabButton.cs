using System;
using UnityEngine;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views
{
    /// <summary>Inspector-friendly wiring for one button in the Settings tab strip.</summary>
    public class SettingsTabButton : MonoBehaviour
    {
        [SerializeField] private VButton button;
        [Tooltip("The existing Highlight child from TabButton.prefab.")]
        [SerializeField] private GameObject highlight;
        [Tooltip("The matching General/Video/Input/Audio tab root.")]
        [SerializeField] private GameObject content;

        public void Bind(Action clicked)
        {
            if (button != null)
                button.Bind(clicked);
        }

        public void SetSelected(bool selected)
        {
            if (highlight != null)
                highlight.SetActive(selected);

            if (content != null)
                content.SetActive(selected);
        }
    }
}
