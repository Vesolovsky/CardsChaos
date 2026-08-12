using UnityEngine;
using UnityEngine.InputSystem;

namespace Vesolovsky.Game.Trailer
{
    public enum TrailerModifier
    {
        None,
        Ctrl,
        Alt,
        Shift,
    }

    /// <summary>
    /// One key, optionally with a modifier held, read straight off the keyboard rather than through
    /// the game's input asset.
    ///
    /// The trailer tools are driven while the Game view has focus and a take is running, so they
    /// need keys of their own that no rebind can take away and that no gameplay action answers to
    /// as well. Everything defaults to Alt + something for exactly that reason: Alt is not used by
    /// anything in the game, so a trailer key can never fire a skill by accident. Set the key to
    /// None to turn a binding off.
    /// </summary>
    [System.Serializable]
    public class TrailerHotkey
    {
        [Tooltip("Held alongside the key. Alt keeps trailer keys clear of every gameplay binding.")]
        [SerializeField] private TrailerModifier modifier = TrailerModifier.Alt;

        [SerializeField] private Key key = Key.None;

        public TrailerHotkey()
        {
        }

        public TrailerHotkey(Key key, TrailerModifier modifier = TrailerModifier.Alt)
        {
            this.key = key;
            this.modifier = modifier;
        }

        public bool IsSet => key != Key.None;

        public bool WasPressed()
        {
            Keyboard keyboard = Keyboard.current;

            if (!IsSet || keyboard == null || !ModifierHeld(keyboard))
                return false;

            return keyboard[key].wasPressedThisFrame;
        }

        public override string ToString()
        {
            if (!IsSet)
                return "(unbound)";

            return modifier == TrailerModifier.None ? key.ToString() : $"{modifier}+{key}";
        }

        private bool ModifierHeld(Keyboard keyboard)
        {
            return modifier switch
            {
                TrailerModifier.Ctrl => keyboard.ctrlKey.isPressed,
                TrailerModifier.Alt => keyboard.altKey.isPressed,
                TrailerModifier.Shift => keyboard.shiftKey.isPressed,
                _ => true,
            };
        }
    }
}
