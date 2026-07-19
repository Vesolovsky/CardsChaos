using UnityEngine;
using VInspector;

//TODO: Add to the core
namespace Vesolovsky.Core.EditorToolbag
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class Note : MonoBehaviour
    {
        [Tab("Data")]
        [SerializeField, TextArea] private string description = "";
        [SerializeField, Variants("Piotrek", "Szymon")] private string author;

        [Tab("Settings")]
        [SerializeField] private Color cardColor = Color.black;
        [SerializeField] private bool showInScene = true;

        [SerializeField, Min(0)] private float leftMargin = 64f;
        [SerializeField, Min(0)] private float topMargin = 8f;
        [SerializeField, Range(120, 600)] private float maxWidth = 280f;

#if UNITY_EDITOR

        private bool IsDirectlySelected()
        {
            var selected = UnityEditor.Selection.gameObjects;
            if (selected == null || selected.Length == 0) return false;

            for (int i = 0; i < selected.Length; i++)
                if (selected[i] == gameObject) return true;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showInScene) return;
            if (!IsDirectlySelected()) return;

            var sceneView = UnityEditor.SceneView.currentDrawingSceneView;
            if (sceneView == null) return;

            var svSize = sceneView.position.size;

            UnityEditor.Handles.BeginGUI();
            {
                const float pad = 8f;

                var content = new GUIContent(
                    $"Author: {author}\n{(string.IsNullOrEmpty(description) ? "<empty>" : description)}"
                );

                var style = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    padding = new RectOffset(10, 10, 8, 8)
                };

                // Oblicz rozmiar karty
                Vector2 size = style.CalcSize(content);
                size.x = Mathf.Min(size.x, maxWidth);
                size.y = style.CalcHeight(content, size.x);

                // Pozycja: lewy górny róg + marginesy
                float x = leftMargin;
                float y = topMargin;

                // Zabezpieczenie przed wyjœciem poza okno (np. gdy okno jest w¹skie)
                float totalW = size.x + pad;
                float totalH = size.y + pad;
                x = Mathf.Clamp(x, 0f, svSize.x - totalW);
                y = Mathf.Clamp(y, 0f, svSize.y - totalH);

                var rect = new Rect(x, y, totalW, totalH);

                // T³o i treœæ
                var prevColor = GUI.color;
                GUI.color = new Color(cardColor.r, cardColor.g, cardColor.b, 1f);
                GUI.Box(rect, GUIContent.none);
                GUI.color = Color.white;

                GUI.Label(
                    new Rect(rect.x + pad * 0.5f, rect.y + pad * 0.5f, size.x, size.y),
                    content, style
                );

                GUI.color = prevColor;
            }
            UnityEditor.Handles.EndGUI();
        }
#endif
    }
}
