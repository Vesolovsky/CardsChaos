using System.Collections.Generic;
using CardsChaos.Cards;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Views.Album;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The album: the sets down one side, the open set's pages in the middle, and the cards
    /// currently in hand piled up ready to be filed.
    ///
    /// The view owns the wiring and nothing else. Which set is open and which page is showing are
    /// its business because they are about looking; what a card is and where it is allowed to go
    /// belong to the view model.
    /// </summary>
    public class CardAlbumView : View<ICardAlbumViewModel>
    {
        [Header("Sets")]
        [Tooltip("The vertical layout the set buttons are spawned into.")]
        [SerializeField] private RectTransform setButtonsContainer;

        [SerializeField] private AlbumSetButton setButtonPrefab;

        [Header("Pages")]
        [SerializeField] private AlbumPageStrip pages;

        [Tooltip("Optional until the paging buttons exist. Without them the album still opens; " +
                 "it just cannot be turned past the first page.")]
        [SerializeField] private VButton nextPageButton;

        [SerializeField] private VButton previousPageButton;

        [Header("Hand")]
        [SerializeField] private AlbumHandFan handPile;

        [Header("Dragging")]
        [SerializeField] private AlbumDragController drag;

        [Header("Input")]
        [Tooltip("Opens and closes the album. Escape also closes it.")]
        [SerializeField] private Key toggleKey = Key.B;

        private readonly List<AlbumSetButton> _setButtons = new List<AlbumSetButton>();

        private DiContainer _container;
        private AlbumSetButton _openSet;

        [Inject]
        private void InjectContainer(DiContainer container) => _container = container;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            drag.Initialize(ViewModel, ViewModel.Artwork);
            pages.Initialize(drag, ViewModel.Album, ViewModel.Artwork);
            handPile.Initialize(drag, ViewModel.Hand);

            BuildSetButtons();
            BindPagingButtons();

            pages.PageChanged += RefreshPagingButtons;
            ViewModel.AlbumChanged += OnAlbumChanged;

            ViewModel.IsOpen
                .Subscribe(OnIsOpenChanged)
                .AddTo(this);

            // Something has to be showing, and the first set is as good a guess as any - the
            // album has no notion yet of which one the player was last looking at.
            if (_setButtons.Count > 0)
                OpenSet(_setButtons[0]);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || ViewModel == null)
                return;

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                if (ViewModel.IsOpen.Value)
                    ViewModel.Close();
                else
                    ViewModel.Open();

                return;
            }

            // Escape only ever closes. Bound the other way round it would fight every other panel
            // that wants the same key to back out of itself.
            if (ViewModel.IsOpen.Value && keyboard.escapeKey.wasPressedThisFrame)
                ViewModel.Close();
        }

        private void BuildSetButtons()
        {
            foreach (CardSetDefinition set in ViewModel.Sets)
            {
                if (set == null)
                {
                    Debug.LogError($"[{nameof(CardAlbumView)}] The catalog has a null set.", this);
                    continue;
                }

                AlbumSetButton button = _container.InstantiatePrefabForComponent<AlbumSetButton>(
                    setButtonPrefab, setButtonsContainer);

                button.Bind(set, OpenSet);
                button.SetProgress(ViewModel.CountFiled(set.SetId));

                _setButtons.Add(button);
            }
        }

        private void BindPagingButtons()
        {
            if (nextPageButton != null)
                nextPageButton.Bind(pages.GoToNextPage);

            if (previousPageButton != null)
                previousPageButton.Bind(pages.GoToPreviousPage);

            RefreshPagingButtons();
        }

        private void OpenSet(AlbumSetButton button)
        {
            if (_openSet == button)
                return;

            // The glow is the only thing saying which set the grid belongs to, so the one being
            // left has to give it up in the same breath the new one takes it.
            if (_openSet != null)
                _openSet.SetSelected(false);

            _openSet = button;
            _openSet.SetSelected(true);

            pages.Show(button.Set);
            RefreshPagingButtons();
        }

        /// <summary>
        /// Only the set counters are refreshed here. The slots themselves are left to the drag
        /// controller, which fills one at the moment the card actually lands on it - redrawing
        /// the page the instant the album changed would put the card in the slot while it is
        /// still visibly in the air.
        /// </summary>
        private void OnAlbumChanged(string setId)
        {
            foreach (AlbumSetButton button in _setButtons)
            {
                if (button.Set.SetId == setId)
                {
                    button.SetProgress(ViewModel.CountFiled(setId));
                    return;
                }
            }
        }

        private void RefreshPagingButtons()
        {
            if (nextPageButton != null)
                nextPageButton.interactable = pages.CanGoNext;

            if (previousPageButton != null)
                previousPageButton.interactable = pages.CanGoPrevious;
        }

        private void OnIsOpenChanged(bool isOpen)
        {
            if (isOpen)
                Show(destroyCancellationToken).Forget();
            else
                Hide(destroyCancellationToken).Forget();
        }

        protected override void OnDestroy()
        {
            if (pages != null)
                pages.PageChanged -= RefreshPagingButtons;

            if (ViewModel != null)
                ViewModel.AlbumChanged -= OnAlbumChanged;

            base.OnDestroy();
        }
    }
}
