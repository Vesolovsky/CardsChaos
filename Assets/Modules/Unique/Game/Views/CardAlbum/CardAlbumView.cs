using System.Collections.Generic;
using CardsChaos.Cards;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Views.Album;
using VInspector;
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

        [Header("Inspect")]
        [SerializeField] private AlbumCardInspector inspector;

        [Header("Header labels")]
        [Tooltip("Current page over total, as X / Y.")]
        [SerializeField] private VText pageText;

        [Tooltip("The open set's name. Typed out letter by letter whenever the set changes.")]
        [SerializeField] private TypewriterText setName;

        [Tooltip("The open set's collection progress, correctly filed over total, as Z / K.")]
        [SerializeField] private VText collectionProgressText;

        [Tooltip("The little kick the progress label takes when its number changes. Keep it " +
                 "small - this is meant to be felt, not seen.")]
        [SerializeField] private Vector3 progressPunch = new Vector3(0.12f, 0.12f, 0f);

        [SerializeField] private float progressPunchDuration = 0.3f;

        [Tooltip("How many times the kick oscillates. Lower is gentler - a single settle rather " +
                 "than a buzz. This is the vibrato.")]
        [SerializeField] private float progressPunchFrequency = 3f;

        private readonly List<AlbumSetButton> _setButtons = new List<AlbumSetButton>();

        private DiContainer _container;
        private IAlbumFocusRequest _albumFocus;
        private IGameplayPanels _panels;
        private IInputActions _input;
        private InputAction _toggleAction;
        private AlbumSetButton _openSet;

        // What the collection label currently reads, so the punch only fires when the number
        // actually moves - filing the wrong card raises the change event without changing the
        // count, and that should not kick the label.
        private int _shownCollectionCount = -1;

        // The focus channel is optional so the album still builds while the upgrade system is
        // being wired up; Smart Album Open simply does nothing until its installer is present.
        [Inject]
        private void InjectContainer(
            DiContainer container,
            [InjectOptional] IAlbumFocusRequest albumFocus,
            [InjectOptional] IGameplayPanels panels,
            [InjectOptional] IInputActions input)
        {
            _container = container;
            _albumFocus = albumFocus;
            _panels = panels;
            _input = input;
        }

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            drag.Initialize(ViewModel, ViewModel.Artwork);
            inspector.Initialize(ViewModel.Artwork);
            pages.Initialize(drag, inspector, ViewModel.Album, ViewModel.Artwork);
            handPile.Initialize(drag, inspector, ViewModel.Hand);

            BuildSetButtons();
            BindPagingButtons();

            pages.PageChanged += RefreshPaging;
            drag.CardFiledCorrectly += pages.OnCardFiledCorrectly;
            ViewModel.AlbumChanged += OnAlbumChanged;

            if (_albumFocus != null)
                _albumFocus.OpenRequested += OnAlbumFocusRequested;

            // The HUD's album button pulls the same lever the toggle key does.
            if (_panels != null)
                _panels.AlbumToggleRequested += Toggle;

            if (_input != null)
                _toggleAction = _input.Find(GameInputActions.ToggleAlbum);

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

            // The inspect is a layer over the album, so while it is open it owns the input: the
            // same keys that would page or close the album turn and shut the card instead. Routed
            // through here rather than read in the inspector's own Update so that a single Escape
            // can never close the card and the album in one frame - only one place reads it.
            if (inspector != null && inspector.IsOpen)
            {
                DriveInspector(keyboard);
                return;
            }

            if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
            {
                Toggle();
                return;
            }

            // Escape only ever closes. Bound the other way round it would fight every other panel
            // that wants the same key to back out of itself.
            if (ViewModel.IsOpen.Value && keyboard.escapeKey.wasPressedThisFrame)
                ViewModel.Close();
        }

        /// <summary>Opens the album if it is shut and shuts it if it is open - the B key and the
        /// HUD's album button both land here.</summary>
        private void Toggle()
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsOpen.Value)
                ViewModel.Close();
            else
                ViewModel.Open();
        }

        /// <summary>
        /// The card close-up's controls, the same as the room's inspector so a card reads the
        /// same however it was opened: the right button and Escape close it, and the left button
        /// turns it over when it lands on the card or leaves when it lands off it.
        /// </summary>
        private void DriveInspector(Keyboard keyboard)
        {
            Mouse mouse = Mouse.current;

            bool rightClick = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if (rightClick || keyboard.escapeKey.wasPressedThisFrame)
            {
                inspector.Close();
                return;
            }

            // Space turns the card over from the keyboard, wherever the cursor is.
            if (keyboard.spaceKey.wasPressedThisFrame && !inspector.JustOpened)
            {
                inspector.Flip();
                return;
            }

            // JustOpened swallows the very click that opened the card - without it the card would
            // flip or close on the same press that brought it up.
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || inspector.JustOpened)
                return;

            if (inspector.IsPointerOverCard(mouse.position.ReadValue()))
                inspector.Flip();
            else
                inspector.Close();
        }

        /// <summary>
        /// Plays the page-completion celebration on the open page, to preview it from the album
        /// object itself rather than hunting down the page strip in play mode.
        /// </summary>
        [Button]
        private void TestPageCompletion() => pages.PlayCompletionEffect();

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

            RefreshPaging();
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

            // Show the pages first: it settles the page count that the page label then reads.
            pages.Show(button.Set);
            RefreshPaging();

            if (setName != null)
                setName.Play(button.Set.SetName);

            // No punch on a set switch - the number is jumping to a different set's total, not
            // being earned.
            SetCollectionProgress(button.Set, punch: false);
        }

        /// <summary>
        /// Carries out a Smart Album Open: opens the album if it is shut, turns to the requested
        /// set and jumps straight to the page the skill worked out. The jump is immediate - the
        /// point of the skill is to be already there, not to watch the pages flick past.
        /// </summary>
        private void OnAlbumFocusRequested(string setId, int pageIndex)
        {
            if (ViewModel == null)
                return;

            AlbumSetButton button = _setButtons.Find(b => b.Set != null && b.Set.SetId == setId);
            if (button == null)
                return;

            if (!ViewModel.IsOpen.Value)
                ViewModel.Open();

            OpenSet(button);
            pages.GoToPage(pageIndex, immediately: true);
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
                    break;
                }
            }

            // The open set's own label tracks the same count, and here it may punch: a card was
            // just filed, so a change in the number is something the player earned.
            if (_openSet != null && _openSet.Set.SetId == setId)
                SetCollectionProgress(_openSet.Set, punch: true);
        }

        private void RefreshPaging()
        {
            if (nextPageButton != null)
                nextPageButton.interactable = pages.CanGoNext;

            if (previousPageButton != null)
                previousPageButton.interactable = pages.CanGoPrevious;

            if (pageText != null)
                pageText.SetText($"{pages.PageIndex + 1}/{pages.PageCount}");
        }

        /// <summary>
        /// Writes the open set's collection count, and kicks the label when the number moves - but
        /// only when a placement caused it, not when a set switch simply swaps in a different
        /// total.
        /// </summary>
        private void SetCollectionProgress(CardSetDefinition set, bool punch)
        {
            if (collectionProgressText == null)
                return;

            int filed = ViewModel.CountFiled(set.SetId);
            collectionProgressText.SetText($"{filed}/{set.CardCount}");

            bool changed = filed != _shownCollectionCount;
            _shownCollectionCount = filed;

            if (punch && changed)
            {
                Tween.PunchScale(collectionProgressText.rectTransform, progressPunch,
                    progressPunchDuration, progressPunchFrequency);
            }
        }

        private void OnIsOpenChanged(bool isOpen)
        {
            // UI Images are not camera-driven mip-streaming renderers. Explicitly keep only the
            // current album page sharp while it is on screen, and release it as soon as the album
            // closes so the world can use the budget.
            if (pages != null)
                pages.SetFullResolutionEnabled(isOpen);

            if (isOpen)
            {
                Show(destroyCancellationToken).Forget();
            }
            else
            {
                // A card left open when the album goes would come back both stale and, worse,
                // still holding the input the next time the album opened.
                if (inspector != null)
                    inspector.Close();

                Hide(destroyCancellationToken).Forget();
            }
        }

        protected override void OnDestroy()
        {
            if (pages != null)
                pages.PageChanged -= RefreshPaging;

            if (drag != null && pages != null)
                drag.CardFiledCorrectly -= pages.OnCardFiledCorrectly;

            if (ViewModel != null)
                ViewModel.AlbumChanged -= OnAlbumChanged;

            if (_albumFocus != null)
                _albumFocus.OpenRequested -= OnAlbumFocusRequested;

            if (_panels != null)
                _panels.AlbumToggleRequested -= Toggle;

            base.OnDestroy();
        }
    }
}
