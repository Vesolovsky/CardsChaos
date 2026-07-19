using UnityEngine;
using UnityEditor;

namespace Vesolovsky.Core.Utils
{
    //TODO: add to the core
    [CustomPropertyDrawer(typeof(VFloatReference))]
    public class VFloatReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty useConstantProp = property.FindPropertyRelative("UseConstant");
            SerializedProperty constantValueProp = property.FindPropertyRelative("ConstantValue");
            SerializedProperty variableProp = property.FindPropertyRelative("Variable");

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float buttonWidth = 20f;
            Rect valueRect = new Rect(position.x, position.y, position.width - buttonWidth - 4f, position.height);
            Rect toggleRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

            if (useConstantProp.boolValue)
            {
                EditorGUI.PropertyField(valueRect, constantValueProp, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(valueRect, variableProp, GUIContent.none);
            }

            if (GUI.Button(toggleRect, new GUIContent(useConstantProp.boolValue ? "C" : "V", "Toggle between constant and variable")))
            {
                useConstantProp.boolValue = !useConstantProp.boolValue;
            }

            EditorGUI.EndProperty();
        }
    }

}
