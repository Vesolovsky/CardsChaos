using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.Services.Settings;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// Authored Settings UI over two transactional drafts: regular settings and Input System
    /// binding overrides. Nothing in the running game changes until Apply is pressed.
    /// </summary>
    public class SettingsView : View<ISettingsViewModel>
    {
        private static readonly FullScreenMode[] DisplayModes =
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.Windowed,
        };

        private static readonly string[] DisplayModeLabels =
        {
            "Fullscreen (Borderless)",
            "Fullscreen (Exclusive)",
            "Windowed",
        };

        private static readonly int[] FpsLimits = { -1, 30, 60, 120, 144, 165, 240 };

        [Header("Tabs")]
        [SerializeField] private SettingsTabButton generalTab;
        [SerializeField] private SettingsTabButton videoTab;
        [SerializeField] private SettingsTabButton inputTab;
        [SerializeField] private SettingsTabButton audioTab;

        [Tooltip("The single ScrollRect wrapping the tab body; its Content follows the active tab.")]
        [SerializeField] private ScrollRect settingsScrollRect;

        [Header("General")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private VText mouseSensitivityValueText;
        [SerializeField] private Toggle invertMouseXToggle;
        [SerializeField] private VText invertMouseXValueText;
        [SerializeField] private Toggle autoSaveToggle;
        [SerializeField] private VText autoSaveValueText;
        [SerializeField] private Toggle showHintsToggle;
        [SerializeField] private VText showHintsValueText;
        [SerializeField] private VButton generalResetButton;

        [Header("Input")]
        [SerializeField] private KeyBindEntry[] keyBindEntries;
        [SerializeField] private VButton inputResetButton;

        [Header("Video")]
        [SerializeField] private GameObject qualityEntry;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private GameObject resolutionEntry;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private VText vSyncValueText;
        [SerializeField] private GameObject fpsTargetEntry;
        [SerializeField] private TMP_Dropdown fpsLimitDropdown;
        [SerializeField] private VButton videoResetButton;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private VText masterVolumeValueText;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private VText musicVolumeValueText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private VText sfxVolumeValueText;
        [SerializeField] private VButton audioResetButton;

        [Header("Bottom bar")]
        [SerializeField] private VButton applyButton;
        [SerializeField] private VButton closeButton;

        private readonly List<ResolutionOption> _resolutions = new List<ResolutionOption>();

        private DynamicViewsCanvas _dynamicViewsCanvas;
        private IView _activePopup;
        private KeyBindEntry _pendingRebindEntry;
        private bool _listenersBound;
        private bool _isClosing;
        private bool _popupFlowBusy;
        private int _popupRequestVersion;

        [Inject]
        private void InjectSettings(DynamicViewsCanvas dynamicViewsCanvas)
        {
            _dynamicViewsCanvas = dynamicViewsCanvas;
        }

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            BuildVideoOptions();
            BindControls();
            RefreshAllControls();
            SelectTab(0);
        }

        private void BindControls()
        {
            generalTab?.Bind(() => SelectTab(0));
            videoTab?.Bind(() => SelectTab(1));
            inputTab?.Bind(() => SelectTab(2));
            audioTab?.Bind(() => SelectTab(3));

            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.minValue = GameSettingsData.MIN_MOUSE_SENSITIVITY;
                mouseSensitivitySlider.maxValue = GameSettingsData.MAX_MOUSE_SENSITIVITY;
                mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            }

            invertMouseXToggle?.onValueChanged.AddListener(OnInvertMouseXChanged);
            autoSaveToggle?.onValueChanged.AddListener(OnAutoSaveChanged);
            showHintsToggle?.onValueChanged.AddListener(OnShowHintsChanged);

            generalResetButton?.Bind(OnResetGeneral);

            if (keyBindEntries != null)
            {
                foreach (KeyBindEntry entry in keyBindEntries)
                {
                    if (entry != null)
                        entry.Bind(OnRebindRequested, OnSingleBindResetRequested);
                }
            }

            inputResetButton?.Bind(OnResetInput);

            qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            displayModeDropdown?.onValueChanged.AddListener(OnDisplayModeChanged);
            resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
            vSyncToggle?.onValueChanged.AddListener(OnVSyncChanged);
            fpsLimitDropdown?.onValueChanged.AddListener(OnFpsLimitChanged);
            videoResetButton?.Bind(OnResetVideo);

            PrepareVolumeSlider(masterVolumeSlider, OnMasterVolumeChanged);
            PrepareVolumeSlider(musicVolumeSlider, OnMusicVolumeChanged);
            PrepareVolumeSlider(sfxVolumeSlider, OnSfxVolumeChanged);
            audioResetButton?.Bind(OnResetAudio);

            applyButton?.Bind(OnApply);
            closeButton?.Bind(OnClose);

            // Sliders and dropdowns are plain Unity controls with no sound of their own; give them
            // the same hover/click feedback VButton has built in. The buttons above already carry it.
            // Sliders hover off the handle only, so sweeping the cursor along the track stays quiet;
            // dropdowns sound off the whole control.
            AddSliderAudio(mouseSensitivitySlider);
            AddSliderAudio(masterVolumeSlider);
            AddSliderAudio(musicVolumeSlider);
            AddSliderAudio(sfxVolumeSlider);
            AddControlAudio(qualityDropdown);
            AddControlAudio(displayModeDropdown);
            AddControlAudio(resolutionDropdown);
            AddControlAudio(fpsLimitDropdown);

            if (ViewModel.InputDraft != null)
                ViewModel.InputDraft.BindingChanged += OnDraftBindingChanged;

            _listenersBound = true;
        }

        /// <summary>
        /// Attaches the shared pointer hover/click sound to a control at runtime, so the settings
        /// prefab does not have to carry the component on every slider and dropdown by hand.
        /// </summary>
        /// <summary>Hover + click sound on the whole control - fine for compact controls like dropdowns.</summary>
        private void AddControlAudio(Component control)
        {
            if (control == null)
                return;

            control.gameObject.AddComponent<PointerHoverAudio>().Initialize(AudioService);
            control.gameObject.AddComponent<PointerClickAudio>().Initialize(AudioService);
        }

        /// <summary>
        /// Sounds a slider without the whole track answering the cursor: the hover lives on the
        /// grabbable handle alone, while the click sits on the slider root - next to the Slider
        /// itself, where both pointer-down handlers run - rather than on the handle, where it would
        /// swallow the press the Slider needs to start a drag.
        /// </summary>
        private void AddSliderAudio(Slider slider)
        {
            if (slider == null)
                return;

            RectTransform handle = slider.handleRect;
            if (handle != null)
            {
                // The handle must be a raycast target to receive the hover; a handle you can grab
                // normally already is, but assert it so the hover cannot silently miss.
                if (handle.TryGetComponent(out Graphic handleGraphic))
                    handleGraphic.raycastTarget = true;

                handle.gameObject.AddComponent<PointerHoverAudio>().Initialize(AudioService);
            }

            slider.gameObject.AddComponent<PointerClickAudio>().Initialize(AudioService);
        }

        private static void PrepareVolumeSlider(Slider slider, UnityEngine.Events.UnityAction<float> listener)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(listener);
        }

        private void SelectTab(int index)
        {
            generalTab?.SetSelected(index == 0);
            videoTab?.SetSelected(index == 1);
            inputTab?.SetSelected(index == 2);
            audioTab?.SetSelected(index == 3);

            // All tabs share one ScrollRect and viewport; SetSelected only toggles which group is
            // visible, so the ScrollRect's Content must be pointed at that group too - otherwise the
            // scrollbar keeps sizing itself against whichever group it was authored with.
            SettingsTabButton activeTab =
                index == 0 ? generalTab :
                index == 1 ? videoTab :
                index == 2 ? inputTab :
                index == 3 ? audioTab : null;

            BindScrollContentToActiveTab(activeTab);
        }

        private void BindScrollContentToActiveTab(SettingsTabButton tab)
        {
            if (settingsScrollRect == null || tab == null)
                return;

            RectTransform content = tab.ContentRect;
            if (content == null)
                return;

            settingsScrollRect.content = content;

            // Rebuild first so the new group's height is current when the ScrollRect sizes the
            // handle, then start at the top instead of inheriting the previous tab's scroll offset.
            Canvas.ForceUpdateCanvases();
            settingsScrollRect.verticalNormalizedPosition = 1f;
        }

        #region General

        private void OnMouseSensitivityChanged(float value)
        {
            ViewModel.Draft.MouseSensitivity = Mathf.Clamp(
                value,
                GameSettingsData.MIN_MOUSE_SENSITIVITY,
                GameSettingsData.MAX_MOUSE_SENSITIVITY);
            RefreshSensitivityText();
        }

        private void OnInvertMouseXChanged(bool value)
        {
            ViewModel.Draft.InvertMouseX = value;
            SetToggleText(invertMouseXValueText, value);
        }

        private void OnAutoSaveChanged(bool value)
        {
            ViewModel.Draft.AutoSave = value;
            SetToggleText(autoSaveValueText, value);
        }

        private void OnShowHintsChanged(bool value)
        {
            ViewModel.Draft.ShowHints = value;
            SetToggleText(showHintsValueText, value);
        }

        private void OnResetGeneral()
        {
            ViewModel.ResetGeneral();
            RefreshGeneralControls();
        }

        private void RefreshGeneralControls()
        {
            GameSettingsData draft = ViewModel.Draft;

            mouseSensitivitySlider?.SetValueWithoutNotify(draft.MouseSensitivity);
            invertMouseXToggle?.SetIsOnWithoutNotify(draft.InvertMouseX);
            autoSaveToggle?.SetIsOnWithoutNotify(draft.AutoSave);
            showHintsToggle?.SetIsOnWithoutNotify(draft.ShowHints);

            RefreshSensitivityText();
            SetToggleText(invertMouseXValueText, draft.InvertMouseX);
            SetToggleText(autoSaveValueText, draft.AutoSave);
            SetToggleText(showHintsValueText, draft.ShowHints);
        }

        private void RefreshSensitivityText()
        {
            mouseSensitivityValueText?.SetText(
                ViewModel.Draft.MouseSensitivity.ToString("F2", CultureInfo.InvariantCulture));
        }

        #endregion

        #region Input

        private void OnRebindRequested(KeyBindEntry entry)
        {
            if (entry == null || ViewModel.InputDraft == null || _isClosing)
                return;

            OpenRebindPrompt(entry).Forget();
        }

        private async UniTask OpenRebindPrompt(KeyBindEntry entry)
        {
            if (!TryBeginPopupFlow())
                return;

            try
            {
                _pendingRebindEntry = entry;
                await CloseActivePopup(immediately: true);

                if (_isClosing || this == null)
                    return;

                IView popup = await ShowPopup(
                    $"Rebind {entry.DisplayName}",
                    $"Press any button to rebind {entry.DisplayName}. Press Escape to cancel.",
                    ConfirmationPopupButtons.None);

                if (popup == null)
                    return;

                // VButton invokes on pointer-up, but waiting one player-loop iteration also protects
                // keyboard-triggered UI clicks from becoming the candidate themselves.
                await UniTask.Yield();

                if (_isClosing || this == null || _pendingRebindEntry != entry)
                    return;

                ViewModel.InputDraft.BeginCapture(
                    entry.ActionName,
                    path => OnRebindCandidate(entry, path).Forget(),
                    OnRebindCanceled,
                    error => OnRebindFailed(error).Forget());
            }
            finally
            {
                _popupFlowBusy = false;
            }
        }

        private async UniTask OnRebindCandidate(KeyBindEntry entry, string candidatePath)
        {
            if (_isClosing || ViewModel.InputDraft == null)
                return;

            if (!TryBeginPopupFlow())
                return;

            try
            {
                RebindConflictInfo conflict = ViewModel.InputDraft.FindConflict(entry.ActionName, candidatePath);
                if (!conflict.HasConflict)
                {
                    ViewModel.InputDraft.CommitCandidate(entry.ActionName, candidatePath);
                    _pendingRebindEntry = null;
                    await CloseActivePopup();
                    return;
                }

                await CloseActivePopup();
                await ShowConflictPopup(entry, conflict, resumeCaptureOnCancel: true);
            }
            finally
            {
                _popupFlowBusy = false;
            }
        }

        private void OnRebindCanceled()
        {
            if (_isClosing)
                return;

            _pendingRebindEntry = null;
            CloseActivePopup().Forget();
        }

        private async UniTask OnRebindFailed(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
                Debug.LogWarning($"[{nameof(SettingsView)}] Rebinding failed: {error}", this);

            _pendingRebindEntry = null;
            await CloseActivePopup();
        }

        private void OnSingleBindResetRequested(KeyBindEntry entry)
        {
            if (entry == null || ViewModel.InputDraft == null || _isClosing)
                return;

            RebindConflictInfo conflict = ViewModel.InputDraft.Reset(entry.ActionName);
            if (conflict.HasConflict)
                ShowResetConflict(entry, conflict).Forget();
        }

        private async UniTask ShowResetConflict(KeyBindEntry entry, RebindConflictInfo conflict)
        {
            if (!TryBeginPopupFlow())
                return;

            try
            {
                await CloseActivePopup(immediately: true);
                await ShowConflictPopup(entry, conflict, resumeCaptureOnCancel: false);
            }
            finally
            {
                _popupFlowBusy = false;
            }
        }

        private async UniTask ShowConflictPopup(
            KeyBindEntry entry,
            RebindConflictInfo conflict,
            bool resumeCaptureOnCancel)
        {
            string conflictingName = FindInputEntry(conflict.ConflictingActionName)?.DisplayName
                                     ?? conflict.ConflictingActionName;

            await ShowPopup(
                "Binding conflict",
                $"{conflict.CandidateDisplay} is already assigned to {conflictingName}. " +
                "Are you sure you want to override it?",
                ConfirmationPopupButtons.ConfirmAndDecline,
                confirmAction: () =>
                {
                    _activePopup = null; // The popup unloads itself after this callback.
                    ViewModel.InputDraft.CommitCandidate(entry.ActionName, conflict.CandidatePath);
                    _pendingRebindEntry = null;
                },
                declineAction: () =>
                {
                    _activePopup = null; // The popup unloads itself after this callback.
                    if (resumeCaptureOnCancel)
                        ResumeRebindAfterConflict(entry).Forget();
                    else
                        _pendingRebindEntry = null;
                });
        }

        private async UniTask ResumeRebindAfterConflict(KeyBindEntry entry)
        {
            // Let the conflict popup process its own Unload before placing the waiting popup under
            // the same DynamicViewsCanvas.
            await UniTask.Yield();
            if (!_isClosing && this != null)
                await OpenRebindPrompt(entry);
        }

        private void OnResetInput()
        {
            ViewModel.ResetInput();
            RefreshInputControls();
        }

        private void OnDraftBindingChanged(string actionName)
        {
            KeyBindEntry entry = FindInputEntry(actionName);
            if (entry != null)
                entry.SetBindingText(ViewModel.InputDraft.GetDisplay(actionName));
        }

        private void RefreshInputControls()
        {
            if (keyBindEntries == null || ViewModel.InputDraft == null)
                return;

            foreach (KeyBindEntry entry in keyBindEntries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ActionName))
                    continue;

                try
                {
                    entry.SetBindingText(ViewModel.InputDraft.GetDisplay(entry.ActionName));
                }
                catch (ArgumentException exception)
                {
                    Debug.LogError($"[{nameof(SettingsView)}] {exception.Message}", entry);
                    entry.SetBindingText("-");
                }
            }
        }

        private KeyBindEntry FindInputEntry(string actionName)
        {
            if (keyBindEntries == null)
                return null;

            foreach (KeyBindEntry entry in keyBindEntries)
            {
                if (entry != null && string.Equals(
                        entry.ActionName,
                        actionName,
                        StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        #endregion

        #region Video

        private void BuildVideoOptions()
        {
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            }

            if (displayModeDropdown != null)
            {
                displayModeDropdown.ClearOptions();
                displayModeDropdown.AddOptions(new List<string>(DisplayModeLabels));
            }

            BuildResolutionOptions();

            if (fpsLimitDropdown != null)
            {
                var labels = new List<string>(FpsLimits.Length);
                foreach (int limit in FpsLimits)
                    labels.Add(limit < 0 ? "Unlimited" : limit.ToString());

                fpsLimitDropdown.ClearOptions();
                fpsLimitDropdown.AddOptions(labels);
            }
        }

        private void BuildResolutionOptions()
        {
            _resolutions.Clear();
            var seen = new HashSet<string>();

            Resolution[] available = Screen.resolutions;
            if (available != null)
            {
                foreach (Resolution resolution in available)
                {
                    int refreshRate = Mathf.Max(1,
                        Mathf.RoundToInt((float)resolution.refreshRateRatio.value));
                    string key = $"{resolution.width}x{resolution.height}@{refreshRate}";
                    if (seen.Add(key))
                        _resolutions.Add(new ResolutionOption(
                            resolution.width,
                            resolution.height,
                            refreshRate));
                }
            }

            if (_resolutions.Count == 0)
            {
                Resolution current = Screen.currentResolution;
                _resolutions.Add(new ResolutionOption(
                    Mathf.Max(1, current.width),
                    Mathf.Max(1, current.height),
                    Mathf.Max(1, Mathf.RoundToInt((float)current.refreshRateRatio.value))));
            }

            _resolutions.Sort(ResolutionOption.Compare);

            if (resolutionDropdown != null)
            {
                var labels = new List<string>(_resolutions.Count);
                foreach (ResolutionOption option in _resolutions)
                    labels.Add(option.Label);

                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(labels);
            }
        }

        private void OnQualityChanged(int index)
        {
            AudioService.Play(AudioSFXKey.ButtonClick);

            if (QualitySettings.names.Length == 0)
                return;

            ViewModel.Draft.QualityLevel = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        }

        private void OnDisplayModeChanged(int index)
        {
            AudioService.Play(AudioSFXKey.ButtonClick);

            if (index < 0 || index >= DisplayModes.Length)
                return;

            ViewModel.Draft.FullScreenMode = DisplayModes[index];

            if (ViewModel.Draft.FullScreenMode == FullScreenMode.FullScreenWindow)
            {
                GameSettingsData defaults = GameSettingsData.CreateDefaults();
                ViewModel.Draft.ResolutionWidth = defaults.ResolutionWidth;
                ViewModel.Draft.ResolutionHeight = defaults.ResolutionHeight;
                ViewModel.Draft.RefreshRate = defaults.RefreshRate;
            }

            RefreshVideoAvailability();
            RefreshResolutionSelection();
        }

        private void OnResolutionChanged(int index)
        {
            AudioService.Play(AudioSFXKey.ButtonClick);

            if (index < 0 || index >= _resolutions.Count)
                return;

            ResolutionOption resolution = _resolutions[index];
            ViewModel.Draft.ResolutionWidth = resolution.Width;
            ViewModel.Draft.ResolutionHeight = resolution.Height;
            ViewModel.Draft.RefreshRate = resolution.RefreshRate;
        }

        private void OnVSyncChanged(bool enabled)
        {
            ViewModel.Draft.VSync = enabled;
            SetToggleText(vSyncValueText, enabled);
            RefreshVideoAvailability();
        }

        private void OnFpsLimitChanged(int index)
        {
            AudioService.Play(AudioSFXKey.ButtonClick);

            if (index >= 0 && index < FpsLimits.Length)
                ViewModel.Draft.FpsLimit = FpsLimits[index];
        }

        private void OnResetVideo()
        {
            ViewModel.ResetVideo();
            RefreshVideoControls();
        }

        private void RefreshVideoControls()
        {
            GameSettingsData draft = ViewModel.Draft;

            qualityDropdown?.SetValueWithoutNotify(Mathf.Clamp(
                draft.QualityLevel,
                0,
                Mathf.Max(0, QualitySettings.names.Length - 1)));
            displayModeDropdown?.SetValueWithoutNotify(IndexOfDisplayMode(draft.FullScreenMode));
            RefreshResolutionSelection();
            vSyncToggle?.SetIsOnWithoutNotify(draft.VSync);
            SetToggleText(vSyncValueText, draft.VSync);
            fpsLimitDropdown?.SetValueWithoutNotify(IndexOfFpsLimit(draft.FpsLimit));

            RefreshVideoAvailability();
        }

        private void RefreshResolutionSelection()
        {
            GameSettingsData draft = ViewModel.Draft;
            int index = 0;

            for (int i = 0; i < _resolutions.Count; i++)
            {
                ResolutionOption option = _resolutions[i];
                if (option.Width == draft.ResolutionWidth &&
                    option.Height == draft.ResolutionHeight &&
                    option.RefreshRate == draft.RefreshRate)
                {
                    index = i;
                    break;
                }
            }

            resolutionDropdown?.SetValueWithoutNotify(index);
        }

        private void RefreshVideoAvailability()
        {
            if (qualityEntry != null)
                qualityEntry.SetActive(QualitySettings.names.Length > 1);

            if (resolutionEntry != null)
            {
                bool resolutionCanBeChanged =
                    ViewModel.Draft.FullScreenMode != FullScreenMode.FullScreenWindow &&
                    _resolutions.Count > 0;
                resolutionEntry.SetActive(resolutionCanBeChanged);
            }

            if (fpsTargetEntry != null)
                fpsTargetEntry.SetActive(!ViewModel.Draft.VSync);
        }

        private static int IndexOfDisplayMode(FullScreenMode mode)
        {
            for (int i = 0; i < DisplayModes.Length; i++)
            {
                if (DisplayModes[i] == mode)
                    return i;
            }

            return 0;
        }

        private static int IndexOfFpsLimit(int limit)
        {
            for (int i = 0; i < FpsLimits.Length; i++)
            {
                if (FpsLimits[i] == limit)
                    return i;
            }

            return 0;
        }

        #endregion

        #region Audio

        private void OnMasterVolumeChanged(float value)
        {
            ViewModel.Draft.MasterVolume = Mathf.Clamp01(value);
            SetVolumeText(masterVolumeValueText, ViewModel.Draft.MasterVolume);
        }

        private void OnMusicVolumeChanged(float value)
        {
            ViewModel.Draft.MusicVolume = Mathf.Clamp01(value);
            SetVolumeText(musicVolumeValueText, ViewModel.Draft.MusicVolume);
        }

        private void OnSfxVolumeChanged(float value)
        {
            ViewModel.Draft.SfxVolume = Mathf.Clamp01(value);
            SetVolumeText(sfxVolumeValueText, ViewModel.Draft.SfxVolume);
        }

        private void OnResetAudio()
        {
            ViewModel.ResetAudio();
            RefreshAudioControls();
        }

        private void RefreshAudioControls()
        {
            GameSettingsData draft = ViewModel.Draft;

            masterVolumeSlider?.SetValueWithoutNotify(draft.MasterVolume);
            musicVolumeSlider?.SetValueWithoutNotify(draft.MusicVolume);
            sfxVolumeSlider?.SetValueWithoutNotify(draft.SfxVolume);

            SetVolumeText(masterVolumeValueText, draft.MasterVolume);
            SetVolumeText(musicVolumeValueText, draft.MusicVolume);
            SetVolumeText(sfxVolumeValueText, draft.SfxVolume);
        }

        private static void SetVolumeText(VText text, float normalizedValue)
        {
            text?.SetText($"{Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f)}%");
        }

        #endregion

        private void OnApply()
        {
            ViewModel.Apply();

            // Apply can sanitize monitor-specific values. Re-read the accepted snapshot so the UI
            // always shows precisely what the runtime stored.
            BuildResolutionOptions();
            RefreshAllControls();

            ShowAppliedConfirmation().Forget();
        }

        private async UniTask ShowAppliedConfirmation()
        {
            await ShowPopup(
                "Settings saved",
                "Your changes have been applied and saved.",
                ConfirmationPopupButtons.Confirm,
                confirmAction: () => _activePopup = null); // The popup unloads itself after this.
        }

        private void OnClose()
        {
            if (_isClosing)
                return;

            // Closing with pending edits would silently drop them, so make the player confirm first.
            if (ViewModel.HasUnsavedChanges)
            {
                ConfirmDiscardAndClose().Forget();
                return;
            }

            CloseImmediately();
        }

        private async UniTask ConfirmDiscardAndClose()
        {
            await ShowPopup(
                "Unsaved changes",
                "You have unsaved changes. Are you sure you want to leave without saving them?",
                ConfirmationPopupButtons.ConfirmAndDecline,
                confirmAction: () =>
                {
                    _activePopup = null; // The popup unloads itself after this callback.
                    CloseImmediately();
                },
                declineAction: () => _activePopup = null); // Stay in settings; only the popup closes.
        }

        private void CloseImmediately()
        {
            if (_isClosing)
                return;

            _isClosing = true;
            _popupRequestVersion++;

            if (ViewModel.InputDraft != null && ViewModel.InputDraft.IsCapturing)
                ViewModel.InputDraft.CancelCapture();

            CloseActivePopup(immediately: true).Forget();
            Unload().Forget();
        }

        private void RefreshAllControls()
        {
            RefreshGeneralControls();
            RefreshInputControls();
            RefreshVideoControls();
            RefreshAudioControls();
        }

        private static void SetToggleText(VText text, bool enabled)
        {
            text?.SetText(enabled ? "On" : "Off");
        }

        private async UniTask<IView> ShowPopup(
            string title,
            string description,
            ConfirmationPopupButtons buttons,
            Action confirmAction = null,
            Action declineAction = null)
        {
            if (_dynamicViewsCanvas == null || _isClosing)
                return null;

            int requestVersion = ++_popupRequestVersion;

            var previouslyLoaded = new HashSet<IView>(SceneViewsService.LoadedViews);
            var definition = new ConfirmationPopupViewDefinition
            {
                ViewModelInitData = new ConfirmationPopupViewModelInitData(
                    title,
                    description,
                    buttons,
                    confirmAction,
                    declineAction)
            };

            await SceneViewsService.AddView(
                definition,
                _dynamicViewsCanvas.transform,
                throughQueue: false);

            IReadOnlyList<IView> loadedViews = SceneViewsService.LoadedViews;
            IView createdPopup = null;
            for (int i = loadedViews.Count - 1; i >= 0; i--)
            {
                IView view = loadedViews[i];
                if (view is ConfirmationPopupView && !previouslyLoaded.Contains(view))
                {
                    createdPopup = view;
                    break;
                }
            }

            if (createdPopup == null)
            {
                Debug.LogError($"[{nameof(SettingsView)}] Confirmation popup was not registered.", this);
                return null;
            }

            if (_isClosing || this == null || requestVersion != _popupRequestVersion)
            {
                await createdPopup.Unload(immediately: true);
                return null;
            }

            _activePopup = createdPopup;
            return createdPopup;
        }

        private bool TryBeginPopupFlow()
        {
            if (_popupFlowBusy || _isClosing)
                return false;

            _popupFlowBusy = true;
            return true;
        }

        private async UniTask CloseActivePopup(bool immediately = false)
        {
            IView popup = _activePopup;
            _activePopup = null;

            if (popup == null)
                return;

            if (popup is UnityEngine.Object unityObject && unityObject == null)
                return;

            await popup.Unload(immediately);
        }

        protected override void OnDestroy()
        {
            _isClosing = true;

            if (ViewModel?.InputDraft != null)
            {
                ViewModel.InputDraft.BindingChanged -= OnDraftBindingChanged;
                if (ViewModel.InputDraft.IsCapturing)
                    ViewModel.InputDraft.CancelCapture();
            }

            if (_listenersBound)
            {
                mouseSensitivitySlider?.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
                invertMouseXToggle?.onValueChanged.RemoveListener(OnInvertMouseXChanged);
                autoSaveToggle?.onValueChanged.RemoveListener(OnAutoSaveChanged);
                showHintsToggle?.onValueChanged.RemoveListener(OnShowHintsChanged);
                qualityDropdown?.onValueChanged.RemoveListener(OnQualityChanged);
                displayModeDropdown?.onValueChanged.RemoveListener(OnDisplayModeChanged);
                resolutionDropdown?.onValueChanged.RemoveListener(OnResolutionChanged);
                vSyncToggle?.onValueChanged.RemoveListener(OnVSyncChanged);
                fpsLimitDropdown?.onValueChanged.RemoveListener(OnFpsLimitChanged);
                masterVolumeSlider?.onValueChanged.RemoveListener(OnMasterVolumeChanged);
                musicVolumeSlider?.onValueChanged.RemoveListener(OnMusicVolumeChanged);
                sfxVolumeSlider?.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            }

            if (_activePopup != null)
                _activePopup.Unload(immediately: true).Forget();

            base.OnDestroy();
        }

        private readonly struct ResolutionOption
        {
            public int Width { get; }
            public int Height { get; }
            public int RefreshRate { get; }
            public string Label => $"{Width} x {Height} @ {RefreshRate} Hz";

            public ResolutionOption(int width, int height, int refreshRate)
            {
                Width = width;
                Height = height;
                RefreshRate = refreshRate;
            }

            public static int Compare(ResolutionOption left, ResolutionOption right)
            {
                int width = left.Width.CompareTo(right.Width);
                if (width != 0)
                    return width;

                int height = left.Height.CompareTo(right.Height);
                return height != 0 ? height : left.RefreshRate.CompareTo(right.RefreshRate);
            }
        }
    }
}
