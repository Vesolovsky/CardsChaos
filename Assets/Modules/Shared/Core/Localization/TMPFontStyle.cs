using TMPro;
using UnityEngine;

namespace Vesolovsky.Core.Localization
{
    //TODO: Add to the core
    [CreateAssetMenu(menuName = "Vesolovsky/Localization/TMP Font Style")]
    public class TMPFontStyle : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Material fontMaterialPreset;

        public TMP_FontAsset Font => font;
        public Material FontMaterialPreset => fontMaterialPreset;
    }
}
