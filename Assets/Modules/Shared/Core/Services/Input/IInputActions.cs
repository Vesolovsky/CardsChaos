using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Vesolovsky.Core.Services.Input
{
    /// <summary>
    /// The game's one input schema. Gameplay, HUD hints and the settings screen all read the same
    /// asset through this service. Binding overrides are loaded before the asset is enabled and are
    /// persisted only when a rebind draft is explicitly applied.
    /// </summary>
    public interface IInputActions
    {
        event Action BindingsChanged;

        InputActionAsset Asset { get; }
        IReadOnlyList<GameInputBindingInfo> RebindableBindings { get; }

        InputAction Find(string actionName);
        bool TryGetBindingInfo(string actionName, out GameInputBindingInfo bindingInfo);
        string Display(string actionName);

        InputRebindDraft CreateRebindDraft();

        /// <summary>
        /// Applies a complete draft, persists it and raises <see cref="BindingsChanged"/>. This is
        /// primarily called by <see cref="InputRebindDraft.Apply"/>.
        /// </summary>
        void ApplyBindingOverrides(IReadOnlyDictionary<Guid, string> effectivePathsByBindingId);
    }

    /// <summary>Stable action names shared by gameplay, HUD hints and the settings screen.</summary>
    public static class GameInputActions
    {
        public const string Throw = "Throw";
        public const string ToggleHand = "ToggleHand";
        public const string ToggleAlbum = "ToggleAlbum";
        public const string ToggleUpgrades = "ToggleUpgrades";
        public const string Sprint = "Sprint";
        public const string Interact = "Interact";
        public const string FlipCard = "FlipCard";
        public const string CardMagnet = "CardMagnet";
        public const string HandSort = "HandSort";
        public const string SmartAlbumOpen = "SmartAlbumOpen";
        public const string Levitate = "Levitate";
        public const string MuscleMemory = "MuscleMemory";
        public const string Zoom = "Zoom";

        private static readonly string[] RebindableActionNamesInternal =
        {
            Throw,
            ToggleHand,
            ToggleAlbum,
            ToggleUpgrades,
            Sprint,
            Interact,
            FlipCard,
            CardMagnet,
            HandSort,
            SmartAlbumOpen,
            Levitate,
            MuscleMemory,
            Zoom,
        };

        public static IReadOnlyList<string> RebindableActionNames { get; } =
            Array.AsReadOnly(RebindableActionNamesInternal);
    }

    /// <summary>A snapshot of one keyboard-and-mouse binding in the live input asset.</summary>
    public sealed class GameInputBindingInfo
    {
        public string ActionName { get; }
        public Guid BindingId { get; }
        public int BindingIndex { get; }
        public string DefaultPath { get; }
        public string EffectivePath { get; }

        public GameInputBindingInfo(
            string actionName,
            Guid bindingId,
            int bindingIndex,
            string defaultPath,
            string effectivePath)
        {
            ActionName = actionName;
            BindingId = bindingId;
            BindingIndex = bindingIndex;
            DefaultPath = defaultPath ?? string.Empty;
            EffectivePath = effectivePath ?? string.Empty;
        }
    }

    public class InputActions : IInputActions, IDisposable
    {
        private const string BindingOverridesPlayerPrefsKey =
            "Vesolovsky.GameControls.BindingOverrides.v1";
        private const string KeyboardMouseBindingGroup = "Keyboard&Mouse";

        private readonly InputActionAsset _asset;
        private readonly List<GameInputBindingInfo> _rebindableBindings =
            new List<GameInputBindingInfo>();
        private readonly ReadOnlyCollection<GameInputBindingInfo> _readOnlyRebindableBindings;

        public event Action BindingsChanged;

        public InputActionAsset Asset => _asset;
        public IReadOnlyList<GameInputBindingInfo> RebindableBindings => _readOnlyRebindableBindings;

        // Optional so a scene whose asset has not been assigned yet still builds - it just logs and
        // hands out no actions, rather than tripping the whole container on a null argument.
        public InputActions([InjectOptional] InputActionAsset asset)
        {
            _asset = asset;
            _readOnlyRebindableBindings = _rebindableBindings.AsReadOnly();

            if (_asset == null)
            {
                Debug.LogError($"[{nameof(InputActions)}] No {nameof(InputActionAsset)} assigned; " +
                               "no gameplay input will fire.");
                return;
            }

            LoadSavedBindingOverrides();
            RefreshBindingInfo();
            _asset.Enable();
        }

        public InputAction Find(string actionName)
        {
            if (_asset == null)
                return null;

            InputAction action = _asset.FindAction(actionName);
            if (action == null)
                Debug.LogError($"[{nameof(InputActions)}] The input asset has no action '{actionName}'.");

            return action;
        }

        public bool TryGetBindingInfo(string actionName, out GameInputBindingInfo bindingInfo)
        {
            for (int i = 0; i < _rebindableBindings.Count; i++)
            {
                if (!string.Equals(
                        _rebindableBindings[i].ActionName,
                        actionName,
                        StringComparison.Ordinal))
                    continue;

                bindingInfo = _rebindableBindings[i];
                return true;
            }

            bindingInfo = null;
            return false;
        }

        public string Display(string actionName)
        {
            if (!TryGetBindingInfo(actionName, out GameInputBindingInfo bindingInfo))
                return "?";

            // Gameplay hints have historically used keycap-like upper-case labels. Settings drafts
            // use InputRebindDraft.GetDisplay(), which keeps Unity's friendly title casing.
            return InputBindingDisplay.Format(bindingInfo.EffectivePath).ToUpperInvariant();
        }

        public InputRebindDraft CreateRebindDraft()
        {
            return new InputRebindDraft(this);
        }

        public void ApplyBindingOverrides(IReadOnlyDictionary<Guid, string> effectivePathsByBindingId)
        {
            if (_asset == null)
                return;
            if (effectivePathsByBindingId == null)
                throw new ArgumentNullException(nameof(effectivePathsByBindingId));

            bool assetWasEnabled = _asset.enabled;
            if (assetWasEnabled)
                _asset.Disable();

            try
            {
                // Work from fresh binding data in case another system changed the asset after this
                // service was constructed.
                RefreshBindingInfo();

                for (int i = 0; i < _rebindableBindings.Count; i++)
                {
                    GameInputBindingInfo bindingInfo = _rebindableBindings[i];
                    if (!effectivePathsByBindingId.TryGetValue(bindingInfo.BindingId, out string path))
                        continue;

                    InputAction action = _asset.FindAction(bindingInfo.ActionName);
                    if (action == null || bindingInfo.BindingIndex >= action.bindings.Count)
                        continue;

                    action.RemoveBindingOverride(bindingInfo.BindingIndex);

                    string requestedPath = path ?? string.Empty;
                    if (!string.Equals(
                            requestedPath,
                            bindingInfo.DefaultPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        // An empty override deliberately disables the binding and is displayed as '-'.
                        action.ApplyBindingOverride(bindingInfo.BindingIndex, requestedPath);
                    }
                }

                PlayerPrefs.SetString(
                    BindingOverridesPlayerPrefsKey,
                    _asset.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();
                RefreshBindingInfo();
            }
            finally
            {
                if (assetWasEnabled)
                    _asset.Enable();
            }

            BindingsChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_asset != null)
                _asset.Disable();
        }

        private void LoadSavedBindingOverrides()
        {
            if (!PlayerPrefs.HasKey(BindingOverridesPlayerPrefsKey))
                return;

            string json = PlayerPrefs.GetString(BindingOverridesPlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                // Must happen before Enable(), otherwise gameplay can observe one frame of defaults.
                _asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[{nameof(InputActions)}] Saved binding overrides could not be loaded and " +
                    $"will be ignored. {exception.Message}");
                PlayerPrefs.DeleteKey(BindingOverridesPlayerPrefsKey);
                PlayerPrefs.Save();
            }
        }

        private void RefreshBindingInfo()
        {
            _rebindableBindings.Clear();
            if (_asset == null)
                return;

            IReadOnlyList<string> actionNames = GameInputActions.RebindableActionNames;
            for (int actionIndex = 0; actionIndex < actionNames.Count; actionIndex++)
            {
                string actionName = actionNames[actionIndex];
                InputAction action = _asset.FindAction(actionName);
                if (action == null)
                {
                    Debug.LogError(
                        $"[{nameof(InputActions)}] Rebindable action '{actionName}' is missing.");
                    continue;
                }

                if (!TryFindKeyboardMouseBinding(action, out int bindingIndex))
                {
                    Debug.LogError(
                        $"[{nameof(InputActions)}] Rebindable action '{actionName}' has no " +
                        $"'{KeyboardMouseBindingGroup}' binding.");
                    continue;
                }

                InputBinding binding = action.bindings[bindingIndex];
                _rebindableBindings.Add(new GameInputBindingInfo(
                    actionName,
                    binding.id,
                    bindingIndex,
                    binding.path,
                    binding.effectivePath));
            }
        }

        private static bool TryFindKeyboardMouseBinding(InputAction action, out int bindingIndex)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;
                if (!BindingBelongsToGroup(binding.groups, KeyboardMouseBindingGroup))
                    continue;

                bindingIndex = i;
                return true;
            }

            bindingIndex = -1;
            return false;
        }

        private static bool BindingBelongsToGroup(string groups, string expectedGroup)
        {
            if (string.IsNullOrEmpty(groups))
                return false;

            string[] bindingGroups = groups.Split(';');
            for (int i = 0; i < bindingGroups.Length; i++)
            {
                if (string.Equals(
                        bindingGroups[i].Trim(),
                        expectedGroup,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
