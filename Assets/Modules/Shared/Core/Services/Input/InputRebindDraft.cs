using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine.InputSystem;

namespace Vesolovsky.Core.Services.Input
{
    /// <summary>A read-only row exposed by <see cref="InputRebindDraft"/>.</summary>
    public sealed class InputRebindDraftEntry
    {
        public string ActionName { get; }
        public Guid BindingId { get; }
        public string DefaultPath { get; }
        public string Path { get; internal set; }

        internal int BindingIndex { get; }

        internal InputRebindDraftEntry(GameInputBindingInfo bindingInfo)
        {
            ActionName = bindingInfo.ActionName;
            BindingId = bindingInfo.BindingId;
            BindingIndex = bindingInfo.BindingIndex;
            DefaultPath = bindingInfo.DefaultPath;
            Path = bindingInfo.EffectivePath;
        }
    }

    /// <summary>Information needed to render a key-conflict confirmation.</summary>
    public readonly struct RebindConflictInfo
    {
        public bool HasConflict { get; }
        public string ActionName { get; }
        public string ConflictingActionName { get; }
        public string CandidatePath { get; }
        public string CandidateDisplay => InputBindingDisplay.Format(CandidatePath);

        internal RebindConflictInfo(
            string actionName,
            string conflictingActionName,
            string candidatePath)
        {
            HasConflict = !string.IsNullOrEmpty(conflictingActionName);
            ActionName = actionName;
            ConflictingActionName = conflictingActionName;
            CandidatePath = candidatePath ?? string.Empty;
        }

        internal static RebindConflictInfo None(string actionName, string candidatePath)
        {
            return new RebindConflictInfo(actionName, null, candidatePath);
        }
    }

    /// <summary>
    /// A transactional snapshot of all ten keyboard-and-mouse bindings. Capturing and editing only
    /// changes this object; the live input asset and PlayerPrefs remain untouched until Apply().
    /// </summary>
    public sealed class InputRebindDraft : IDisposable
    {
        private readonly IInputActions _inputActions;
        private readonly List<InputRebindDraftEntry> _entries =
            new List<InputRebindDraftEntry>();
        private readonly ReadOnlyCollection<InputRebindDraftEntry> _readOnlyEntries;

        // Snapshot of the paths as of the last applied state; the yardstick for IsDirty.
        private readonly Dictionary<Guid, string> _baselinePaths = new Dictionary<Guid, string>();

        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private InputAction _captureAction;
        private bool _captureActionWasEnabled;
        private string _capturedPath;
        private Action<string> _onCandidate;
        private Action _onCanceled;
        private Action<string> _onFailed;
        private bool _disposed;

        public event Action<string> BindingChanged;

        public IReadOnlyList<InputRebindDraftEntry> Entries => _readOnlyEntries;
        public bool IsCapturing => _rebindOperation != null;

        public InputRebindDraft(IInputActions inputActions)
        {
            _inputActions = inputActions ?? throw new ArgumentNullException(nameof(inputActions));
            _readOnlyEntries = _entries.AsReadOnly();

            IReadOnlyList<GameInputBindingInfo> liveBindings = inputActions.RebindableBindings;
            for (int i = 0; i < liveBindings.Count; i++)
                _entries.Add(new InputRebindDraftEntry(liveBindings[i]));

            CaptureBaseline();
        }

