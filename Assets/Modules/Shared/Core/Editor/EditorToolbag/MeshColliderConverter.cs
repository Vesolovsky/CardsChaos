using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vesolovsky.Core.EditorToolbag
{
    /// <summary>
    /// Swaps mesh colliders for box colliders across whatever the open scene actually uses.
    ///
    /// The colliders almost all arrive through prefabs, so the swap is made on the prefab asset
    /// rather than on each instance. Done per instance, every one of the hundreds in the scene
    /// would end up carrying a removed-component override and an added-component override, and
    /// the scene file would grow by more than the change is worth.
    ///
    /// A box is a lossy stand-in for a mesh: it is the mesh's bounding box, so anything with a
    /// hollow, an arch or a gap underneath comes out solid. Run the report first - it ranks the
    /// meshes by how much of their own bounding box they actually fill, and the ones at the top
    /// of that list are the ones a box will misrepresent.
    /// </summary>
    public static class MeshColliderConverter
    {
        private const string MenuRoot = "Tools/Physics/";

        [MenuItem(MenuRoot + "Report Mesh Colliders In Open Scene")]
        private static void Report()
        {
            List<MeshCollider> colliders = FindInOpenScene();
            if (colliders.Count == 0)
            {
                Debug.Log("[MeshColliderConverter] No mesh colliders in the open scene.");
                return;
            }

            Split(colliders, out HashSet<string> prefabPaths, out List<MeshCollider> loose);

            // Keyed by mesh, because the same mesh usually turns up on dozens of instances and
            // the fill ratio is a property of the mesh, not of where it was dropped.
            var fill = new Dictionary<Mesh, float>();

            foreach (MeshCollider collider in colliders)
            {
                Mesh mesh = MeshOf(collider);
                if (mesh != null && !fill.ContainsKey(mesh))
                    fill[mesh] = FillRatio(mesh);
            }

            var report = new StringBuilder();
            report.AppendLine($"[MeshColliderConverter] {colliders.Count} mesh colliders in the " +
                              $"open scene: {prefabPaths.Count} prefab assets, {loose.Count} loose " +
                              "scene objects.");
            report.AppendLine();
            report.AppendLine("Worst box candidates (share of their own bounding box the mesh " +
                              "fills - low means a box will seal a lot of empty space):");

            foreach (KeyValuePair<Mesh, float> entry in fill.OrderBy(e => e.Value).Take(25))
                report.AppendLine($"  {entry.Value:P0}  {entry.Key.name}");

            Debug.Log(report.ToString());
        }

        [MenuItem(MenuRoot + "Convert Mesh Colliders In Open Scene")]
        private static void ConvertOpenScene()
        {
            List<MeshCollider> colliders = FindInOpenScene();
            if (colliders.Count == 0)
            {
                Debug.Log("[MeshColliderConverter] No mesh colliders in the open scene.");
                return;
            }

            Split(colliders, out HashSet<string> prefabPaths, out List<MeshCollider> loose);

            bool confirmed = EditorUtility.DisplayDialog(
                "Convert mesh colliders to boxes",
                $"{colliders.Count} mesh colliders found.\n\n" +
                $"{prefabPaths.Count} prefab assets will be rewritten on disk, affecting every " +
                $"scene that uses them.\n{loose.Count} loose scene objects will be changed, and " +
                "those can be undone.\n\nPrefab edits cannot be undone - revert them through " +
                "version control if the result is wrong.",
                "Convert",
                "Cancel");

            if (!confirmed)
                return;

            Convert(prefabPaths, loose);
        }

        [MenuItem(MenuRoot + "Convert Mesh Colliders In Selection")]
        private static void ConvertSelection()
        {
            List<MeshCollider> colliders = Selection.gameObjects
                .SelectMany(o => o.GetComponentsInChildren<MeshCollider>(true))
                .Distinct()
                .ToList();

            if (colliders.Count == 0)
            {
                Debug.Log("[MeshColliderConverter] Nothing selected has a mesh collider.");
                return;
            }

            Split(colliders, out HashSet<string> prefabPaths, out List<MeshCollider> loose);
            Convert(prefabPaths, loose);
        }

        private static void Convert(HashSet<string> prefabPaths, List<MeshCollider> loose)
        {
            int converted = 0;
            int skipped = 0;

            foreach (string path in prefabPaths)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;

                    foreach (MeshCollider collider in contents.GetComponentsInChildren<MeshCollider>(true))
                    {
                        if (Replace(collider, undoable: false))
                        {
                            converted++;
                            changed = true;
                        }
                        else
                        {
                            skipped++;
                        }
                    }

                    if (changed)
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            Undo.SetCurrentGroupName("Convert mesh colliders to boxes");
            int group = Undo.GetCurrentGroup();

            foreach (MeshCollider collider in loose)
            {
                if (Replace(collider, undoable: true))
                    converted++;
                else
                    skipped++;
            }

            Undo.CollapseUndoOperations(group);

            AssetDatabase.SaveAssets();

            Debug.Log($"[MeshColliderConverter] Converted {converted} colliders across " +
                      $"{prefabPaths.Count} prefabs and {loose.Count} scene objects. " +
                      $"{skipped} left alone.");
        }

        /// <summary>
        /// Sorts colliders into the prefab assets they came from and the ones that live only in
        /// the scene. A collider added on top of a prefab instance has no source to fix, so it
        /// counts as a scene object.
        /// </summary>
        private static void Split(
            IEnumerable<MeshCollider> colliders,
            out HashSet<string> prefabPaths,
            out List<MeshCollider> loose)
        {
            prefabPaths = new HashSet<string>();
            loose = new List<MeshCollider>();

            foreach (MeshCollider collider in colliders)
            {
                if (collider == null)
                    continue;

                if (PrefabUtility.IsPartOfPrefabInstance(collider))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(collider);
                    string path = source != null ? AssetDatabase.GetAssetPath(source) : null;

                    if (!string.IsNullOrEmpty(path))
                    {
                        prefabPaths.Add(path);
                        continue;
                    }
                }

                loose.Add(collider);
            }
        }

        private static bool Replace(MeshCollider collider, bool undoable)
        {
            GameObject host = collider.gameObject;
            Mesh mesh = MeshOf(collider);

            if (mesh == null)
            {
                Debug.LogWarning($"[MeshColliderConverter] '{host.name}' has no mesh to measure; " +
                                 "left as a mesh collider.", host);

                return false;
            }

            // Mesh bounds are already the local space AABB, which is the space a box collider is
            // authored in - so no transform maths, and the box keeps up with any scale on the
            // object for free.
            Bounds bounds = mesh.bounds;

            bool isTrigger = collider.isTrigger;
            bool enabled = collider.enabled;
            PhysicMaterial material = collider.sharedMaterial;

            // Added before the old one is destroyed, so the object is never briefly without a
            // collider for anything that declares it requires one.
            BoxCollider box = undoable
                ? Undo.AddComponent<BoxCollider>(host)
                : host.AddComponent<BoxCollider>();

            box.center = bounds.center;
            box.size = bounds.size;
            box.isTrigger = isTrigger;
            box.enabled = enabled;
            box.sharedMaterial = material;

            if (undoable)
                Undo.DestroyObjectImmediate(collider);
            else
                Object.DestroyImmediate(collider);

            return true;
        }

        /// <summary>Every mesh collider in the loaded scenes, active or not.</summary>
        private static List<MeshCollider> FindInOpenScene()
        {
            return Object
                .FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToList();
        }

        private static Mesh MeshOf(MeshCollider collider)
        {
            if (collider.sharedMesh != null)
                return collider.sharedMesh;

            // An empty mesh collider falls back to the renderer's mesh, which is what it would
            // have picked up had anyone assigned it.
            var filter = collider.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>
        /// How much of its own bounding box a mesh actually occupies, 0 to 1.
        ///
        /// Signed tetrahedron volumes against the origin, which only adds up to the real volume
        /// if the mesh is closed. Furniture generally is, and where it is not the number comes
        /// out low - which happens to be the same answer a hollow shape deserves anyway.
        /// </summary>
        private static float FillRatio(Mesh mesh)
        {
            Bounds bounds = mesh.bounds;
            float boxVolume = bounds.size.x * bounds.size.y * bounds.size.z;

            if (boxVolume <= 0f || !mesh.isReadable)
                return 1f;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            double volume = 0.0;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }

            return Mathf.Clamp01((float)(Mathf.Abs((float)volume) / boxVolume));
        }
    }
}
