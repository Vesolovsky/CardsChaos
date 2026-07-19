using System;
using UnityEngine;

namespace Vesolovsky.Core.Utils.Extensions
{
    //TODO: add to the core
    public static class EnumExtensions
    {
        public static T GetRandomEnumValue<T>(bool excludeFirst = false) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));

            var startIndex = excludeFirst ? 1 : 0;
            int randomIndex = UnityEngine.Random.Range(startIndex, values.Length);
            return values[randomIndex];
        }
    }
}
