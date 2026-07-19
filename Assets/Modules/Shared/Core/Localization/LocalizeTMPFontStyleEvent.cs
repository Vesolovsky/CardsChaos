using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Vesolovsky.Core.Localization
{
    public class LocalizeTMPFontStyleEvent
        : LocalizedAssetEvent<TMPFontStyle, LocalizedTMPFontStyle, UnityEventTMPFontStyle>
    {
        private TMP_Text _target;

        private void Awake()
        {
            _target = GetComponent<TMP_Text>();
        }

        private void Reset()
        {
            _target = GetComponent<TMP_Text>();
        }

        public void Apply(TMPFontStyle style)
        {
            if (_target == null || style == null) return;

            if (style.Font != null)
                _target.font = style.Font;

            if (style.FontMaterialPreset != null)
                _target.fontSharedMaterial = style.FontMaterialPreset;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_target == null) _target = GetComponent<TMP_Text>();
            if (_target == null) return;

            if (OnUpdateAsset.GetPersistentEventCount() > 0) return;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(OnUpdateAsset, Apply);
        }
#endif
    }

    [Serializable]
    public class LocalizedTMPFontStyle : LocalizedAsset<TMPFontStyle> { }

    [Serializable]
    public class UnityEventTMPFontStyle : UnityEvent<TMPFontStyle> { }
}
