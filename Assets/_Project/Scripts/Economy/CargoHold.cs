using System;
using System.Collections.Generic;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Mining;

namespace VoidProspector.Economy
{
    /// <summary>Chunks collected but not yet sold. Limited by capacity.</summary>
    public sealed class CargoHold
    {
        public event Action Changed;

        readonly Dictionary<(MineralDefinition mineral, int grade), int> _counts = new Dictionary<(MineralDefinition, int), int>();
        int _capacity;

        public CargoHold(int capacity) => _capacity = Math.Max(0, capacity);

        public int Capacity
        {
            get => _capacity;
            set
            {
                value = Math.Max(0, value);
                if (value == _capacity) return;
                _capacity = value;
                Changed?.Invoke();
            }
        }

        public int Count { get; private set; }
        public GameNumber Value { get; private set; }
        public bool IsFull => Count >= _capacity;
        public bool IsEmpty => Count == 0;
        public float Fill => _capacity > 0 ? Math.Min(1f, (float)Count / _capacity) : 1f;

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
            Count++;
            Value += chunk.value;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Empties the hold and returns the value of what was in it.</summary>
        public GameNumber TakeAll()
        {
            var value = Value;
            if (Count == 0) return value;
            _counts.Clear();
            Count = 0;
            Value = GameNumber.Zero;
            Changed?.Invoke();
            return value;
        }
    }
}
