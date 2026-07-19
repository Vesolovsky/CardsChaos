using UnityEngine;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Core
{
    //TODO: Add to the core
    public class BuildVersion : MonoBehaviour
    {
        /// <summary>
        /// MajorVersion.Variant
        /// </summary>
        public const string CURRENT_VERSION = "0.15";
        
        [SerializeField] private VText versionText;

        private void Start()
        {
            versionText.SetText($"v{CURRENT_VERSION}");
        }
    }
}
