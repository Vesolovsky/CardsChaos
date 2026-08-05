using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// The one place HUD hints appear. Each hint is authored once - its words, an optional icon,
    /// optionally which key to name inside its text, and its own timing - under a <see cref="HintId"/>;
    /// the rest of the HUD raises a hint by that id and this shows it.
    ///
    /// Hints queue: raised one after another they play one after another, each fading in, holding
    /// for its own time, and fading out before the next takes the stage. Timing is per hint, so a
    /// "how to" can linger while a "ready" flashes by, and a hint can wait before it first shows.
    ///
    /// A hint is either a one-time teaching nudge - shown at most once a scene - or a recurring one
    /// that may play again each time it is raised (a skill announcing itself ready as its cooldown
    /// ends). Either way the same hint is never queued twice at once.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Hint")]
    public class HudHint : MonoBehaviour
    {
        /// <summary>One authored hint: what it reads, an optional key and icon, and its own timing.</summary>
        [Serializable]
        public class HintDefinition
        {
            [Tooltip("The id the code raises this hint by.")]
            public HintId Id;

            [Tooltip("What the hint reads. Rich text is fine, so an inline <sprite> icon can sit " +
                     "right in it. Put {0} where a key should go and set Action Name below.")]
            [TextArea]
            public string Text;

            [Tooltip("Optional. A GameInputActions name whose current key is written into the {0} " +
                     "of Text - e.g. Throw. Leave empty for a hint that names no key.")]
            public string ActionName;

            [Tooltip("Optional. Shown in the icon slot beside the text; the slot is hidden for " +
                     "hints with none. An inline <sprite> in Text is the other way to carry an icon.")]
            public Sprite Icon;

            [Tooltip("Seconds to wait once this hint reaches the front of the queue before it fades " +
                     "in. Let the scene settle before the very first nudge; leave 0 for the rest.")]
            public float StartDelay;

            [Tooltip("Seconds the hint holds fully shown before it fades out. Per hint - a 'how to' " +
                     "can dwell while a quick 'ready' passes by.")]
            public float HoldDuration = 3.5f;

            [Tooltip("On for a one-time teaching nudge, shown at most once a scene; off for a hint " +
                     "that may recur, like a skill announcing itself ready each time it comes off cooldown.")]
            public bool OneTime = true;
        }

        [Header("Display")]
        [Tooltip("Faded as one to show and hide the whole hint.")]
        [SerializeField] private CanvasGroup group;

        [SerializeField] private TMP_Text text;

        [Tooltip("Optional icon shown beside the text. Switched off for hints that carry none.")]
        [SerializeField] private Image icon;

        [Header("Hints")]
        [Tooltip("Every hint the HUD can raise, one entry per id.")]
        [SerializeField] private List<HintDefinition> hints = new List<HintDefinition>();

        [Header("Fade")]
        [SerializeField] private float fadeInDuration = 0.4f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        [Tooltip("Quiet gap between one hint fading out and the next fading in.")]
        [SerializeField] private float gapDuration = 0.3f;

        private readonly Queue<HintDefinition> _queue = new Queue<HintDefinition>();

        // One-time hints already shown this scene, so they are never shown again.
        private readonly HashSet<HintId> _seen = new HashSet<HintId>();

        // Hints queued or on screen right now, so the same one is never lined up twice at once -
        // for a recurring hint this is the only guard, since it is not tracked in _seen.
        private readonly HashSet<HintId> _pending = new HashSet<HintId>();

        private Func<string, string> _keyDisplayResolver;
        private bool _running;
        private bool _enabled = true;

        /// <summary>
        /// Wires the way to turn an action name into its current key label, so a hint that names a
        /// key reads right and follows a rebind. Also parks the display hidden. Optional resolver -
        /// without it a hint that names a key just shows its text with the placeholder unfilled.
        /// </summary>
        public void Initialize(Func<string, string> keyDisplayResolver)
        {
            _keyDisplayResolver = keyDisplayResolver;

            if (group != null)
                group.alpha = 0f;

            SetIcon(null);
        }

        /// <summary>
        /// Turns hints on or off - the "Show hints" setting drives this. Turned off, nothing is
        /// raised and anything still queued is dropped; a hint already on screen fades out on its
        /// own as the queue stops feeding it. A one-time hint refused while off is not marked seen,
        /// so it can still teach its lesson if hints are turned back on and it is raised again.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;

            if (_enabled)
                return;

            _queue.Clear();
            _pending.Clear();
        }

        /// <summary>
        /// Raises a hint. It joins the back of the queue and plays once it reaches the front. A
        /// one-time hint that has already been shown this scene does nothing; any hint already
        /// queued or on screen does nothing, so a burst never stacks the same hint up. With hints
        /// turned off nothing is raised at all.
        /// </summary>
        public void Show(HintId id)
        {
            if (!_enabled)
                return;

            HintDefinition definition = Find(id);
            if (definition == null)
            {
                Debug.LogWarning($"[{nameof(HudHint)}] No hint is authored for id '{id}'.", this);
                return;
            }

            if (definition.OneTime && _seen.Contains(id))
                return;

            // Already waiting or showing - do not line it up a second time.
            if (!_pending.Add(id))
                return;

            if (definition.OneTime)
                _seen.Add(id);

            _queue.Enqueue(definition);

            if (!_running)
                RunAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            _running = true;

            try
            {
                while (_queue.Count > 0)
                {
                    HintDefinition definition = _queue.Dequeue();
                    await PlayAsync(definition, ct);

                    // Off the pending list only once it has finished, so a recurring hint can be
                    // raised again for its next turn but never while this one is still up.
                    _pending.Remove(definition.Id);

                    if (_queue.Count > 0 && gapDuration > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(gapDuration), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // The scene went away mid-hint; nothing to clean up.
            }
            finally
            {
                _running = false;
            }
        }

        private async UniTask PlayAsync(HintDefinition definition, CancellationToken ct)
        {
            // The wait is spent hidden, so nothing of the previous hint lingers during it.
            if (definition.StartDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(definition.StartDelay), cancellationToken: ct);

            Apply(definition);

            if (group == null)
                return;

            await Tween.Alpha(group, 1f, fadeInDuration).WithCancellation(ct);

            if (definition.HoldDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(definition.HoldDuration), cancellationToken: ct);

            await Tween.Alpha(group, 0f, fadeOutDuration).WithCancellation(ct);
        }

        private void Apply(HintDefinition definition)
        {
            if (text != null)
                text.SetText(Compose(definition));

            SetIcon(definition.Icon);
        }

        private string Compose(HintDefinition definition)
        {
            string body = definition.Text ?? string.Empty;

            if (string.IsNullOrEmpty(definition.ActionName) || _keyDisplayResolver == null)
                return body;

            return string.Format(body, _keyDisplayResolver(definition.ActionName));
        }

        private void SetIcon(Sprite sprite)
        {
            if (icon == null)
                return;

            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        private HintDefinition Find(HintId id)
        {
            foreach (HintDefinition definition in hints)
            {
                if (definition != null && definition.Id == id)
                    return definition;
            }

            return null;
        }
    }
}
