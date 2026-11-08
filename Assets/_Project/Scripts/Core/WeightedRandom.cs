using System;
using System.Collections.Generic;

namespace VoidProspector.Core
{
    /// <summary>Picks items by weight. Items with a weight of zero or less are never picked.</summary>
    public static class WeightedRandom
    {
        /// <summary>Index of the picked item, or -1 if the list is empty or no item has a positive weight.</summary>
        public static int PickIndex<T>(IReadOnlyList<T> items, Func<T, float> weight, Random rng)
        {
            if (items == null || items.Count == 0) return -1;

            double total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                float w = weight(items[i]);
                if (w > 0) total += w;
            }
            if (total <= 0) return -1;

            double r = rng.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < items.Count; i++)
            {
                float w = weight(items[i]);
                if (w <= 0) continue;
                last = i;
                r -= w;
                if (r < 0) return i;
            }
            return last;   // floating-point leftovers land on the last valid item
        }

        /// <summary>The picked item, or default if nothing can be picked.</summary>
        public static T Pick<T>(IReadOnlyList<T> items, Func<T, float> weight, Random rng)
        {
            int i = PickIndex(items, weight, rng);
            return i < 0 ? default : items[i];
        }
    }
}