        /// <summary>
        /// True when any binding differs from the last applied state. The baseline is the live input
        /// asset as of construction and is refreshed by <see cref="Apply"/>, so this reads false again
        /// right after a successful apply.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    InputRebindDraftEntry entry = _entries[i];
                    _baselinePaths.TryGetValue(entry.BindingId, out string baseline);
                    if (!string.Equals(
                            entry.Path ?? string.Empty,
                            baseline ?? string.Empty,
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }

        private void CaptureBaseline()
        {
            _baselinePaths.Clear();
            for (int i = 0; i < _entries.Count; i++)
                _baselinePaths[_entries[i].BindingId] = _entries[i].Path;
        }

        public string GetDisplay(string actionName)
        {
            return InputBindingDisplay.Format(GetEntry(actionName).Path);
        }

        public string GetPath(string actionName)
        {
            return GetEntry(actionName).Path;
        }

        /// <summary>
        /// Captures a candidate but does not mutate the draft. The caller should first call
        /// FindConflict() and then CommitCandidate() immediately or after confirmation. Start this
        /// method after the UI click which opened the prompt has been released (for example after one
        /// UniTask.Yield), otherwise that click itself can become the candidate.
        /// </summary>
        public void BeginCapture(
            string actionName,
            Action<string> onCandidate,
            Action onCanceled,
            Action<string> onFailed = null)
        {
            ThrowIfDisposed();
            if (onCandidate == null)
                throw new ArgumentNullException(nameof(onCandidate));

            if (IsCapturing)
                CancelCapture();

            InputRebindDraftEntry entry = GetEntry(actionName);
            InputAction action = _inputActions.Find(actionName);
            if (action == null)
            {
                onFailed?.Invoke($"Input action '{actionName}' does not exist.");
                return;
            }

            _capturedPath = null;
            _onCandidate = onCandidate;
            _onCanceled = onCanceled;
            _onFailed = onFailed;
            _captureAction = action;
            _captureActionWasEnabled = action.enabled;

            if (_captureActionWasEnabled)
                action.Disable();

            try
            {
                _rebindOperation = action
                    .PerformInteractiveRebinding(entry.BindingIndex)
                    .WithControlsHavingToMatchPath("<Keyboard>")
                    .WithControlsHavingToMatchPath("<Mouse>")
                    .WithControlsExcluding("<Gamepad>")
                    .WithControlsExcluding("<Pointer>/position")
                    .WithControlsExcluding("<Pointer>/delta")
                    .WithControlsExcluding("<Mouse>/scroll")
                    .WithControlsExcluding("<Mouse>/clickCount")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .OnApplyBinding((_, path) => _capturedPath = path)
                    .OnCancel(_ => CompleteCanceled())
                    .OnComplete(_ => CompleteCandidate());

                _rebindOperation.Start();
            }
            catch (Exception exception)
            {
                Action<string> failed = _onFailed;
                CleanupCapture();
                failed?.Invoke(exception.Message);
            }
        }

        public void CancelCapture()
        {
            ThrowIfDisposed();
            if (_rebindOperation == null)
                return;

            // Cancel invokes CompleteCanceled synchronously in Input System 1.7. The fallback cleanup
            // protects us if a future Input System implementation does not invoke the callback here.
            InputActionRebindingExtensions.RebindingOperation operation = _rebindOperation;
            try
            {
                operation.Cancel();
            }
            finally
            {
                if (ReferenceEquals(_rebindOperation, operation))
                    CompleteCanceled();
            }
        }

        public RebindConflictInfo FindConflict(string actionName, string candidatePath)
        {
            ThrowIfDisposed();
            GetEntry(actionName); // Validate the target even if candidatePath is empty.

            string candidateKey = InputBindingConflict.Normalize(candidatePath);
            if (string.IsNullOrEmpty(candidateKey))
                return RebindConflictInfo.None(actionName, candidatePath);

            for (int i = 0; i < _entries.Count; i++)
            {
                InputRebindDraftEntry entry = _entries[i];
                if (string.Equals(entry.ActionName, actionName, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(
                        InputBindingConflict.Normalize(entry.Path),
                        candidateKey,
                        StringComparison.Ordinal))
                    continue;

                return new RebindConflictInfo(actionName, entry.ActionName, candidatePath);
            }

            return RebindConflictInfo.None(actionName, candidatePath);
        }

        /// <summary>
        /// Commits a candidate to the draft and unbinds every other action using the same key. Calling
        /// this is the confirmation step after FindConflict() returned a conflict.
        /// </summary>
        public void CommitCandidate(string actionName, string candidatePath)
        {
            ThrowIfDisposed();
            InputRebindDraftEntry target = GetEntry(actionName);
            string requestedPath = candidatePath ?? string.Empty;
            string candidateKey = InputBindingConflict.Normalize(requestedPath);

            if (!string.IsNullOrEmpty(candidateKey))
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    InputRebindDraftEntry entry = _entries[i];
                    if (ReferenceEquals(entry, target))
                        continue;
                    if (!string.Equals(
                            InputBindingConflict.Normalize(entry.Path),
                            candidateKey,
                            StringComparison.Ordinal))
                        continue;

                    if (!string.IsNullOrEmpty(entry.Path))
                    {
                        entry.Path = string.Empty;
                        BindingChanged?.Invoke(entry.ActionName);
                    }
                }
            }

            if (!string.Equals(target.Path, requestedPath, StringComparison.OrdinalIgnoreCase))
            {
                target.Path = requestedPath;
                BindingChanged?.Invoke(target.ActionName);
            }
        }

