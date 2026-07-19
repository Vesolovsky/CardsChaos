using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Core.Utils.Extensions
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Gets all components of type <typeparamref name="T"/> in the direct children of the specified parent transform.
        /// </summary>
        /// <typeparam name="T">The type of component to find.</typeparam>
        /// <param name="parent">The parent transform to search within.</param>
        /// <returns>A list of components of type <typeparamref name="T"/> found in the direct children of the parent transform.</returns>
        public static List<T> GetComponentsInDirectChildren<T>(this Transform parent)
        {
            var results = new List<T>();

            foreach (Transform child in parent)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    results.Add(component);
                }
            }

            return results;
        }

        public static Vector3 GetCenterPoint(this Transform[] points)
        {
            if (points == null || points.Length == 0)
            {
                Debug.LogError("Not enough points to calculate center point");
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;

            foreach (Transform point in points)
            {
                sum += point.position;
            }

            return sum / points.Length;
        }

        public static Vector3 GetLocalCenterPoint(this Transform[] points)
        {
            if (points == null || points.Length == 0)
            {
                Debug.LogError("Not enough points to calculate center point");
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;

            foreach (Transform point in points)
            {
                sum += point.localPosition;
            }

            return sum / points.Length;
        }
    }
}