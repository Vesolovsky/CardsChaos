using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards.CardEditor
{
    /// <summary>
    /// Shape of a single card. The outline is a superellipse rounded rectangle whose
    /// corner profile was measured off the artwork itself (see <see cref="Default"/>),
    /// so the mesh silhouette lands exactly on the boundary of the printed card face.
    /// </summary>
    public struct CardMeshSettings
    {
        public float Width;
        public float Height;
        public float Thickness;

        /// <summary>Corner radius in world units.</summary>
        public float CornerRadius;

        /// <summary>Superellipse exponent. 2 is a plain circle, lower is more clipped.</summary>
        public float Squareness;

        public int CornerSegments;
        public int RimSegments;

        public Vector2 TextureSize;

        /// <summary>
        /// Pulls the UVs in by this many texture pixels so the outermost geometry never
        /// samples the black band that sits outside the card face in the source art.
        /// </summary>
        public float UvInsetPixels;

        // Measured from Sets/BirdsOfTheSun/03_FlareHeron.png (1024x1536):
        // corner radius 48.5 px, superellipse exponent 1.73, identical on all four corners.
        public const float MeasuredCornerRadiusPixels = 48.5f;
        public const float MeasuredSquareness = 1.73f;

        public static CardMeshSettings Default
        {
            get
            {
                // Real playing card proportions; the art is 2:3 so height follows from width.
                const float width = 0.063f;
                return new CardMeshSettings
                {
                    Width = width,
                    Height = width * 1.5f,
                    // Far chunkier than a real card (~0.3 mm): thin plates z-fight badly
                    // where they stack or overlap on the table, and the extra depth also
                    // lets the rounded rim read as 3D.
                    Thickness = 0.0025f,
                    CornerRadius = MeasuredCornerRadiusPixels / 1024f * width,
                    Squareness = MeasuredSquareness,
                    CornerSegments = 14,
                    RimSegments = 5,
                    TextureSize = new Vector2(1024f, 1536f),
                    UvInsetPixels = 6f,
                };
            }
        }
    }

    /// <summary>
    /// Builds the shared card mesh.
    ///
    /// Layout conventions the shader and gameplay code depend on:
    ///   - The front face points along +Z, so transform.forward is the card's facing.
    ///   - uv0 addresses the front texture, uv1 addresses the back texture (mirrored in X).
    ///   - Vertex colour R is 1 on the front face, 0 on the back face and sweeps between
    ///     them across the rim, which lets the rim tint itself from both textures.
    /// </summary>
    public static class CardMeshBuilder
    {
        public static Mesh Build(CardMeshSettings s)
        {
            Vector2[] outline = BuildOutline(s);
            Vector2[] outward = BuildOutwardNormals(outline);

            int n = outline.Length;
            float halfThickness = s.Thickness * 0.5f;

            // The rim is a half-round of radius halfThickness, so the flat faces sit
            // that far inside the silhouette.
            var faceOutline = new Vector2[n];
            for (int i = 0; i < n; i++)
                faceOutline[i] = outline[i] - outward[i] * halfThickness;

            float uvScaleX = 1f - 2f * s.UvInsetPixels / s.TextureSize.x;
            float uvScaleY = 1f - 2f * s.UvInsetPixels / s.TextureSize.y;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            // The front face points along +Z, so it is viewed from +Z looking back down
            // the axis - and from there world +X runs to the viewer's left. The front UVs
            // therefore have to flip X, and the back face, seen from the other side, does not.
            Vector2 FrontUv(Vector2 p) => new Vector2(
                (-p.x / s.Width) * uvScaleX + 0.5f,
                (p.y / s.Height) * uvScaleY + 0.5f);

            Vector2 BackUv(Vector2 p) => new Vector2(
                (p.x / s.Width) * uvScaleX + 0.5f,
                (p.y / s.Height) * uvScaleY + 0.5f);

            void AddVertex(Vector3 pos, Vector3 nrm, float faceMix)
            {
                vertices.Add(pos);
                normals.Add(nrm);
                uv0.Add(FrontUv(pos));
                uv1.Add(BackUv(pos));
                colors.Add(new Color(faceMix, 0f, 0f, 1f));
            }

            // ---- front face (normal +Z, fan from the centre) ----
            int frontCentre = vertices.Count;
            AddVertex(new Vector3(0f, 0f, halfThickness), Vector3.forward, 1f);
            int frontRing = vertices.Count;
            for (int i = 0; i < n; i++)
                AddVertex(new Vector3(faceOutline[i].x, faceOutline[i].y, halfThickness), Vector3.forward, 1f);

            for (int i = 0; i < n; i++)
            {
                triangles.Add(frontCentre);
                triangles.Add(frontRing + i);
                triangles.Add(frontRing + (i + 1) % n);
            }

            // ---- back face (normal -Z, reversed winding) ----
            int backCentre = vertices.Count;
            AddVertex(new Vector3(0f, 0f, -halfThickness), Vector3.back, 0f);
            int backRing = vertices.Count;
            for (int i = 0; i < n; i++)
                AddVertex(new Vector3(faceOutline[i].x, faceOutline[i].y, -halfThickness), Vector3.back, 0f);

            for (int i = 0; i < n; i++)
            {
                triangles.Add(backCentre);
                triangles.Add(backRing + (i + 1) % n);
                triangles.Add(backRing + i);
            }

            // ---- rim: rings sweeping from the front plane round to the back plane ----
            int rimSegments = Mathf.Max(1, s.RimSegments);
            int rimStart = vertices.Count;

            for (int j = 0; j <= rimSegments; j++)
            {
                float angle = Mathf.PI * 0.5f - Mathf.PI * j / rimSegments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float faceMix = 0.5f + 0.5f * sin;

                for (int i = 0; i < n; i++)
                {
                    Vector2 xy = faceOutline[i] + outward[i] * (halfThickness * cos);
                    Vector3 pos = new Vector3(xy.x, xy.y, halfThickness * sin);
                    Vector3 nrm = new Vector3(outward[i].x * cos, outward[i].y * cos, sin).normalized;
                    AddVertex(pos, nrm, faceMix);
                }
            }

            for (int j = 0; j < rimSegments; j++)
            {
                int ringA = rimStart + j * n;
                int ringB = rimStart + (j + 1) * n;
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    int a = ringA + i;
                    int b = ringA + next;
                    int c = ringB + i;
                    int d = ringB + next;

                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);

                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = "CardMesh" };
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            return mesh;
        }

        /// <summary>Counter-clockwise superellipse rounded rectangle centred on the origin.</summary>
        private static Vector2[] BuildOutline(CardMeshSettings s)
        {
            float halfWidth = s.Width * 0.5f;
            float halfHeight = s.Height * 0.5f;
            float radius = Mathf.Min(s.CornerRadius, Mathf.Min(halfWidth, halfHeight));
            float exponent = 2f / Mathf.Max(0.1f, s.Squareness);
            int segments = Mathf.Max(2, s.CornerSegments);

            var quadrants = new[]
            {
                new Vector2(1f, 1f),
                new Vector2(-1f, 1f),
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f),
            };

            var points = new List<Vector2>((segments + 1) * 4);

            foreach (Vector2 sign in quadrants)
            {
                var centre = new Vector2(sign.x * (halfWidth - radius), sign.y * (halfHeight - radius));

                // Quadrants alternate direction so the whole outline stays counter-clockwise.
                bool forward = sign.x * sign.y > 0f;

                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float phi = (forward ? t : 1f - t) * Mathf.PI * 0.5f;

                    // Cos/Sin overshoot into tiny negatives at the quadrant ends, and
                    // Pow of a negative base with a fractional exponent is NaN.
                    float x = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(phi)), exponent);
                    float y = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phi)), exponent);
                    points.Add(centre + new Vector2(sign.x * radius * x, sign.y * radius * y));
                }
            }

            return points.ToArray();
        }

        private static Vector2[] BuildOutwardNormals(Vector2[] outline)
        {
            int n = outline.Length;
            var edgeNormals = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                Vector2 dir = (outline[(i + 1) % n] - outline[i]).normalized;
                edgeNormals[i] = new Vector2(dir.y, -dir.x);
            }

            var vertexNormals = new Vector2[n];
            for (int i = 0; i < n; i++)
                vertexNormals[i] = (edgeNormals[(i - 1 + n) % n] + edgeNormals[i]).normalized;

            return vertexNormals;
        }
    }
}
