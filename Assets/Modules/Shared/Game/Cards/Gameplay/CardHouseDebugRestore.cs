#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace CardsChaos.Cards
{
    /// <summary>
    /// TEMPORARY editor-play-mode helper: press F5 to stand every collapsed house of cards back up,
    /// so a collapse can be tested over and over without leaving Play mode.
    ///
    /// The whole file is compiled out of a real build (editor-only), and it installs itself, so
    /// nothing needs wiring in the scene. Delete this file once the house collapse is dialled in.
    ///
    /// F5 rather than Ctrl+Z on purpose: Ctrl+Z in Play mode can trip the editor's own Undo. Change
    /// <see cref="RestoreKey"/> if F5 clashes with anything.
    /// </summary>
    internal static class CardHouseDebugRestore
    {
        private const Key RestoreKey = Key.F5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("[CardHouseDebugRestore]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null || !keyboard[RestoreKey].wasPressedThisFrame)
                    return;

                CardHand hand = FindFirstObjectByType<CardHand>();
                CardHouse[] houses = FindObjectsByType<CardHouse>(FindObjectsSortMode.None);

                foreach (CardHouse house in houses)
                    house.RestoreStanding(hand);

                Debug.Log($"[CardHouseDebugRestore] Stood {houses.Length} house(s) back up.");
            }
        }
    }
}
#endif
