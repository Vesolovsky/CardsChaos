using System;
using CardsChaos.Cards;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// The button that swaps the hand between the corner pile and the fanned-out spread, and shows
    /// which way it is about to swap it.
    ///
    /// The icon is always the layout the hand is in now - the pile icon while piled, the hand icon
    /// while fanned - and the hint offers the other one, "Switch to hand [TAB]". It watches the hand
    /// rather than only its own click, so pressing TAB in the room turns the icon over here too; the
    /// turn is a shrink to nothing, a swap at the bottom, and a grow back.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Hand Switch Button")]
    public class HudHandSwitchButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private VButton button;
        [SerializeField] private HudSlideLabel label;

        [Tooltip("The icon that shows the current layout.")]
        [SerializeField] private Image icon;

        [SerializeField] private Sprite pileIcon;
        [SerializeField] private Sprite handIcon;

        [Header("Hints")]
        [Tooltip("Shown while piled - the swap it offers is to the hand. {0} is the trigger key.")]
        [SerializeField] private string toHandFormat = "Switch to hand [{0}]";

        [Tooltip("Shown while fanned - the swap it offers is to the pile. {0} is the trigger key.")]
        [SerializeField] private string toPileFormat = "Switch to pile [{0}]";

        [Header("Icon swap")]
        [SerializeField] private float shrinkDuration = 0.12f;
        [SerializeField, SearchableEnum] private Ease shrinkEase = Ease.InCubic;
        [SerializeField] private float growDuration = 0.16f;
        [SerializeField, SearchableEnum] private Ease growEase = Ease.OutBack;

        private CardHand _hand;
        private string _keyDisplay;
        private bool _hovered;
        private CardHandLayout _shownLayout;

        private Tween _iconTween;

        /// <summary>Wires the toggle click and takes the hand to read and reflect the layout from.</summary>
        public void Initialize(CardHand hand, Action onToggle, string keyDisplay)
        {
            _hand = hand;
            _keyDisplay = keyDisplay;

            if (button != null && onToggle != null)
                button.Bind(onToggle);

            _shownLayout = hand != null ? hand.Layout : CardHandLayout.Pile;
            ApplyIconImmediate(_shownLayout);
            RefreshLabel();
        }

        private void Update()
        {
            if (_hand == null)
                return;

            // The hand is the source of truth for the layout, so a TAB press in the room and a
            // click here both arrive as the same change to react to.
            if (_hand.Layout != _shownLayout)
            {
                _shownLayout = _hand.Layout;
                PlayIconSwap(_shownLayout);

                if (_hovered)
                    RefreshLabel();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            RefreshLabel();

            if (label != null)
                label.Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;

            if (label != null)
                label.Hide();
        }

        private Sprite IconFor(CardHandLayout layout) =>
            layout == CardHandLayout.Pile ? pileIcon : handIcon;

        private void RefreshLabel()
        {
            if (label == null)
                return;

            // Piled now, so the offer is the hand; fanned now, so the offer is the pile.
            string format = _shownLayout == CardHandLayout.Pile ? toHandFormat : toPileFormat;
            label.SetText(string.Format(format, _keyDisplay));
        }

        private void ApplyIconImmediate(CardHandLayout layout)
        {
            if (icon == null)
                return;

            icon.sprite = IconFor(layout);
            icon.rectTransform.localScale = Vector3.one;
        }

        private void PlayIconSwap(CardHandLayout layout)
        {
            if (icon == null)
                return;

            RectTransform rect = icon.rectTransform;

            // A swap already mid-turn is dropped and restarted from full size, so a quick double
            // toggle never leaves the icon stuck small.
            if (_iconTween.isAlive)
                _iconTween.Stop();

            rect.localScale = Vector3.one;

            _iconTween = Tween.Scale(rect, 0f, shrinkDuration, shrinkEase)
                .OnComplete(() =>
                {
                    icon.sprite = IconFor(layout);
                    _iconTween = Tween.Scale(rect, 1f, growDuration, growEase);
                });
        }

        private void OnDestroy()
        {
            if (_iconTween.isAlive)
                _iconTween.Stop();
        }
    }
}
