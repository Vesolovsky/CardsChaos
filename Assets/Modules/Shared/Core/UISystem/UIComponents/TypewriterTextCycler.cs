using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Types a run of lines through one <see cref="TypewriterText"/>, one after another, round and
    /// round for as long as it is on screen.
    ///
    /// For a label with more to say than fits in it: rather than cramming two rules into one
    /// sentence or finding room for a second label, the one line says each of them in turn. Each
    /// line types itself out, holds long enough to be read, is wiped, and the next takes its place.
    ///
    /// It runs off its own enabling, not off a call, so a screen that hides and shows this label
    /// picks the cycle back up without anything having to drive it - and stops it dead when the
    /// label goes away, rather than leaving a loop typing into a panel nobody is looking at.
    /// </summary>
    [RequireComponent(typeof(TypewriterText))]
    [AddComponentMenu("Vesolovsky/UI/Typewriter Text Cycler")]
    public class TypewriterTextCycler : MonoBehaviour
    {
        [Tooltip("The lines, shown in this order and then round again. One line on its own is " +
                 "simply typed out and left up - there is nothing to swap it for.")]
        [SerializeField, TextArea] private List<string> lines = new List<string>();

        [Tooltip("Seconds a line stays up once it has finished typing. Counted from the end of " +
                 "the reveal, not the start, so a long line and a short one are both readable for " +
                 "this long.")]
        [SerializeField, Min(0f)] private float holdDuration = 4f;

        [Tooltip("Quiet gap after a line is wiped and before the next starts typing. Small - it " +
                 "is the beat that makes the swap read as a new line rather than as a redraw.")]
        [SerializeField, Min(0f)] private float gapDuration = 0.4f;

        private TypewriterText _typewriter;

        // Cancelled when the label is switched off, so the loop stops with it rather than running
        // on against a hidden panel. Linked to the destroy token too, for the same reason.
        private CancellationTokenSource _cts;

        private TypewriterText Typewriter =>
            _typewriter != null ? _typewriter : _typewriter = GetComponent<TypewriterText>();

        private void OnEnable()
        {
            if (lines == null || lines.Count == 0)
                return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunAsync(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            // Not on the enabling frame itself. This is the only place in the game that drives a
            // TypewriterText from OnEnable, and on a scene load that lands in the middle of
            // everything waking up - before the canvas has built its first mesh, and with no
            // guarantee that the TMP_Text on this very object has had its own OnEnable yet.
            // Play() force-updates that mesh and reads a character count back out of it, which is
            // not a question worth asking a text that is not up yet. One frame later everything on
            // the object is awake and laid out, and the label is invisible until then anyway.
            if (await UniTask.NextFrame(ct).SuppressCancellationThrow())
                return;

            int index = 0;

            while (!ct.IsCancellationRequested)
            {
                string line = lines[index];
                index = (index + 1) % lines.Count;

                Typewriter.Play(line);

                // The reveal first, then the hold: the hold is meant to be time spent reading a
                // finished line, and a line that types for a second would otherwise get a second
                // less of it than a short one.
                if (await Wait(Typewriter.RevealDuration + holdDuration, ct))
                    return;

                // A single line has nowhere to go next, so it is left up rather than blinked.
                if (lines.Count < 2)
                    return;

                Typewriter.SetImmediate(string.Empty);

                if (await Wait(gapDuration, ct))
                    return;
            }
        }

        /// <summary>Waits, and reports whether the wait was cut short rather than throwing.</summary>
        private static async UniTask<bool> Wait(float seconds, CancellationToken ct)
        {
            if (seconds <= 0f)
                return ct.IsCancellationRequested;

            return await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct)
                .SuppressCancellationThrow();
        }
    }
}
