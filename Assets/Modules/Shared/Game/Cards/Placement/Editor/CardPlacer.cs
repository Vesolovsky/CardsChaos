using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Edit-mode authoring for laying the whole collection out by hand.
    ///
    /// Two moves, both on rebindable shortcuts (Edit > Shortcuts > CardsChaos):
    ///   Spawn   - drops the next card copy the scene is still missing at the view pivot, face up,
    ///             ready to be dragged into place. The first collection is completed before the
    ///             placer starts handing out second copies, and those are rationed per set by
    ///             <see cref="CardDuplicates"/> - not every card gets one.
    ///   Release - runs the physics on the selected card(s) only, letting them fall and nestle
    ///             onto everything already placed without shifting the rest of the pile.
    ///
    /// <see cref="CardPlacerWindow"/> adds the one-shot Ctrl+Click that does both at the cursor.
    ///
    /// Resting cards carry only a BoxCollider. This adds a tuned Rigidbody to the selected target
    /// cards for the duration of the drop, steps Unity's own PhysX with
    /// <see cref="Physics.Simulate"/>, then removes those temporary bodies again.
    /// </summary>
    public static class CardPlacer
    {
        public enum RotationStyle
        {
            FlatFaceUp, // laid flat, face to the sky; only the spin around up is random
            RandomFlat, // laid flat but face up or down at random, with a small tilt
            FullRandom, // any orientation - let the physics tumble it flat
        }

        // The card mesh faces along +Z, so a -90 pitch lays it flat face up and +90 face down.
        // Same convention the old CardSpawner used.
        private const float FaceUpPitch = -90f;
        private const float FaceDownPitch = 90f;

        private const string ParentName = "EnviroCards";
        private const int MaxSceneCopiesPerCard = 2;

        // Catalog() runs on every window repaint, so the "more than one catalog" note is latched
        // to fire once a session instead of spamming the console.
        private static bool _warnedMultipleCatalogs;

        // A resting card drifts well under a millimetre a second, so anything below these counts
        // as asleep and lets the simulation stop early instead of grinding out every step.
        private const float LinearSleep = 0.01f; // m/s
        private const float AngularSleep = 0.15f; // rad/s
        private const int StableStepsToStop = 6; // consecutive calm steps before an early stop

        // ------------------------------------------------------------------ persisted settings
        // Kept in EditorPrefs rather than on the window so the shortcuts behave the same whether
        // or not the window happens to be open.

        private const string Prefix = "CardsChaos.CardPlacer.";

        public static RotationStyle SpawnRotation
        {
            get => (RotationStyle)EditorPrefs.GetInt(Prefix + "SpawnRotation", (int)RotationStyle.FlatFaceUp);
            set => EditorPrefs.SetInt(Prefix + "SpawnRotation", (int)value);
        }

        public static RotationStyle ClickRotation
        {
            get => (RotationStyle)EditorPrefs.GetInt(Prefix + "ClickRotation", (int)RotationStyle.RandomFlat);
            set => EditorPrefs.SetInt(Prefix + "ClickRotation", (int)value);
        }

        public static float MaxTilt
        {
            get => EditorPrefs.GetFloat(Prefix + "MaxTilt", 12f);
            set => EditorPrefs.SetFloat(Prefix + "MaxTilt", value);
        }

        /// <summary>How high above the target point a Ctrl+Click card is dropped from, in metres.</summary>
        public static float DropHeight
        {
            get => EditorPrefs.GetFloat(Prefix + "DropHeight", 0.03f);
            set => EditorPrefs.SetFloat(Prefix + "DropHeight", Mathf.Max(0f, value));
        }

        /// <summary>Hard cap on physics steps so a card that never settles cannot hang the editor.</summary>
        public static int MaxSimSteps
        {
            get => EditorPrefs.GetInt(Prefix + "MaxSimSteps", 400);
            set => EditorPrefs.SetInt(Prefix + "MaxSimSteps", Mathf.Clamp(value, 1, 5000));
        }

        /// <summary>GUID of the catalog to draw from; empty means "find the only one in the project".</summary>
        public static string CatalogGuid
        {
            get => EditorPrefs.GetString(Prefix + "CatalogGuid", string.Empty);
            set => EditorPrefs.SetString(Prefix + "CatalogGuid", value ?? string.Empty);
        }

        // ------------------------------------------------------------------------- shortcuts

        [Shortcut("CardsChaos/Spawn Next Card", KeyCode.G)]
        public static void SpawnShortcut()
        {
            GameObject card = Spawn(PivotPoint(), SpawnRotation);
            if (card == null)
                return;

            // Left selected with the move gizmo up so it can be dragged straight away.
            Selection.activeGameObject = card;
            Tools.current = Tool.Move;
        }

        [Shortcut("CardsChaos/Release Selected (Simulate)", KeyCode.H)]
        public static void ReleaseSelectedShortcut()
        {
            List<Card> targets = SelectedCards();
            if (targets.Count == 0)
            {
                Debug.LogWarning("[CardPlacer] Select one or more cards to release.");
                return;
            }

            Simulate(targets, "Release Cards");
        }

        // ----------------------------------------------------------------------------- public

        /// <summary>Spawn + settle in one step, used by the Ctrl+Click placement mode.</summary>
        public static void SpawnAndRelease(Vector3 groundPoint)
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place Card");

            GameObject card = Spawn(groundPoint + Vector3.up * DropHeight, ClickRotation);
            if (card != null && card.TryGetComponent(out Card target))
            {
                Simulate(new[] { target }, "Place Card");
                Selection.activeGameObject = card;
            }

            // Collapse spawn + settle so a single Ctrl+Z removes the placed card whole.
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// Instantiates a random card copy the scene does not have yet at
        /// <paramref name="position"/>. Missing first copies are always completed before second
        /// copies are offered. Returns null when the catalog is missing/empty or every card is
        /// already present twice.
        /// </summary>
        public static GameObject Spawn(Vector3 position, RotationStyle style)
        {
            CardCatalog catalog = Catalog();
            if (catalog == null)
            {
                Debug.LogWarning("[CardPlacer] No CardCatalog found. Pick one in the Card Placer window.");
                return null;
            }

            List<Card> candidates = NextPlacementCandidates(catalog, out bool placingSecondCopies);
            if (candidates.Count == 0)
            {
                Debug.Log(catalog.Cards.Count == 0
                    ? "[CardPlacer] The catalog is empty."
                    : "[CardPlacer] Every card in the catalog is already in the scene twice.");
                return null;
            }

            Card prefab = candidates[Random.Range(0, candidates.Count)];

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Card");

            instance.transform.SetParent(SpawnParent(), worldPositionStays: true);
            instance.transform.SetPositionAndRotation(position, Rotation(style));
            EditorUtility.SetDirty(instance);

            string remainingKind = placingSecondCopies
                ? "second copies still missing"
                : "first copies still missing";
            Debug.Log(
                $"[CardPlacer] Spawned '{instance.name}' - {candidates.Count - 1} {remainingKind}.",
                instance);
            return instance;
        }

        /// <summary>
        /// Falls the given cards under gravity while every other card in the scene is frozen into
        /// an immovable collider, so a released card settles onto the existing pile without the
        /// pile settling into itself. The move is a single undoable step.
        /// </summary>
        public static void Simulate(IReadOnlyList<Card> targets, string undoName)
        {
            if (targets == null || targets.Count == 0)
                return;

            var targetBodies = new List<Rigidbody>(targets.Count);
            var targetSet = new HashSet<Rigidbody>();
            foreach (Card target in targets)
            {
                // Inactive bodies do not participate in Physics.Simulate and are excluded from
                // the scene-body query below. Do not create a temporary component that could not
                // be configured or removed by this operation.
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;

                Rigidbody body = target.EnsureBody();
                if (targetSet.Add(body))
                    targetBodies.Add(body);
            }

            if (targetBodies.Count == 0)
                return;

            Rigidbody[] all = Object.FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var savedKinematic = new Dictionary<Rigidbody, bool>(all.Length);
            var savedCollision = new Dictionary<Rigidbody, CollisionDetectionMode>(all.Length);
            var savedInterpolation = new Dictionary<Rigidbody, RigidbodyInterpolation>(all.Length);
            SimulationMode previousMode = Physics.simulationMode;

            // Pre-sim poses of the targets, captured so the whole thing can be turned into one
            // clean undo entry after the fact - Physics.Simulate writes the transforms in native
            // code, outside anything Undo would otherwise notice.
            var poses = new (Transform Transform, Vector3 Position, Quaternion Rotation)[targetBodies.Count];
            for (int i = 0; i < targetBodies.Count; i++)
            {
                Transform t = targetBodies[i].transform;
                poses[i] = (t, t.position, t.rotation);
            }

            try
            {
                foreach (Rigidbody body in all)
                {
                    savedKinematic[body] = body.isKinematic;
                    savedCollision[body] = body.collisionDetectionMode;
                    savedInterpolation[body] = body.interpolation;

                    if (targetSet.Contains(body))
                    {
                        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        body.isKinematic = false;
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        // Physics.Simulate advances directly; interpolation is only useful while
                        // rendering between automatic fixed steps.
                        body.interpolation = RigidbodyInterpolation.None;
                        body.useGravity = true;
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    else
                    {
                        // Frozen: still collides with the falling card, but never moves itself.
                        // A continuous dynamic mode cannot be kept on a kinematic body.
                        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        body.isKinematic = true;
                    }
                }

                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();

                float step = Time.fixedDeltaTime;
                int calm = 0;
                for (int i = 0; i < MaxSimSteps; i++)
                {
                    Physics.Simulate(step);
                    calm = AllCalm(targetBodies) ? calm + 1 : 0;
                    if (calm >= StableStepsToStop)
                        break;
                }
            }
            finally
            {
                Physics.simulationMode = previousMode;

                foreach (Rigidbody body in all)
                {
                    if (targetSet.Contains(body))
                    {
                        // Leave the authored result as a static BoxCollider. DestroyImmediate is
                        // intentional in edit mode: the body is temporary implementation detail,
                        // while RecordSettledPoses below owns the single useful undo entry.
                        if (!body.isKinematic)
                        {
                            body.velocity = Vector3.zero;
                            body.angularVelocity = Vector3.zero;
                        }

                        body.interpolation = RigidbodyInterpolation.None;
                        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        body.isKinematic = true;
                        Object.DestroyImmediate(body);
                        continue;
                    }

                    if (!savedKinematic.TryGetValue(body, out bool kinematic))
                        continue;

                    // Restore unrelated bodies exactly. If one was dynamic, leave the temporary
                    // kinematic state before restoring a continuous collision mode.
                    body.isKinematic = kinematic;
                    body.collisionDetectionMode = savedCollision[body];
                    body.interpolation = savedInterpolation[body];
                }
            }

            RecordSettledPoses(poses, undoName);
            MarkActiveSceneDirty();
        }

        public static CardCatalog Catalog()
        {
            string guid = CatalogGuid;
            if (!string.IsNullOrEmpty(guid))
            {
                var chosen = AssetDatabase.LoadAssetAtPath<CardCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (chosen != null)
                    return chosen;
            }

            string[] found = AssetDatabase.FindAssets("t:CardCatalog");
            if (found.Length == 0)
                return null;

            if (found.Length > 1 && !_warnedMultipleCatalogs)
            {
                _warnedMultipleCatalogs = true;
                Debug.LogWarning(
                    $"[CardPlacer] {found.Length} CardCatalog assets exist; using the first. " +
                    "Pick the one you want in the Card Placer window.");
            }

            return AssetDatabase.LoadAssetAtPath<CardCatalog>(AssetDatabase.GUIDToAssetPath(found[0]));
        }

        /// <summary>
        /// Reports catalog identities represented at least once and at least twice in the scene.
        /// A second physical copy is counted for authoring progress only; neither instance is
        /// marked as the duplicate. <paramref name="duplicateTotal"/> is the quota the sets add up
        /// to, which is far less than the catalog - most cards never get a second copy.
        /// </summary>
        public static void Counts(
            out int placed, out int duplicates, out int total, out int duplicateTotal)
        {
            CardCatalog catalog = Catalog();
            total = catalog == null ? 0 : catalog.Cards.Count;
            placed = 0;
            duplicates = 0;
            duplicateTotal = CardDuplicates.TotalQuota(catalog);

            if (catalog == null)
                return;

            Dictionary<(string, int), int> present = PresentIdentityCounts();

            foreach (Card card in catalog.Cards)
            {
                if (!TryGetPrefabIdentity(card, out (string, int) key))
                    continue;

                present.TryGetValue(key, out int count);
                if (count >= 1)
                    placed++;

                if (count >= MaxSceneCopiesPerCard)
                    duplicates++;
            }
        }

        /// <summary>
        /// Deletes every copy of a card beyond the first, so the scene is back to one physical card
        /// per identity and the duplicate pass can be redone. Undoable in one step.
        ///
        /// Where several copies exist the survivor is the one carrying the most components - that
        /// is what keeps the authored endgame card, which has a script the plain copies do not -
        /// and then the earliest sibling, which is the copy that was placed first.
        /// </summary>
        public static int RemoveExtraCopies()
        {
            var byIdentity = new Dictionary<(string, int), List<CardIdentity>>();

            CardIdentity[] identities = Object.FindObjectsByType<CardIdentity>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (CardIdentity identity in identities)
            {
                var key = (identity.SetId, identity.Number);
                if (!byIdentity.TryGetValue(key, out List<CardIdentity> copies))
                    byIdentity[key] = copies = new List<CardIdentity>();

                copies.Add(identity);
            }

            int removed = 0;

            foreach (KeyValuePair<(string, int), List<CardIdentity>> entry in byIdentity)
            {
                List<CardIdentity> copies = entry.Value;
                if (copies.Count < 2)
                    continue;

                copies.Sort(CompareKeepPriority);

                for (int i = 1; i < copies.Count; i++)
                {
                    Undo.DestroyObjectImmediate(copies[i].gameObject);
                    removed++;
                }
            }

            Debug.Log($"[CardPlacer] Removed {removed} extra card cop{(removed == 1 ? "y" : "ies")}; " +
                      "the scene now holds one of each.");

            return removed;
        }

        private static int CompareKeepPriority(CardIdentity left, CardIdentity right)
        {
            int byComponents = right.GetComponents<Component>().Length
                               - left.GetComponents<Component>().Length;

            return byComponents != 0
                ? byComponents
                : left.transform.GetSiblingIndex() - right.transform.GetSiblingIndex();
        }

        // ------------------------------------------------------------------------ internals

        private static List<Card> NextPlacementCandidates(
            CardCatalog catalog, out bool placingSecondCopies)
        {
            var candidates = new List<Card>();
            placingSecondCopies = false;

            if (catalog == null)
                return candidates;

            Dictionary<(string, int), int> present = PresentIdentityCounts();
            AddCardsWithSceneCount(catalog, present, requiredCount: 0, candidates);

            if (candidates.Count > 0)
                return candidates;

            placingSecondCopies = true;
            AddCardsWithSceneCount(catalog, present, requiredCount: 1, candidates);
            return candidates;
        }

        private static void AddCardsWithSceneCount(
            CardCatalog catalog,
            IReadOnlyDictionary<(string, int), int> present,
            int requiredCount,
            List<Card> destination)
        {
            // Only a set still short of its quota may offer another second copy. Counted from the
            // scene rather than remembered, so deleting duplicates by hand frees the places again.
            Dictionary<string, int> remaining = requiredCount >= 1
                ? RemainingQuotaBySet(catalog, present)
                : null;

            foreach (Card card in catalog.Cards)
            {
                if (!TryGetPrefabIdentity(card, out (string, int) key))
                    continue;

                if (remaining != null &&
                    (!remaining.TryGetValue(key.Item1, out int left) || left <= 0))
                {
                    continue;
                }

                present.TryGetValue(key, out int count);
                if (count == requiredCount && count < MaxSceneCopiesPerCard)
                    destination.Add(card);
            }
        }

        /// <summary>
        /// How many more duplicates each set may take: its quota less the second copies already in
        /// the scene. A set outside the collection has a quota of zero and never appears here.
        /// </summary>
        private static Dictionary<string, int> RemainingQuotaBySet(
            CardCatalog catalog, IReadOnlyDictionary<(string, int), int> present)
        {
            var remaining = new Dictionary<string, int>();

            foreach (CardSetDefinition set in catalog.Sets)
            {
                int quota = CardDuplicates.QuotaFor(set);
                if (quota > 0)
                    remaining[set.SetId] = quota;
            }

            foreach (KeyValuePair<(string, int), int> entry in present)
            {
                if (entry.Value < MaxSceneCopiesPerCard)
                    continue;

                if (remaining.TryGetValue(entry.Key.Item1, out int left))
                    remaining[entry.Key.Item1] = left - 1;
            }

            return remaining;
        }

        private static Dictionary<(string, int), int> PresentIdentityCounts()
        {
            var present = new Dictionary<(string, int), int>();

            CardIdentity[] identities = Object.FindObjectsByType<CardIdentity>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (CardIdentity identity in identities)
            {
                var key = (identity.SetId, identity.Number);
                present.TryGetValue(key, out int count);
                present[key] = count + 1;
            }

            return present;
        }

        private static bool TryGetPrefabIdentity(Card card, out (string, int) key)
        {
            // Identity is set at runtime in Card.Awake, which has not run on a prefab asset, so
            // read the component straight off the prefab instead of Card.Identity.
            if (card != null && card.TryGetComponent(out CardIdentity identity))
            {
                key = (identity.SetId, identity.Number);
                return true;
            }

            key = default;
            return false;
        }

        private static List<Card> SelectedCards()
        {
            var cards = new List<Card>();

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go != null && go.TryGetComponent(out Card card))
                    cards.Add(card);
            }

            return cards;
        }

        private static bool AllCalm(IReadOnlyList<Rigidbody> targets)
        {
            foreach (Rigidbody body in targets)
            {
                if (body.velocity.sqrMagnitude > LinearSleep * LinearSleep)
                    return false;

                if (body.angularVelocity.sqrMagnitude > AngularSleep * AngularSleep)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Registers the settle as one undo step: rewind to the pre-sim pose, record, then re-apply
        /// the settled pose so Undo has a genuine before/after pair to flip between.
        /// </summary>
        private static void RecordSettledPoses(
            (Transform Transform, Vector3 Position, Quaternion Rotation)[] poses, string undoName)
        {
            var settled = new (Vector3 Position, Quaternion Rotation)[poses.Length];
            var transforms = new Transform[poses.Length];

            for (int i = 0; i < poses.Length; i++)
            {
                Transform t = poses[i].Transform;
                settled[i] = (t.position, t.rotation);
                transforms[i] = t;
                t.SetPositionAndRotation(poses[i].Position, poses[i].Rotation);
            }

            Undo.RecordObjects(transforms, undoName);

            for (int i = 0; i < poses.Length; i++)
                transforms[i].SetPositionAndRotation(settled[i].Position, settled[i].Rotation);
        }

        private static Transform SpawnParent()
        {
            GameObject existing = GameObject.Find(ParentName);
            if (existing != null)
                return existing.transform;

            var root = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(root, "Create Cards Root");
            return root.transform;
        }

        private static Vector3 PivotPoint()
        {
            SceneView view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        private static Quaternion Rotation(RotationStyle style)
        {
            switch (style)
            {
                case RotationStyle.FullRandom:
                    return Random.rotation;

                case RotationStyle.RandomFlat:
                    float pitch = Random.value < 0.5f ? FaceUpPitch : FaceDownPitch;
                    return Quaternion.Euler(
                        pitch + Random.Range(-MaxTilt, MaxTilt),
                        Random.Range(0f, 360f),
                        Random.Range(-MaxTilt, MaxTilt));

                default: // FlatFaceUp
                    return Quaternion.Euler(FaceUpPitch, Random.Range(0f, 360f), 0f);
            }
        }

        private static void MarkActiveSceneDirty()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
