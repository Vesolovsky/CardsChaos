using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Vesolovsky.Core.Services.Input
{
    /// <summary>
    /// The game's one input schema. It owns the <see cref="InputActionAsset"/>, keeps it enabled for
    /// the session, and hands out actions and their display strings by name.
    ///
    /// This is the single source of truth for what key does what: the readers ask it for an action
    /// to poll, the HUD asks it for the same action's label, and both change together the instant the
    /// binding does - there is no second copy of a key anywhere to fall out of step. Rebinding, when
    /// it comes, edits the one asset behind this.
    /// </summary>
    public interface IInputActions
    {
        /// <summary>The action with the given name, or null (with an error) when the asset has none.</summary>
        InputAction Find(string actionName);

        /// <summary>The action's binding as text for a hint - "F", "1", "Tab". "?" when it is missing.</summary>
        string Display(string actionName);
    }

    /// <summary>Action names shared by the readers and the HUD, so the two never disagree on a spelling.</summary>
    public static class GameInputActions
    {
        public const string Throw = "Throw";
        public const string ToggleHand = "ToggleHand";
        public const string ToggleAlbum = "ToggleAlbum";
        public const string ToggleUpgrades = "ToggleUpgrades";
        public const string Sprint = "Sprint";
        public const string Interact = "Interact";
        public const string FlipCard = "FlipCard";
    }

    public class InputActions : IInputActions, IDisposable
    {
        private readonly InputActionAsset _asset;

        // Optional so a scene whose asset has not been assigned yet still builds - it just logs and
        // hands out no actions, rather than tripping the whole container on a null argument.
        public InputActions([InjectOptional] InputActionAsset asset)
        {
            _asset = asset;

            if (_asset != null)
                _asset.Enable();
            else
                Debug.LogError($"[{nameof(InputActions)}] No {nameof(InputActionAsset)} assigned; " +
                               "no gameplay input will fire.");
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

        public string Display(string actionName)
        {
            InputAction action = Find(actionName);

            // Upper-cased so a named key reads like a keycap - "TAB", not "Tab" - beside the single
            // letters it sits next to in the hints.
            return action != null ? action.GetBindingDisplayString().ToUpperInvariant() : "?";
        }

        public void Dispose()
        {
            if (_asset != null)
                _asset.Disable();
        }
    }
}
