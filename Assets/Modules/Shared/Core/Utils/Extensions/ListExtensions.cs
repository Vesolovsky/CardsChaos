using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace Vesolovsky.Core.Utils.Extensions
{
    public static class ListExtensions
    {
        public static T GetRandomElement<T>(this List<T> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new System.InvalidOperationException("Cannot select a random element from an empty or null list.");
            }

            int index = Random.Range(0, list.Count);
            return list[index];
        }

        /// <summary>
        /// Gets you n random unique elements
        /// </summary>
        public static List<T> GetRandomUniqueElements<T>(this List<T> list, int count)
        {
            if (list == null || list.Count == 0)
            {
                throw new InvalidOperationException("Cannot select random elements from an empty or null list.");
            }

            if (count < 0 || count > list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count), $"Count({count}) cannot be greater than the number of elements({list.Count}) in the list or less than zero.");
            }

            return list.OrderBy(_ => Random.value).Take(count).ToList();
        }
        
        public static void Shuffle<T>(this IList<T> list)
        {
            var count = list.Count;
            var last = count - 1;
            for (var i = 0; i < last; ++i) {
                var r = Random.Range(i, count);
                var tmp = list[i];
                list[i] = list[r];
                list[r] = tmp;
            }
        }

        public static void SortBy<T, T2>(this List<T> list, System.Func<T, T2> keySelector) where T2 : System.IComparable => list.Sort((q, w) => keySelector(q).CompareTo(keySelector(w)));
    }
}