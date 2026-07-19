using UnityEngine;

namespace Vesolovsky.Core.Utils.Extensions
{
    public static class VectorExtensions
    {
        #region Set vectors
        public static Vector3 SetX(this Vector3 vector, float x)
        {
            return new Vector3(x, vector.y, vector.z);
        }

        public static Vector3 SetY(this Vector3 vector, float y)
        {
            return new Vector3(vector.x, y, vector.z);
        }

        public static Vector3 SetZ(this Vector3 vector, float z)
        {
            return new Vector3(vector.x, vector.y, z);
        }

        public static Vector2 SetX(this Vector2 vector, float x)
        {
            return new Vector2(x, vector.y);
        }

        public static Vector2 SetY(this Vector2 vector, float y)
        {
            return new Vector2(vector.x, y);
        }
        #endregion

        #region Vectors operations
        public static Vector3 AddX(this Vector3 vector, float x)
        {
            return vector.SetX(vector.x + x);
        }

        public static Vector3 AddY(this Vector3 vector, float y)
        {
            return vector.SetY(vector.y + y);
        }

        public static Vector3 AddZ(this Vector3 vector, float z)
        {
            return vector.SetZ(vector.z + z);
        }

        public static Vector2 AddX(this Vector2 vector, float x)
        {
            return vector.SetX(vector.x + x);
        }

        public static Vector2 AddY(this Vector2 vector, float y)
        {
            return vector.SetY(vector.y + y);
        }
        #endregion

        public static Vector3 GetUniformVector(float value)
        {
            return new Vector3(value, value, value);
        }

        public static Vector3 GetMidpoint(Vector3 pointA, Vector3 pointB)
        {
            return (pointA + pointB) / 2;
        }
    }
}