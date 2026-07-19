using System;
using UnityEditor;
using UnityEngine;
namespace Vesolovsky.Core.Utils.Editor
{
    [CustomPropertyDrawer(typeof(InlineEntry), true)]
    public class InlineEntryDrawer : PropertyDrawer
    {
        private const float Spacing = 6f;
        private const float KeyWidthRatio = 0.5f;
        private const float ValueWidthRatio = 0.5f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!ShouldDrawInline(property))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var keyProp = property.FindPropertyRelative("key");
            var valueProp = property.FindPropertyRelative("value");

            if (keyProp == null || valueProp == null)
            {
                EditorGUI.HelpBox(position,
                    $"{property.displayName}: [Inline] / InlineEntry requires fields with names 'key' and 'value'.",
                    MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect contentRect = EditorGUI.PrefixLabel(
                position,
                GUIUtility.GetControlID(FocusType.Passive),
                label
            );

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float availableWidth = contentRect.width - Spacing;
            float keyWidth = Mathf.Max(40f, availableWidth * KeyWidthRatio);
            float valueWidth = Mathf.Max(40f, availableWidth * ValueWidthRatio);

            if (keyWidth + valueWidth > availableWidth)
            {
                float total = keyWidth + valueWidth;
                keyWidth = availableWidth * (keyWidth / total);
                valueWidth = availableWidth * (valueWidth / total);
            }

            float keyHeight = EditorGUI.GetPropertyHeight(keyProp, GUIContent.none, true);
            float valueHeight = EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);
            float totalHeight = Mathf.Max(keyHeight, valueHeight);

            Rect keyRect = new Rect(
                contentRect.x,
                contentRect.y,
                keyWidth,
                totalHeight
            );

            Rect valueRect = new Rect(
                contentRect.x + keyWidth + Spacing,
                contentRect.y,
                availableWidth - keyWidth,
                totalHeight
            );

            EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);
            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);

            EditorGUI.indentLevel = oldIndent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!ShouldDrawInline(property))
                return EditorGUI.GetPropertyHeight(property, label, true);

            var keyProp = property.FindPropertyRelative("key");
            var valueProp = property.FindPropertyRelative("value");

            if (keyProp == null || valueProp == null)
                return EditorGUIUtility.singleLineHeight * 2f;

            float keyHeight = EditorGUI.GetPropertyHeight(keyProp, GUIContent.none, true);
            float valueHeight = EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);

            return Mathf.Max(keyHeight, valueHeight);
        }

        private bool ShouldDrawInline(SerializedProperty property)
        {
            if (property == null)
                return false;

            Type type = fieldInfo?.FieldType;
            if (type == null)
                return true;

            type = GetElementTypeIfNeeded(type);

            if (type == null)
                return true;

            return Attribute.IsDefined(type, typeof(InlineAttribute), true)
                   || typeof(InlineEntry).IsAssignableFrom(type);
        }

        private static Type GetElementTypeIfNeeded(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(System.Collections.Generic.List<>))
                    return type.GetGenericArguments()[0];
            }

            return type;
        }
    }
}
