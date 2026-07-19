using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    public class DropdownScrollToSelected : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Dropdown _dropdown;

        private Coroutine _scrollRoutine;

        private void Reset()
        {
            _dropdown = GetComponent<TMP_Dropdown>();
        }

        private void Awake()
        {
            if (_dropdown == null)
                _dropdown = GetComponent<TMP_Dropdown>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_scrollRoutine != null)
                StopCoroutine(_scrollRoutine);

            _scrollRoutine = StartCoroutine(ScrollToSelectedAfterOpen());
        }

        private IEnumerator ScrollToSelectedAfterOpen()
        {
            // TMP_Dropdown creates the Dropdown List only after the click has been handled.
            yield return null;
            yield return new WaitForEndOfFrame();

            ScrollRect scrollRect = FindOpenedDropdownScrollRect();

            if (scrollRect == null)
                yield break;

            Canvas.ForceUpdateCanvases();

            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            Canvas.ForceUpdateCanvases();

            ScrollToSelected(scrollRect);

            // Second pass - the layout/ScrollRect sometimes overrides the position after the first set.
            yield return null;

            Canvas.ForceUpdateCanvases();
            ScrollToSelected(scrollRect);
        }

        private ScrollRect FindOpenedDropdownScrollRect()
        {
            ScrollRect[] scrollRects = FindObjectsByType<ScrollRect>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            foreach (ScrollRect scrollRect in scrollRects)
            {
                Transform t = scrollRect.transform;

                while (t != null)
                {
                    if (t.name.Contains("Dropdown List"))
                        return scrollRect;

                    t = t.parent;
                }
            }

            return null;
        }

        private void ScrollToSelected(ScrollRect scrollRect)
        {
            if (_dropdown == null || scrollRect == null)
                return;

            int optionsCount = _dropdown.options.Count;

            if (optionsCount <= 1)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                scrollRect.velocity = Vector2.zero;
                return;
            }

            int selectedIndex = Mathf.Clamp(_dropdown.value, 0, optionsCount - 1);

            // 1 = top, 0 = bottom
            float normalizedPosition = 1f - ((float)selectedIndex / (optionsCount - 1));

            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
            scrollRect.velocity = Vector2.zero;
        }
    }
}
