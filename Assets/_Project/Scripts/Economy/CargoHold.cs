using System;
using System.Collections.Generic;
using R3;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Mining;

namespace VoidProspector.Economy
{
    /// <summary>Chunks collected but not yet sold. Limited by capacity.</summary>
    public sealed class CargoHold
    {
        readonly Dictionary<(MineralDefinition mineral, int grade), int> _counts = new Dictionary<(MineralDefinition, int), int>();
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
            _counts.TryGetValue((mineral, grade), out int n) ? n : 0;

        public int CountOf(MineralDefinition mineral)
        {
            int n = 0;
            foreach (var kv in _counts)
                if (kv.Key.mineral == mineral) n += kv.Value;
            return n;
        }

        /// <summary>Adds a chunk; returns false when the hold is full.</summary>
        public bool TryAdd(ChunkData chunk)
        {
            if (IsFull) return false;
            var key = (chunk.mineral, chunk.grade);
            _counts[key] = CountOf(chunk.mineral, chunk.grade) + 1;
            // Value first, so a subscriber to Count already sees the new value.
            _value.Value = Value + chunk.value;
            _count.Value = Count + 1;
            return true;
        }

        /// <summary>Empties the hold and returns the value of what was in it.</summary>
        public GameNumber TakeAll()
        {
            var value = Value;
            if (Count == 0) return value;
            _counts.Clear();
            _value.Value = GameNumber.Zero;
            _count.Value = 0;
            return value;
        }
    }
}