        /// <summary>
        /// Resets one row immediately when its default is free. On conflict nothing changes and the
        /// returned information can be passed to a confirmation popup; confirmation calls
        /// CommitCandidate(actionName, result.CandidatePath).
        /// </summary>
        public RebindConflictInfo Reset(string actionName)
        {
            ThrowIfDisposed();
            InputRebindDraftEntry entry = GetEntry(actionName);
            RebindConflictInfo conflict = FindConflict(actionName, entry.DefaultPath);
            if (conflict.HasConflict)
                return conflict;

            CommitCandidate(actionName, entry.DefaultPath);
            return conflict;
        }

        public void ResetAll()
        {
            ThrowIfDisposed();

            // Defaults come from the authored asset and are applied as one atomic draft update. This
            // intentionally does not run row-by-row conflict handling against intermediate state.
            for (int i = 0; i < _entries.Count; i++)
            {
                InputRebindDraftEntry entry = _entries[i];
                if (string.Equals(entry.Path, entry.DefaultPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                entry.Path = entry.DefaultPath;
                BindingChanged?.Invoke(entry.ActionName);
            }
        }

        public void Apply()
        {
            ThrowIfDisposed();
            if (IsCapturing)
                CancelCapture();

            var paths = new Dictionary<Guid, string>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
                paths[_entries[i].BindingId] = _entries[i].Path;

            _inputActions.ApplyBindingOverrides(paths);

            // The draft is now the applied state, so move the dirty baseline to match.
            CaptureBaseline();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Do not call view callbacks while its owner is being destroyed.
            _onCandidate = null;
            _onCanceled = null;
            _onFailed = null;

            if (_rebindOperation != null)
            {
                InputActionRebindingExtensions.RebindingOperation operation = _rebindOperation;
                try
                {
                    operation.Cancel();
                }
                finally
                {
                    if (ReferenceEquals(_rebindOperation, operation))
                        CleanupCapture();
                }
            }
            else
            {
                RestoreCaptureAction();
            }
        }

        private void CompleteCandidate()
        {
            string path = _capturedPath;
            Action<string> candidate = _onCandidate;
            Action<string> failed = _onFailed;
            CleanupCapture();

            if (string.IsNullOrEmpty(path))
                failed?.Invoke("No supported keyboard or mouse button was captured.");
            else
                candidate?.Invoke(path);
        }

        private void CompleteCanceled()
        {
            Action canceled = _onCanceled;
            CleanupCapture();
            canceled?.Invoke();
        }

        private void CleanupCapture()
        {
            InputActionRebindingExtensions.RebindingOperation operation = _rebindOperation;
            _rebindOperation = null;
            try
            {
                operation?.Dispose();
            }
            finally
            {
                // Restoring gameplay input is non-negotiable even if disposal in a future Input
                // System version starts surfacing an exception.
                RestoreCaptureAction();

                _capturedPath = null;
                _onCandidate = null;
                _onCanceled = null;
                _onFailed = null;
            }
        }

        private void RestoreCaptureAction()
        {
            InputAction action = _captureAction;
            bool shouldEnable = _captureActionWasEnabled;
            _captureAction = null;
            _captureActionWasEnabled = false;

            if (action != null && shouldEnable)
                action.Enable();
        }

        private InputRebindDraftEntry GetEntry(string actionName)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].ActionName, actionName, StringComparison.Ordinal))
                    return _entries[i];
            }

            throw new ArgumentException(
                $"Action '{actionName}' is not part of the rebindable keyboard-and-mouse set.",
                nameof(actionName));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InputRebindDraft));
        }
    }

    public static class InputBindingDisplay
    {
        public static string Format(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "-";

            return InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice |
                InputControlPath.HumanReadableStringOptions.UseShortNames);
        }
    }

    internal static class InputBindingConflict
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized = path.Trim().Replace(" ", string.Empty).ToLowerInvariant();
            int slash = normalized.LastIndexOf('/');
            if (slash < 0 || slash == normalized.Length - 1)
                return normalized;

            string device = normalized.Substring(0, slash);
            string control = normalized.Substring(slash + 1);

            if (device.Contains("keyboard"))
            {
                switch (control)
                {
                    case "shift":
                    case "shiftkey":
                    case "leftshift":
                    case "rightshift":
                        return "<keyboard>/shift";

                    case "ctrl":
                    case "control":
                    case "leftctrl":
                    case "rightctrl":
                    case "leftcontrol":
                    case "rightcontrol":
                        return "<keyboard>/ctrl";

                    case "alt":
                    case "leftalt":
                    case "rightalt":
                        return "<keyboard>/alt";
                }
            }

            return normalized;
        }
    }
}
