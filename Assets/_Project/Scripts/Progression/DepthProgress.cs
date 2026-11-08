using System;

namespace VoidProspector.Progression
{
    /// <summary>
    /// Depth levels within a sector. Depth 0 is shown as "Depth 1". Breaking enough meteors at the deepest
    /// unlocked depth unlocks the next; the player can move back to any unlocked depth to farm.
    /// </summary>
    public sealed class DepthProgress
    {
        public event Action Changed;

        public int MaxDepth { get; }          // number of depth levels in the sector
        public int MeteorsPerDepth { get; }
        public int Current { get; private set; }
        public int Deepest { get; private set; }
        /// <summary>Meteors broken at the deepest unlocked depth since it was unlocked.</summary>
        public int Progress { get; private set; }

        public DepthProgress(int maxDepth, int meteorsPerDepth)
        {
            MaxDepth = Math.Max(1, maxDepth);
            MeteorsPerDepth = Math.Max(1, meteorsPerDepth);
        }

        public bool IsDeepestUnlocked => Deepest >= MaxDepth - 1;
        public bool CanGoDeeper => Current < Deepest;
        public bool CanGoShallower => Current > 0;
        public float ProgressFraction => IsDeepestUnlocked ? 1f : (float)Progress / MeteorsPerDepth;

        public bool TrySetDepth(int depth)
        {
            if (depth < 0 || depth > Deepest || depth == Current) return false;
            Current = depth;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Counts a broken meteor from <paramref name="depth"/>. Returns true when it unlocked a new depth.</summary>
        public bool RegisterBreak(int depth)
        {
            if (depth != Deepest || IsDeepestUnlocked) return false;
            Progress++;
            if (Progress < MeteorsPerDepth)
            {
                Changed?.Invoke();
                return false;
            }
            Deepest++;
            Progress = 0;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Unlocks up to a depth without breaking meteors (debug tools and, later, loading a save).</summary>
        public void Unlock(int deepest, int current)
        {
            Deepest = Math.Max(0, Math.Min(deepest, MaxDepth - 1));
            Current = Math.Max(0, Math.Min(current, Deepest));
            Progress = 0;
            Changed?.Invoke();
        }
    }
}
