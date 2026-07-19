using System;

namespace Vesolovsky.Core.Utils.Extensions
{
    public static class RandomExtensions
    {
        private static readonly Random Rnd = new Random();

        public static long RandomLong(long min, long max)
        {
            if (min > max)
            {
                throw new ArgumentException($"min value: '{min}' cannot be greater than max value: '{max}'.");
            }

            // Calculate the range size
            ulong range = (ulong)(max - min);

            // Generate a random value in the range of 0 to the range size
            ulong randomValue = (ulong)(Rnd.NextDouble() * range);

            // Return the value shifted by the minimum value
            return (long)(min + (long)randomValue);
        }

        /// <summary>
        /// Return a random int within [minInclusive..maxInclusive]
        /// </summary>
        public static int RandomInclusive(int min, int max)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }
    }
}