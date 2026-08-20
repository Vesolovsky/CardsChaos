using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Stats;
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
    ///
    /// The same album serves two places. In the room it is a panel the player toggles, holding the
    /// room still while it is up. Opened from the main menu it is a display case instead: the hand
    /// is not drawn, nothing can be dragged, and closing it means the view is gone rather than
    /// hidden. Everything else - the sets, the pages, the close-up - is the one implementation.
    /// </summary>
    public class CardAlbumView : View<ICardAlbumViewModel>
    {
        [Header("Sets")]
        [Tooltip("The vertical layout the set buttons are spawned into.")]
        [SerializeField] private RectTransform setButtonsContainer;

        [SerializeField] private AlbumSetButton setButtonPrefab;

        [Tooltip("The list the set buttons scroll inside. Leave empty to take the one the buttons " +
                 "container already sits in.")]
        [SerializeField] private ScrollRect setButtonsScroll;

        [Header("Pages")]
        [SerializeField] private AlbumPageStrip pages;

        [Tooltip("Optional until the paging buttons exist. Without them the album still opens; " +
                 "it just cannot be turned past the first page.")]
        [SerializeField] private VButton nextPageButton;

        [SerializeField] private VButton previousPageButton;

        [Tooltip("Optional. Shuts the album, the same as Escape does. Only ever shown on the " +
                 "album opened from the main menu, where there is no HUD button and no key the " +
                 "player has been taught yet - in the room the album is a panel the player " +
                 "toggles, and a button to close it would be clutter. Switched on and off from " +
                 "code, so whether it is enabled in the prefab makes no difference.")]
        [SerializeField] private VButton closeButton;

        [Header("Hand")]
        [SerializeField] private AlbumHandFan handPile;

        [Header("Dragging")]
        [SerializeField] private AlbumDragController drag;

        [Header("Inspect")]
        [SerializeField] private AlbumCardInspector inspector;

        [Header("Endgame")]
        [Tooltip("The normal album layout (BG): categories, pages, top and bottom panels. Switched " +
                 "off when the album opens into its endgame state.")]
        [SerializeField] private GameObject normalLayout;

        [Tooltip("The endgame layout: the one final-card slot and the closing stat lines. Switched " +
                 "on instead of the normal layout while the player holds the final card.")]
        [SerializeField] private AlbumFinalCardLayout finalLayout;

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

        // Matches the room's table and the fan, so the wheel has the same dead spot everywhere.
        private const float ScrollDeadzone = 0.01f;

        private readonly List<AlbumSetButton> _setButtons = new List<AlbumSetButton>();

        // Reused each scroll to find what the cursor is over, so a wheel notch over a scrolling
        // list is not also spent cycling the hand.
        private readonly List<RaycastResult> _scrollRaycast = new List<RaycastResult>();

        private DiContainer _container;
        private IAlbumFocusRequest _albumFocus;
        private IGameplayPanels _panels;
        private IPlayerStats _playerStats;
        private IInputActions _input;
        private InputAction _toggleAction;
        private InputAction _flipCardAction;
        private AlbumSetButton _openSet;

        // The scroll view found above the buttons container when none was authored. NonSerialized
        // because Unity keeps private serializable fields across an edit-to-play domain reload, and
        // a cache filled in play mode has no business surviving into the next one.
        [System.NonSerialized] private ScrollRect _resolvedSetButtonsScroll;

        // What the collection label currently reads, so the punch only fires when the number
        // actually moves - filing the wrong card raises the change event without changing the
        // count, and that should not kick the label.
        private int _shownCollectionCount = -1;

        // Read once, at setup, and then trusted: whether the room's half of the album exists is
        // settled by which scene we are in and cannot change while the view is alive.
        private bool _readOnly;

        // A display-case album closes by destroying itself, and Escape can be pressed again while
        // the closing animation is still running.
        private bool _isUnloading;

        // The focus channel is optional so the album still builds while the upgrade system is
        // being wired up; Smart Album Open simply does nothing until its installer is present.
        [Inject]
        private void InjectContainer(
            DiContainer container,
            [InjectOptional] IAlbumFocusRequest albumFocus,
            [InjectOptional] IGameplayPanels panels,
            [InjectOptional] IPlayerStats playerStats,
            [InjectOptional] IInputActions input)
        {
            _container = container;
            _albumFocus = albumFocus;
            _panels = panels;
            _playerStats = playerStats;
            _input = input;
        }

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            _readOnly = ViewModel.IsReadOnly;

            drag.Initialize(ViewModel, ViewModel.Artwork);
            drag.SetInteractive(!_readOnly);
            inspector.Initialize(ViewModel.Artwork);
            pages.Initialize(drag, inspector, ViewModel.Album, ViewModel.Artwork);

            if (_readOnly)
            {
                // The pile is a view of the hand out in the room, and in the menu there is no
                // room and no hand. Taken away rather than left empty: an empty pile invites a
                // drag that could never land anywhere.
                if (handPile != null)
                    handPile.gameObject.SetActive(false);
            }
            else
            {
                handPile.Initialize(drag, inspector, ViewModel.Hand);
            }

            BuildSetButtons();
            BindPagingButtons();

            pages.PageChanged += RefreshPaging;
            drag.CardFiledCorrectly += pages.OnCardFiledCorrectly;
            ViewModel.AlbumChanged += OnAlbumChanged;

            // "Set sense" marks the sets the player is carrying a card from, so the marking moves
            // exactly when the hand does - filing a card, taking one back out, cycling the pile.
            if (ViewModel.Hand != null)
                ViewModel.Hand.Changed += RefreshSetPulses;

            if (_albumFocus != null)
                _albumFocus.OpenRequested += OnAlbumFocusRequested;

            // The HUD's album button pulls the same lever the toggle key does.
            if (_panels != null)
                _panels.AlbumToggleRequested += Toggle;

            if (_input != null)
            {
                // The room's album key toggles a panel the player carries around with them. The
                // menu's album is not that: it was asked for by name and Escape is the way back
                // out, so the key is left unbound there rather than quietly doing something the
                // player has never been taught in a screen that never mentions it.
                if (!_readOnly)
                    _toggleAction = _input.Find(GameInputActions.ToggleAlbum);

                // Turning a card over in the close-up works however the album was opened.
                _flipCardAction = _input.Find(GameInputActions.FlipCard);
            }

            ViewModel.IsOpen
                .Subscribe(OnIsOpenChanged)
                .AddTo(this);

            // Something has to be showing, and the first set is as good a guess as any - the
            // album has no notion yet of which one the player was last looking at.
            if (_setButtons.Count > 0)
                OpenSet(_setButtons[0]);

            if (_readOnly)
                OpenAsDisplayCase();
        }

        /// <summary>
        /// Brings the menu's album up. There is no room to ask and no toggle to wait for - it was
        /// added to the screen to be looked at, so it is already open by the time anything else
        /// runs, and the state is set here so Escape and the endgame check both read it correctly.
        /// </summary>
        private void OpenAsDisplayCase()
        {
            ViewModel.Open();

            AudioService.Play(AudioSFXKey.AlbumOpen);

            RefreshAlbumDisplay();

            // A finished game opens on the closing spread rather than on the sets.
            ApplyEndgameMode();

            ScrollToOpenSet();

            if (pages != null)
                pages.SetFullResolutionEnabled(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || ViewModel == null)
                return;

            // The inspect is a layer over the album, so while it is open it owns the input: the
            // same inputs that would page or close the album turn and shut the card instead. Routed
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

            if (ViewModel.IsOpen.Value)
            {
                // The wheel reorders the hand from anywhere on the album, not only while the cursor
                // rests on the fan - the order is the whole point of the screen, so it stays to
                // hand however far the cursor has wandered. The room owns the wheel only while the
                // album is shut (this view holds the world lock while it is open), so the two never
                // both act on one notch.
                HandleHandScroll();

                // Escape only ever closes. Bound the other way round it would fight every other
                // panel that wants the same key to back out of itself. Refused once the endgame has
                // sealed the album.
                if (keyboard.escapeKey.wasPressedThisFrame)
                    TryClose();
            }
        }

        /// <summary>
        /// Turns one notch of the wheel into one card carried across the fan, read straight off the
        /// mouse rather than from a pointer-over event so it works with the cursor anywhere on the
        /// screen.
        /// </summary>
        private void HandleHandScroll()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || ViewModel.Hand == null)
                return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < ScrollDeadzone)
                return;

            // A wheel notch over a list that scrolls itself - the set list down the side - belongs
            // to that list. Cycling the hand as well would have the wheel doing two things at once,
            // which is the irritating part, so when the cursor is over any ScrollRect the hand
            // keeps still and the list scrolls alone.
            if (IsPointerOverScrollRect(mouse.position.ReadValue()))
                return;

            ViewModel.Hand.Cycle(scroll > 0f ? 1 : -1);
        }

        /// <summary>
        /// Whether the cursor is over a <see cref="ScrollRect"/> - the set list, or anything else
        /// that scrolls. Read off the same UI raycast the EventSystem uses to deliver the scroll,
        /// so the hand stands aside exactly when a list would take the wheel.
        /// </summary>
        private bool IsPointerOverScrollRect(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            _scrollRaycast.Clear();
            eventSystem.RaycastAll(pointer, _scrollRaycast);

            foreach (RaycastResult result in _scrollRaycast)
            {
                if (result.gameObject != null &&
                    result.gameObject.GetComponentInParent<ScrollRect>() != null)
                    return true;
            }

            return false;
        }

        /// <summary>Opens the album if it is shut and shuts it if it is open - the B key and the
        /// HUD's album button both land here.</summary>
        private void Toggle()
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsOpen.Value)
                TryClose();
            else
                ViewModel.Open();
        }

        /// <summary>
        /// Closes the album unless the endgame has sealed it. Once the final card is filed the album
        /// can no longer be shut - the closing sequence is playing and the ending takes over from
        /// there - so every way of closing routes through here.
        /// </summary>
        private void TryClose()
        {
            if (finalLayout != null && finalLayout.IsSealed)
                return;

            // The menu's album is a view in its own right rather than a panel the room keeps
            // around, so closing it means it is gone. Guarded because the close animation gives
            // Escape a few more frames in which it would otherwise be pressed again.
            if (_readOnly)
            {
                if (_isUnloading)
                    return;

                _isUnloading = true;
                Unload().Forget();
                return;
            }

            ViewModel.Close();
        }

        /// <summary>
        /// Swaps the album between its normal layout and its endgame layout for this open, by whether
        /// the player is holding the final card. The endgame layout reuses the shared drag and
        /// inspector, so its one slot files exactly like a page slot.
        /// </summary>
        private void ApplyEndgameMode()
        {
            if (finalLayout == null)
            {
                // No closing spread authored, so the normal album is the only thing there is.
                if (normalLayout != null)
                    normalLayout.SetActive(true);

                return;
            }

            // Two ways into the closing spread. In the room it is the player walking in still
            // holding the final card, with the ending ahead of them. From the menu it is the same
            // spread kept as a memento of an ending that has already happened - so it comes up
            // finished, without replaying itself, and offers a way through to the collection.
            bool completed = _readOnly && ViewModel.IsEndgameCardFiled;
            bool endgame = completed || (!_readOnly && ViewModel.HoldsEndgameCard);

            if (normalLayout != null)
                normalLayout.SetActive(!endgame);

            finalLayout.gameObject.SetActive(endgame);

            if (!endgame)
                return;

            if (completed)
            {
                CardRef finalCard = ViewModel.EndgameCard;

                finalLayout.ShowCompleted(
                    drag, inspector, ViewModel.EndgameSet, finalCard,
                    ViewModel.Artwork.Resolve(finalCard), _playerStats, ShowCollection);
            }
            else
            {
                finalLayout.Initialize(drag, inspector, ViewModel.EndgameSet, _playerStats);
            }
        }

        /// <summary>
        /// Shuts the finished album's closing spread and lets the player through to the collection
        /// behind it. Wired to the See Collection button, which only the menu's album has.
        /// </summary>
        private void ShowCollection()
        {
            if (finalLayout != null)
                finalLayout.gameObject.SetActive(false);

            if (normalLayout != null)
                normalLayout.SetActive(true);

            // The set list has been off screen the whole time, so nothing has ever measured it.
            // Bringing the open set into view has to wait until it is actually up.
            ScrollToOpenSet();
        }

        /// <summary>
        /// The card close-up's controls, the same as the room's inspector so a card reads the
        /// same however it was opened: the right button and Escape close it, the Flip Card action
        /// turns it over, and the left button flips on-card or leaves when it lands off-card.
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

            // The rebindable flip action works wherever the cursor is.
            if (_flipCardAction != null && _flipCardAction.WasPressedThisFrame()
                && !inspector.JustOpened)
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

                // The endgame set sits outside the collection and gets no category here - it is
                // reached only through its own special album state, never the normal set list.
                if (!set.CountsTowardCollection)
                    continue;

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

            if (closeButton != null)
            {
                // The room's album is closed with the key that opened it or the HUD button that
                // did; only the menu's, which has neither, needs one of its own.
                closeButton.gameObject.SetActive(_readOnly);

                // Routed through TryClose like every other way out, so the endgame seal refuses
                // it too.
                closeButton.Bind(TryClose);
            }

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
            ScrollToOpenSet();
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

        /// <summary>
        /// Brings the open set's button into view in the category list.
        ///
        /// The list is long enough to scroll and keeps wherever it was left, so a set reached with
        /// the wheel, with Smart Album Open, or simply left open from the last time would otherwise
        /// come back with the glow sitting somewhere off screen. Called when the album opens and
        /// whenever a set is opened from outside the list.
        /// </summary>
        private void ScrollToOpenSet()
        {
            ScrollRect scroll = SetButtonsScroll;
            if (scroll == null || _openSet == null || !_openSet.gameObject.activeInHierarchy)
                return;

            RectTransform content = scroll.content;
            RectTransform viewport = scroll.viewport != null
                ? scroll.viewport
                : scroll.transform as RectTransform;

            if (content == null || viewport == null)
                return;

            // The album is shown and hidden rather than rebuilt, so on the first open the list has
            // never been laid out and every rect still reads as zero. Settle it before measuring.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            float scrollable = content.rect.height - viewport.rect.height;
            if (scrollable <= 0f)
                return;

            // How far the button's middle sits below the top of the content, turned into the
            // normalized position that puts it in the middle of the window. Clamped, so the sets at
            // either end simply pin the list to that end instead of scrolling past it.
            var button = (RectTransform)_openSet.transform;
            Vector3 centre = button.TransformPoint(button.rect.center);
            float belowTop = content.rect.yMax - content.InverseTransformPoint(centre).y;
            float offset = belowTop - viewport.rect.height * 0.5f;

            // Any glide left over from the player's own scrolling would drag the list straight back
            // off the set we just brought up.
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / scrollable);
        }

        /// <summary>
        /// The set list's scroll view: the authored one, or the one the buttons container sits in.
        /// Found once and kept, so nothing has to be wired for the list to follow the open set.
        /// </summary>
        private ScrollRect SetButtonsScroll
        {
            get
            {
                if (setButtonsScroll != null)
                    return setButtonsScroll;

                if (_resolvedSetButtonsScroll == null && setButtonsContainer != null)
                {
                    _resolvedSetButtonsScroll =
                        setButtonsContainer.GetComponentInParent<ScrollRect>(includeInactive: true);
                }

                return _resolvedSetButtonsScroll;
            }
        }

        /// <summary>
        /// Re-reads the album into the set counters and the open set's slots. Cheap, and only run
        /// when the album opens, so it fixes the "first built before the save loaded" case without
        /// resetting which page the player was on.
        /// </summary>
        private void RefreshAlbumDisplay()
        {
            foreach (AlbumSetButton button in _setButtons)
            {
                if (button != null && button.Set != null)
                    button.SetProgress(ViewModel.CountFiled(button.Set.SetId));
            }

            if (_openSet != null)
            {
                pages.RefreshAllSlots();
                SetCollectionProgress(_openSet.Set, punch: false);
            }

            RefreshSetPulses();
        }

        /// <summary>
        /// States, for every set button, whether it should be breathing. Re-asserted wholesale
        /// rather than tracked per card: the answer is one walk of the hand per set, the buttons
        /// ignore an instruction they are already following, and there is nothing here to fall out
        /// of step with the hand.
        /// </summary>
        private void RefreshSetPulses()
        {
            foreach (AlbumSetButton button in _setButtons)
            {
                if (button != null && button.Set != null)
                    button.SetPulsing(ViewModel.ShouldPulseSet(button.Set.SetId));
            }
        }

        private void StopSetPulses()
        {
            foreach (AlbumSetButton button in _setButtons)
            {
                if (button != null)
                    button.SetPulsing(false);
            }
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
            // The display case is put up by whoever added the view and taken away by unloading it,
            // so showing and hiding from here as well would race that and play the open animation
            // twice. Its own setup is done once, in OpenAsDisplayCase.
            if (_readOnly)
                return;

            // UI Images are not camera-driven mip-streaming renderers. Explicitly keep only the
            // current album page sharp while it is on screen, and release it as soon as the album
            // closes so the world can use the budget.
            if (pages != null)
                pages.SetFullResolutionEnabled(isOpen);

            if (isOpen)
            {
                AudioService.Play(AudioSFXKey.AlbumOpen);

                // Read the album afresh on every open. The view is built once at scene start, which
                // can be before the async save load finishes, so the slots and counters it showed
                // then may be stale-empty; opening always happens long after the save is in.
                RefreshAlbumDisplay();

                // Holding the final card opens the album straight into its endgame state instead of
                // the normal layout. Re-evaluated on every open, so a normal open stays normal.
                ApplyEndgameMode();

                // After the endgame swap, so the list is only measured while it is actually up.
                ScrollToOpenSet();

                Show(destroyCancellationToken).Forget();
            }
            else
            {
                // A card left open when the album goes would come back both stale and, worse,
                // still holding the input the next time the album opened.
                if (inspector != null)
                    inspector.Close();

                // Nothing should be left breathing behind a closed album; the next open re-states
                // every button anyway.
                StopSetPulses();

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
            {
                ViewModel.AlbumChanged -= OnAlbumChanged;

                if (ViewModel.Hand != null)
                    ViewModel.Hand.Changed -= RefreshSetPulses;
            }

            if (_albumFocus != null)
                _albumFocus.OpenRequested -= OnAlbumFocusRequested;

            if (_panels != null)
                _panels.AlbumToggleRequested -= Toggle;

            base.OnDestroy();
        }
    }
}
