using System;
using System.Collections.Generic;
using R3;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Mining;

namespace VoidProspector.Economy
{
    /// <summary>Chunks collected but not yet sold. Limited by capacity. Value is tracked per (mineral, grade) stack.</summary>
    public sealed class CargoHold
    {
        sealed class Stack
        {
            public int count;
            public GameNumber value;
        }

        readonly Dictionary<(MineralDefinition mineral, int grade), Stack> _stacks = new Dictionary<(MineralDefinition, int), Stack>();
        readonly ReactiveProperty<int> _capacity;
        readonly ReactiveProperty<int> _count = new ReactiveProperty<int>(0);
        readonly ReactiveProperty<GameNumber> _value = new ReactiveProperty<GameNumber>(GameNumber.Zero);

        public CargoHold(int capacity) => _capacity = new ReactiveProperty<int>(Math.Max(0, capacity));

        public int Capacity
        {
            get => _capacity.Value;
            set => _capacity.Value = Math.Max(0, value);
        }

        public int Count => _count.Value;
        public GameNumber Value => _value.Value;
        public bool IsFull => Count >= Capacity;
        public bool IsEmpty => Count == 0;
        public float Fill => Capacity > 0 ? Math.Min(1f, (float)Count / Capacity) : 1f;

        // UI bindings: each emits the current value on subscribe, then every change.
        public ReadOnlyReactiveProperty<int> CapacityRx => _capacity;
        public ReadOnlyReactiveProperty<int> CountRx => _count;
        public ReadOnlyReactiveProperty<GameNumber> ValueRx => _value;

        public int CountOf(MineralDefinition mineral, int grade) =>
            _stacks.TryGetValue((mineral, grade), out var s) ? s.count : 0;

        public int CountOf(MineralDefinition mineral)
        {
            int n = 0;
            foreach (var kv in _stacks)
                if (kv.Key.mineral == mineral) n += kv.Value.count;
            return n;
        }

        /// <summary>Adds a chunk; returns false when the hold is full.</summary>
        public bool TryAdd(ChunkData chunk)
        {
            if (IsFull) return false;
            var key = (chunk.mineral, chunk.grade);
            if (!_stacks.TryGetValue(key, out var stack)) _stacks[key] = stack = new Stack();
            stack.count++;
            stack.value += chunk.value;
            // Value first, so a subscriber to Count already sees the new value.
            _value.Value = Value + chunk.value;
            _count.Value = Count + 1;
            return true;
        }

        /// <summary>Every (mineral, grade) stack in the hold with its total value, for saving.</summary>
        public IEnumerable<(MineralDefinition mineral, int grade, int count, GameNumber value)> Stacks
        {
            get
            {
                foreach (var kv in _stacks) yield return (kv.Key.mineral, kv.Key.grade, kv.Value.count, kv.Value.value);
            }
        }

        /// <summary>
        /// Replaces the contents when loading a save. Chunk values depend on the depth they came from, so values are
        /// restored as saved. If the stacks carry no values (older saves) the total is shared out per chunk.
        /// May exceed capacity if the data changed.
        /// </summary>
        public void Restore(IEnumerable<(MineralDefinition mineral, int grade, int count, GameNumber value)> stacks, GameNumber total)
        {
            _stacks.Clear();
            int count = 0;
            var stackSum = GameNumber.Zero;
            foreach (var (mineral, grade, n, value) in stacks)
            {
                if (mineral == null || n <= 0) continue;
                var key = (mineral, grade);
                if (!_stacks.TryGetValue(key, out var stack)) _stacks[key] = stack = new Stack();
                stack.count += n;
                stack.value += GameNumber.Max(value, GameNumber.Zero);
                count += n;
                stackSum += GameNumber.Max(value, GameNumber.Zero);
            }

            if (total < GameNumber.Zero || count == 0) total = GameNumber.Zero;
            if (stackSum <= GameNumber.Zero && total > GameNumber.Zero)
                foreach (var s in _stacks.Values) s.value = total * s.count / count;

            _value.Value = total;
            _count.Value = count;
        }

        /// <summary>Empties the hold and returns the value of what was in it.</summary>
        public GameNumber TakeAll()
        {
            var value = Value;
            if (Count == 0) return value;
            _stacks.Clear();
            _value.Value = GameNumber.Zero;
            _count.Value = 0;
            return value;
        }

        /// <summary>
        /// Empties the hold except for the cheapest (lowest-grade) chunks listed in <paramref name="keep"/>, and
        /// returns the value of what was taken.
        /// </summary>
        public GameNumber TakeAllExcept(IReadOnlyList<(MineralDefinition mineral, int count)> keep)
        {
            if (keep == null || keep.Count == 0) return TakeAll();
            var kept = new List<(MineralDefinition, int, int, GameNumber)>();
            int keptCount = 0;
            var keptValue = GameNumber.Zero;
            foreach (var (mineral, want) in keep)
            {
                int left = want;
                foreach (var (grade, stack) in CheapestFirst(mineral))
                {
                    if (left <= 0) break;
                    int n = Math.Min(left, stack.count);
                    var v = ValueOf(stack, n);
                    kept.Add((mineral, grade, n, v));
                    keptCount += n;
                    keptValue += v;
                    left -= n;
                }
            }
            if (keptCount == 0) return TakeAll();

            var taken = GameNumber.Max(Value - keptValue, GameNumber.Zero);
            _stacks.Clear();
            foreach (var (mineral, grade, n, v) in kept) _stacks[(mineral, grade)] = new Stack { count = n, value = v };
            _value.Value = keptValue;
            _count.Value = keptCount;
            return taken;
        }

        /// <summary>Value of the <paramref name="count"/> cheapest chunks of a mineral (fewer if the hold has fewer).</summary>
        public GameNumber ValueOfCheapest(MineralDefinition mineral, int count)
        {
            var total = GameNumber.Zero;
            foreach (var (_, stack) in CheapestFirst(mineral))
            {
                if (count <= 0) break;
                int n = Math.Min(count, stack.count);
                total += ValueOf(stack, n);
                count -= n;
            }
            return total;
        }

        /// <summary>Removes up to <paramref name="count"/> chunks of a mineral, lowest grade first. Returns how many were removed.</summary>
        public int Remove(MineralDefinition mineral, int count)
        {
            int removed = 0;
            var removedValue = GameNumber.Zero;
            foreach (var (grade, stack) in CheapestFirst(mineral))
            {
                if (removed >= count) break;
                int n = Math.Min(count - removed, stack.count);
                var v = ValueOf(stack, n);
                stack.count -= n;
                stack.value = stack.count > 0 ? GameNumber.Max(stack.value - v, GameNumber.Zero) : GameNumber.Zero;
                if (stack.count == 0) _stacks.Remove((mineral, grade));
                removed += n;
                removedValue += v;
            }
            if (removed == 0) return 0;
            _value.Value = Count - removed > 0 ? GameNumber.Max(Value - removedValue, GameNumber.Zero) : GameNumber.Zero;
            _count.Value = Count - removed;
            return removed;
        }

        static GameNumber ValueOf(Stack stack, int n) => n >= stack.count ? stack.value : stack.value * n / stack.count;

        List<(int grade, Stack stack)> CheapestFirst(MineralDefinition mineral)
        {
            var list = new List<(int, Stack)>();
            foreach (var kv in _stacks)
                if (kv.Key.mineral == mineral && kv.Value.count > 0) list.Add((kv.Key.grade, kv.Value));
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return list;
        }
    }
}
