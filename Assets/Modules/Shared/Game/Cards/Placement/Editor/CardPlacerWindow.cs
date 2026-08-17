using UnityEditor;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Dashboard for <see cref="CardPlacer"/>: the collection counter, the tunables the two
    /// shortcuts read, and the Ctrl+Click placement mode that spawns and settles a card wherever
    /// the cursor is in the Scene view - the fast path for scattering a whole set.
    /// </summary>
    public class CardPlacerWindow : EditorWindow
    {
        private bool _clickToPlace;
        private int _controlId;

        [MenuItem("Tools/Cards/Card Placer")]
        private static void Open()
        {
            GetWindow<CardPlacerWindow>("Card Placer").Show();
        }

        private void OnDisable()
        {
            // A closed window must not keep intercepting Scene clicks.
            SetClickToPlace(false);
        }

        // The house section reports the live selection count, which the window would otherwise not
        // notice changing.
        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.Space();
            DrawCatalog();
            EditorGUILayout.Space();
            DrawSettings();
            EditorGUILayout.Space();
            DrawClickToPlace();
            EditorGUILayout.Space();
            DrawHouse();
            EditorGUILayout.Space();
            DrawHelp();
        }

        private void DrawCatalog()
        {
            CardCatalog catalog = CardPlacer.Catalog();

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                var picked = (CardCatalog)EditorGUILayout.ObjectField(
                    "Catalog", catalog, typeof(CardCatalog), allowSceneObjects: false);

                if (change.changed)
                {
                    CardPlacer.CatalogGuid = picked == null
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked));
                }
            }

            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No CardCatalog found in the project.", MessageType.Warning);
                return;
            }

            CardPlacer.Counts(
                out int placed, out int duplicates, out int total, out int duplicateTotal);
            EditorGUILayout.LabelField("Placed", $"{placed} / {total}");

            if (total > 0)
            {
                Rect bar = EditorGUILayout.GetControlRect(false, 6f);
                EditorGUI.ProgressBar(bar, placed / (float)total, string.Empty);

                // Only a share of each set gets a second copy, so this is measured against the
                // quota the sets add up to rather than against the catalog.
                EditorGUILayout.LabelField("Duplicates", $"{duplicates} / {duplicateTotal}");
                Rect duplicateBar = EditorGUILayout.GetControlRect(false, 6f);
                EditorGUI.ProgressBar(
                    duplicateBar,
                    duplicateTotal > 0 ? duplicates / (float)duplicateTotal : 0f,
                    string.Empty);
            }

            if (duplicates >= duplicateTotal && duplicateTotal > 0)
            {
                EditorGUILayout.HelpBox(
                    "Every duplicate is on the scene. Nothing is left to place.", MessageType.Info);
            }
            else if (placed >= total && total > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Every card is on the scene once. The placer is now adding the " +
                    $"{duplicateTotal} duplicates - about {Mathf.RoundToInt(CardDuplicates.Share * 100f)}% " +
                    $"of each set, rounded to fives, and none at all for the smallest sets.",
                    MessageType.Info);
            }

            if (duplicates > 0)
            {
                EditorGUILayout.HelpBox(
                    "A second copy is the card's duplicate: the album takes the first, the " +
                    "duplicate box takes the second, and the save keeps the two apart.",
                    MessageType.Info);
            }

            DrawBoxCapacity(duplicateTotal);

            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent(
                    "Remove every extra copy",
                    "Deletes every card the scene holds more than once, leaving one of each. " +
                    "Undoable in one step.")))
            {
                RemoveExtraCopies(duplicates);
            }
        }

        /// <summary>
        /// Every duplicate is meant to fit in the room's duplicate box, and the quota grows with
        /// the catalog while the box does not - so the day a new set tips it over should be the day
        /// this says so, not the day a player runs out of slots.
        /// </summary>
        private static void DrawBoxCapacity(int duplicateTotal)
        {
            CardStackContainer[] boxes = Object.FindObjectsByType<CardStackContainer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (boxes.Length == 0)
                return;

            int capacity = 0;
            foreach (CardStackContainer box in boxes)
                capacity += box.Capacity;

            string where = boxes.Length == 1 ? "The box holds" : $"The {boxes.Length} boxes hold";

            EditorGUILayout.HelpBox(
                duplicateTotal > capacity
                    ? $"{where} {capacity} cards, but the sets ask for {duplicateTotal} " +
                      "duplicates. Some would have nowhere to go - grow the box or lower the share."
                    : $"{where} {capacity} cards; the sets ask for {duplicateTotal}.",
                duplicateTotal > capacity ? MessageType.Error : MessageType.None);
        }

        private static void RemoveExtraCopies(int duplicates)
        {
            if (duplicates <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Nothing to remove", "No card is in the scene more than once.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Remove extra copies",
                $"Delete {duplicates} card copies, leaving one of each in the scene?\n\n" +
                "The copy carrying extra components is kept, so the authored endgame card stays.",
                "Remove", "Cancel");

            if (confirmed)
                CardPlacer.RemoveExtraCopies();
        }

        private void DrawSettings()
        {
            CardPlacer.SpawnRotation = (CardPlacer.RotationStyle)EditorGUILayout.EnumPopup(
                new GUIContent("Spawn rotation", "Orientation for cards dropped by the Spawn shortcut."),
                CardPlacer.SpawnRotation);

            CardPlacer.ClickRotation = (CardPlacer.RotationStyle)EditorGUILayout.EnumPopup(
                new GUIContent("Ctrl+Click rotation", "Orientation for cards placed by Ctrl+Click."),
                CardPlacer.ClickRotation);

            CardPlacer.MaxTilt = EditorGUILayout.Slider(
                new GUIContent("Max tilt", "Random lean off flat, in degrees, for the flat rotation styles."),
                CardPlacer.MaxTilt, 0f, 45f);

            CardPlacer.DropHeight = EditorGUILayout.FloatField(
                new GUIContent("Drop height (m)", "How far above the click point a Ctrl+Click card falls from."),
                CardPlacer.DropHeight);

            CardPlacer.MaxSimSteps = EditorGUILayout.IntField(
                new GUIContent("Max sim steps", "Physics-step ceiling per settle so a restless card cannot hang the editor."),
                CardPlacer.MaxSimSteps);
        }

        private void DrawClickToPlace()
        {
            bool toggle = EditorGUILayout.ToggleLeft(
                new GUIContent("Ctrl+Click to place in Scene",
                    "While on, Ctrl+Click in the Scene view spawns the next card at the cursor and settles it."),
                _clickToPlace);

            if (toggle != _clickToPlace)
                SetClickToPlace(toggle);

            if (_clickToPlace)
            {
                EditorGUILayout.HelpBox(
                    "Ctrl+Click in the Scene view now spawns and settles a card. " +
                    "Normal Ctrl+Click selection is off until you turn this back off.",
                    MessageType.None);
            }
        }

        private static void DrawHouse()
        {
            EditorGUILayout.LabelField("Card House", EditorStyles.boldLabel);

            CardHouseBuilder.LeanDegrees = EditorGUILayout.Slider(
                new GUIContent("Lean", "How far each tent card leans off vertical, in degrees."),
                CardHouseBuilder.LeanDegrees, 5f, 40f);

            int selected = CardHouseBuilder.SelectedCardCount();
            bool valid = CardHouseBuilder.TryLevelsFor(selected, out int levels);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button(valid
                        ? $"Build {levels}-level house from {selected} cards"
                        : "Build house from selection"))
                {
                    CardHouseBuilder.BuildFromSelection();
                }
            }

            EditorGUILayout.HelpBox(
                valid
                    ? $"{selected} cards selected - arranges into a {levels}-level house on the floor " +
                      "beneath them. Pull any card in play and the rest come down."
                    : $"Select cards, then build. A house needs one of: " +
                      $"{CardHouseBuilder.ValidCountsString(6)} cards (selected now: {selected}).",
                valid ? MessageType.Info : MessageType.None);
        }

        private static void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "Shortcuts (rebind under Edit > Shortcuts > CardsChaos):\n" +
                "   G  -  Spawn Next Card at the view pivot\n" +
                "   H  -  Release Selected (settle with physics)\n\n" +
                "Flow: G to spawn, drag it where you want, H to drop it onto the pile.\n" +
                "Or turn on Ctrl+Click above and place cards in one click each.",
                MessageType.Info);
        }

        private void SetClickToPlace(bool enabled)
        {
            if (enabled == _clickToPlace)
                return;

            _clickToPlace = enabled;

            if (enabled)
                SceneView.duringSceneGui += OnSceneGui;
            else
                SceneView.duringSceneGui -= OnSceneGui;

            SceneView.RepaintAll();
        }

        private void OnSceneGui(SceneView view)
        {
            Event e = Event.current;

            // Reserve a control id every pass so hotControl stays consistent between the down and
            // up events of one click.
            _controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.MouseDown && e.button == 0 && e.control && !e.alt && !e.shift)
            {
                if (TryPointUnderMouse(e.mousePosition, out Vector3 point))
                    CardPlacer.SpawnAndRelease(point);

                // Grab the click so Unity does not also run its own selection/marquee on it.
                GUIUtility.hotControl = _controlId;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && GUIUtility.hotControl == _controlId)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private static bool TryPointUnderMouse(Vector2 mousePosition, out Vector3 point)
        {
            // Transforms may have moved since the last query; sync so the raycast sees them.
            Physics.SyncTransforms();

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                point = hit.point;
                return true;
            }

            // Nothing under the cursor - fall back to the Y=0 plane so a click on empty space
            // still lands a card somewhere sensible.
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }
    }
}
