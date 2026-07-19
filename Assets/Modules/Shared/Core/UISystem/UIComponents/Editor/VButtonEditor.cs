using UnityEditor;
using UnityEditor.UI;

namespace Vesolovsky.Core.UISystem.UIComponents.Editor
{
    [CustomEditor(typeof(VButton), true)]
    public class VButtonEditor : ButtonEditor
    {
        private SerializedProperty _antiSmasherEnabledProperty;
        private SerializedProperty _clickBlockTimeMSProperty;
        private SerializedProperty _buttonTextProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            _antiSmasherEnabledProperty = serializedObject.FindProperty("antiSmasherEnabled");
            _clickBlockTimeMSProperty = serializedObject.FindProperty("clickBlockTimeMS");
            _buttonTextProperty = serializedObject.FindProperty("text");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_buttonTextProperty);
            _antiSmasherEnabledProperty.boolValue = EditorGUILayout.Toggle("Anti smasher enabled", _antiSmasherEnabledProperty.boolValue);

            if (_antiSmasherEnabledProperty.boolValue == true)
            {
                EditorGUILayout.PropertyField(_clickBlockTimeMSProperty);
            }

            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }
    }
}