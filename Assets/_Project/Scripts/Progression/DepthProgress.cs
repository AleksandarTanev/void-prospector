using System;
using R3;

namespace VoidProspector.Progression
{
    /// <summary>
    /// Depth levels within a sector. Depth 0 is shown as "Depth 1". Breaking enough meteors at the deepest
    /// unlocked depth unlocks the next; the player can move back to any unlocked depth to farm.
    /// </summary>
    public sealed class DepthProgress
    {
        readonly ReactiveProperty<int> _current = new ReactiveProperty<int>(0);
        readonly ReactiveProperty<int> _deepest = new ReactiveProperty<int>(0);
        readonly ReactiveProperty<int> _progress = new ReactiveProperty<int>(0);

        public int MaxDepth { get; }          // number of depth levels in the sector
        public int MeteorsPerDepth { get; }
        public int Current => _current.Value;
        public int Deepest => _deepest.Value;
        /// <summary>Meteors broken at the deepest unlocked depth since it was unlocked.</summary>
        public int Progress => _progress.Value;

        // UI bindings: each emits the current value on subscribe, then every change.
        public ReadOnlyReactiveProperty<int> CurrentRx => _current;
        public ReadOnlyReactiveProperty<int> DeepestRx => _deepest;
        public ReadOnlyReactiveProperty<int> ProgressRx => _progress;

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
            _current.Value = depth;
            return true;
        }

        /// <summary>Counts a broken meteor from <paramref name="depth"/>. Returns true when it unlocked a new depth.</summary>
        public bool RegisterBreak(int depth)
        {
            if (depth != Deepest || IsDeepestUnlocked) return false;
            if (Progress + 1 < MeteorsPerDepth)
            {
                _progress.Value = Progress + 1;
                return false;
            }
            _progress.Value = 0;
            _deepest.Value = Deepest + 1;
            return true;
        }

        /// <summary>Restores the exact state when loading a save; values are clamped to this sector's depths.</summary>
        public void Restore(int deepest, int current, int progress)
        {
            _deepest.Value = Math.Max(0, Math.Min(deepest, MaxDepth - 1));
            _progress.Value = IsDeepestUnlocked ? 0 : Math.Max(0, Math.Min(progress, MeteorsPerDepth - 1));
            _current.Value = Math.Max(0, Math.Min(current, Deepest));
        }

        /// <summary>Unlocks up to a depth without breaking meteors (debug tools).</summary>
        public void Unlock(int deepest, int current)
        {
            _progress.Value = 0;
            _deepest.Value = Math.Max(0, Math.Min(deepest, MaxDepth - 1));
            _current.Value = Math.Max(0, Math.Min(current, Deepest));
        }
    }
}
