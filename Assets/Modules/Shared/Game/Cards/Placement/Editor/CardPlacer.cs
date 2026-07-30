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
    ///   Spawn   - drops the next card the scene is still missing at the view pivot, face up,
    ///             ready to be dragged into place.
    ///   Release - runs the physics on the selected card(s) only, letting them fall and nestle
    ///             onto everything already placed without shifting the rest of the pile.
    ///
    /// <see cref="CardPlacerWindow"/> adds the one-shot Ctrl+Click that does both at the cursor.
    ///
    /// The cards already carry a Rigidbody and a BoxCollider tuned for a thin-plate pile (see
    /// CardSetBuilder.BuildBasePrefab), so this needs no third-party tool - it just steps Unity's
    /// own PhysX in the editor with <see cref="Physics.Simulate"/>.
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
            List<Rigidbody> targets = SelectedCardBodies();
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
            if (card != null && card.TryGetComponent(out Rigidbody body))
            {
                Simulate(new[] { body }, "Place Card");
                Selection.activeGameObject = card;
            }

            // Collapse spawn + settle so a single Ctrl+Z removes the placed card whole.
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// Instantiates a random card the scene does not have yet at <paramref name="position"/>.
        /// Returns null when the catalog is missing/empty or every card is already placed.
        /// </summary>
        public static GameObject Spawn(Vector3 position, RotationStyle style)
        {
            CardCatalog catalog = Catalog();
            if (catalog == null)
            {
                Debug.LogWarning("[CardPlacer] No CardCatalog found. Pick one in the Card Placer window.");
                return null;
            }

            List<Card> missing = MissingCards(catalog);
            if (missing.Count == 0)
            {
                Debug.Log(catalog.Cards.Count == 0
                    ? "[CardPlacer] The catalog is empty."
                    : "[CardPlacer] Every card in the catalog is already in the scene.");
                return null;
            }

            Card prefab = missing[Random.Range(0, missing.Count)];

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Card");

            instance.transform.SetParent(SpawnParent(), worldPositionStays: true);
            instance.transform.SetPositionAndRotation(position, Rotation(style));
            EditorUtility.SetDirty(instance);

            Debug.Log($"[CardPlacer] Spawned '{instance.name}' - {missing.Count - 1} still missing.", instance);
            return instance;
        }

        /// <summary>
        /// Falls the given cards under gravity while every other card in the scene is frozen into
        /// an immovable collider, so a released card settles onto the existing pile without the
        /// pile settling into itself. The move is a single undoable step.
        /// </summary>
        public static void Simulate(IReadOnlyList<Rigidbody> targets, string undoName)
        {
            if (targets == null || targets.Count == 0)
                return;

            var targetSet = new HashSet<Rigidbody>(targets);
            Rigidbody[] all = Object.FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var savedKinematic = new Dictionary<Rigidbody, bool>(all.Length);
            var savedGravity = new Dictionary<Rigidbody, bool>(targets.Count);
            SimulationMode previousMode = Physics.simulationMode;

            // Pre-sim poses of the targets, captured so the whole thing can be turned into one
            // clean undo entry after the fact - Physics.Simulate writes the transforms in native
            // code, outside anything Undo would otherwise notice.
            var poses = new (Transform Transform, Vector3 Position, Quaternion Rotation)[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                Transform t = targets[i].transform;
                poses[i] = (t, t.position, t.rotation);
            }

            try
            {
                foreach (Rigidbody body in all)
                {
                    savedKinematic[body] = body.isKinematic;

                    if (targetSet.Contains(body))
                    {
                        savedGravity[body] = body.useGravity;
                        body.isKinematic = false;
                        body.useGravity = true;
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    else
                    {
                        // Frozen: still collides with the falling card, but never moves itself.
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
                    calm = AllCalm(targets) ? calm + 1 : 0;
                    if (calm >= StableStepsToStop)
                        break;
                }
            }
            finally
            {
                Physics.simulationMode = previousMode;

                foreach (Rigidbody body in all)
                {
                    if (savedGravity.TryGetValue(body, out bool gravity))
                        body.useGravity = gravity;

                    if (savedKinematic.TryGetValue(body, out bool kinematic))
                        body.isKinematic = kinematic;

                    // Setting velocity on a kinematic body is an error, so only clear the ones
                    // that ended up dynamic again.
                    if (!body.isKinematic)
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
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

        /// <summary>How many cards are already in the scene out of the catalog total.</summary>
        public static void Counts(out int placed, out int total)
        {
            CardCatalog catalog = Catalog();
            total = catalog == null ? 0 : catalog.Cards.Count;
            placed = total - MissingCards(catalog).Count;
        }

        // ------------------------------------------------------------------------ internals

        private static List<Card> MissingCards(CardCatalog catalog)
        {
            var missing = new List<Card>();
            if (catalog == null)
                return missing;

            HashSet<(string, int)> present = PresentIdentities();

            foreach (Card card in catalog.Cards)
            {
                if (card == null)
                    continue;

                // Identity is set at runtime in Card.Awake, which has not run on a prefab asset,
                // so read the component straight off the prefab instead of Card.Identity.
                if (!card.TryGetComponent(out CardIdentity identity))
                    continue;

                if (!present.Contains((identity.SetId, identity.Number)))
                    missing.Add(card);
            }

            return missing;
        }

        private static HashSet<(string, int)> PresentIdentities()
        {
            var present = new HashSet<(string, int)>();

            CardIdentity[] identities = Object.FindObjectsByType<CardIdentity>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (CardIdentity identity in identities)
                present.Add((identity.SetId, identity.Number));

            return present;
        }

        private static List<Rigidbody> SelectedCardBodies()
        {
            var bodies = new List<Rigidbody>();

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go != null && go.TryGetComponent(out Card _) && go.TryGetComponent(out Rigidbody body))
                    bodies.Add(body);
            }

            return bodies;
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
