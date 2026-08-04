using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Edit-mode authoring that arranges a selection of cards into a standing house of cards.
    ///
    /// The house is a classic pyramid of leaning "tents" (two cards each) bridged by flat cards that
    /// carry the tent above. The poses are computed analytically rather than dropped with physics -
    /// two cards would never settle into a Λ on their own - and every card's foot is placed exactly
    /// on the floor, so nothing levitates. The result is a <see cref="CardHouse"/> whose collapse is
    /// handled at runtime.
    ///
    /// A pyramid of L levels needs L(3L+1)/2 cards: 2, 7, 15, 26, ... The selection count picks the
    /// size; anything that is not one of those is turned away with the valid counts.
    /// </summary>
    public static class CardHouseBuilder
    {
        // The card mesh lies in its local XY plane (width along X, height along Y) with its face
        // along +Z - the same convention CardPlacer and the mesh builder use.
        private const string ParentName = "EnviroCards";
        private const string Prefix = "CardsChaos.CardHouse.";

        // Apex-to-apex spacing as a fraction of card height. A bridge is one card (height) long and
        // spans one gap, so the spacing must be at least a card or neighbouring bridges overlap;
        // the sliver over 1 leaves a hair of gap between them and seats each bridge just inside the
        // two apexes it rests on rather than balancing on their very edges.
        private const float SpanFactor = 1.02f;

        // Fallback card size if a selected card carries no BoxCollider to measure.
        private static readonly Vector3 DefaultCardSize = new Vector3(0.063f, 0.0945f, 0.0025f);

        /// <summary>How far each tent card leans off vertical, in degrees. Tunable from the window.</summary>
        public static float LeanDegrees
        {
            get => EditorPrefs.GetFloat(Prefix + "Lean", 20f);
            set => EditorPrefs.SetFloat(Prefix + "Lean", Mathf.Clamp(value, 5f, 40f));
        }

        [Shortcut("CardsChaos/Build House From Selection")]
        public static void BuildShortcut() => BuildFromSelection();

        /// <summary>How many of the currently selected objects are cards.</summary>
        public static int SelectedCardCount() => SelectedCards().Count;

        /// <summary>Levels of the pyramid a given card count builds, or false if it is not a valid size.</summary>
        public static bool TryLevelsFor(int cardCount, out int levels)
        {
            for (int l = 1; l <= 12; l++)
            {
                int need = CardsForLevels(l);
                if (need == cardCount)
                {
                    levels = l;
                    return true;
                }

                if (need > cardCount)
                    break;
            }

            levels = 0;
            return false;
        }

        /// <summary>A human-readable list of the first few buildable card counts, e.g. "2, 7, 15, 26".</summary>
        public static string ValidCountsString(int howMany)
        {
            var sb = new StringBuilder();
            for (int l = 1; l <= howMany; l++)
            {
                if (l > 1)
                    sb.Append(", ");

                sb.Append(CardsForLevels(l));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Arranges the selected cards into a house. No-op with a warning when nothing card-like is
        /// selected or the count is not one a pyramid can be built from.
        /// </summary>
        public static void BuildFromSelection()
        {
            List<Card> cards = SelectedCards();
            if (cards.Count == 0)
            {
                Debug.LogWarning("[CardHouse] Select the cards to arrange first.");
                return;
            }

            if (!TryLevelsFor(cards.Count, out int levels))
            {
                Debug.LogWarning(
                    $"[CardHouse] {cards.Count} cards selected, which builds no full house. " +
                    $"Select one of: {ValidCountsString(6)} cards.");
                return;
            }

            Vector3 cardSize = CardSize(cards[0]);
            List<Pose> poses = LocalPoses(levels, LeanDegrees, cardSize);
            if (poses.Count != cards.Count)
            {
                Debug.LogError(
                    $"[CardHouse] Geometry made {poses.Count} slots for {cards.Count} cards; aborting.");
                return;
            }

            Vector3 anchor = FloorCentroid(cards);
            float yaw = FacingYaw(anchor);

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Card House");

            var rootGo = new GameObject("CardHouse");
            Undo.RegisterCreatedObjectUndo(rootGo, "Build Card House");
            rootGo.transform.SetParent(SpawnParent(), worldPositionStays: true);
            rootGo.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, yaw, 0f));

            CardHouse house = Undo.AddComponent<CardHouse>(rootGo);

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                StripBody(card);

                Undo.SetTransformParent(card.transform, rootGo.transform, "Build Card House");
                Undo.RecordObject(card.transform, "Build Card House");
                card.transform.localScale = Vector3.one;
                card.transform.localPosition = poses[i].position;
                card.transform.localRotation = poses[i].rotation;
            }

            Undo.RecordObject(house, "Build Card House");
            house.Configure(cards);

            Undo.CollapseUndoOperations(group);

            EditorUtility.SetDirty(house);
            MarkActiveSceneDirty();
            Selection.activeGameObject = rootGo;

            Debug.Log($"[CardHouse] Built a {levels}-level house from {cards.Count} cards.", rootGo);
        }

        // ------------------------------------------------------------------------- geometry

        private static int CardsForLevels(int levels) => levels * (3 * levels + 1) / 2;

        /// <summary>
        /// The pose of every card of an L-level pyramid, in the house's own space: X runs along the
        /// row of tents, Y is up, Z is the depth the ridges run along. The bottom feet sit at Y=0.
        /// </summary>
        private static List<Pose> LocalPoses(int levels, float leanDegrees, Vector3 cardSize)
        {
            float height = cardSize.y;
            float thickness = cardSize.z;

            float theta = leanDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(theta);
            float cos = Mathf.Cos(theta);

            float halfBase = height * sin; // horizontal reach from ridge down to a foot
            float apexHeight = height * cos; // a tent apex above its own base
            float span = height * SpanFactor; // apex-to-apex spacing along the row
            float levelHeight = apexHeight + thickness; // rise from one level's base to the next

            var poses = new List<Pose>();

            for (int k = 0; k < levels; k++)
            {
                int tents = levels - k;
                float baseY = k * levelHeight;

                // Each level up carries one fewer tent, nudged half a span towards the middle so it
                // stands over the gaps below.
                float rowOffset = (k * 0.5f - (levels - 1) * 0.5f) * span;

                for (int i = 0; i < tents; i++)
                {
                    float cx = rowOffset + i * span;
                    AddTent(poses, cx, baseY, halfBase, apexHeight, sin, cos);
                }

                // A bridge spans each gap in this level and forms the floor for the tent above it.
                for (int i = 0; i < tents - 1; i++)
                {
                    float cx = rowOffset + i * span + span * 0.5f;
                    float by = baseY + apexHeight + thickness * 0.5f;
                    poses.Add(new Pose(new Vector3(cx, by, 0f), BridgeRotation()));
                }
            }

            return poses;
        }

        private static void AddTent(
            List<Pose> poses, float cx, float baseY, float halfBase, float apexHeight, float sin, float cos)
        {
            float centreY = baseY + apexHeight * 0.5f;

            // Right card: foot at +halfBase, leaning up to the apex at cx; face turned up and out.
            poses.Add(new Pose(
                new Vector3(cx + halfBase * 0.5f, centreY, 0f),
                Quaternion.LookRotation(new Vector3(cos, sin, 0f), new Vector3(-sin, cos, 0f))));

            // Left card: the mirror of it.
            poses.Add(new Pose(
                new Vector3(cx - halfBase * 0.5f, centreY, 0f),
                Quaternion.LookRotation(new Vector3(-cos, sin, 0f), new Vector3(sin, cos, 0f))));
        }

        // Flat and face up (the -90 pitch), turned 90 so its length runs along the row to span the
        // gap while its width sits along the depth the ridges run in.
        private static Quaternion BridgeRotation() => Quaternion.Euler(-90f, 90f, 0f);

        // ------------------------------------------------------------------------- placement

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

        private static Vector3 CardSize(Card card)
        {
            if (card != null && card.TryGetComponent(out BoxCollider box))
            {
                Vector3 scale = card.transform.lossyScale;
                return new Vector3(
                    box.size.x * Mathf.Abs(scale.x),
                    box.size.y * Mathf.Abs(scale.y),
                    box.size.z * Mathf.Abs(scale.z));
            }

            return DefaultCardSize;
        }

        // The floor to stand the house on is taken as the lowest point of the cards being arranged -
        // they are resting on it already - centred on their average X and Z.
        private static Vector3 FloorCentroid(List<Card> cards)
        {
            Vector3 sum = Vector3.zero;
            float floorY = float.PositiveInfinity;

            foreach (Card card in cards)
            {
                sum += card.transform.position;

                if (card.TryGetComponent(out Collider col))
                    floorY = Mathf.Min(floorY, col.bounds.min.y);
                else
                    floorY = Mathf.Min(floorY, card.transform.position.y);
            }

            if (float.IsInfinity(floorY))
                floorY = 0f;

            Vector3 centroid = sum / cards.Count;
            return new Vector3(centroid.x, floorY, centroid.z);
        }

        // Turn the house so its depth faces the scene camera - that is the angle the triangular
        // silhouette reads from. Falls back to no turn when there is no scene view to read.
        private static float FacingYaw(Vector3 anchor)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
                return 0f;

            Vector3 dir = view.camera.transform.position - anchor;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
                return 0f;

            return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
        }

        private static void StripBody(Card card)
        {
            // Resting cards carry only a BoxCollider; clear any stray Rigidbody left from editing and
            // make sure the collider is solid so the standing cards actually lean on each other.
            if (card.TryGetComponent(out Rigidbody body))
                Undo.DestroyObjectImmediate(body);

            if (card.TryGetComponent(out BoxCollider box) && box.isTrigger)
            {
                Undo.RecordObject(box, "Build Card House");
                box.isTrigger = false;
            }
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

        private static void MarkActiveSceneDirty()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
