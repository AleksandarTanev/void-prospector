using System;
using UnityEngine;

namespace VoidProspector.Core
{
    /// <summary>
    /// The number type for every in-game amount that can grow large (credits, values, costs, HP).
    /// Wraps a double today; the single place to swap in a big-number library later.
    /// </summary>
    [Serializable]
    public struct GameNumber : IEquatable<GameNumber>, IComparable<GameNumber>
    {
        [SerializeField] double value;

        public GameNumber(double value) => this.value = value;

        public double Value => value;

        public static readonly GameNumber Zero = new GameNumber(0);
        public static readonly GameNumber One = new GameNumber(1);

        public static implicit operator GameNumber(double v) => new GameNumber(v);
        public static implicit operator GameNumber(int v) => new GameNumber(v);
        public static explicit operator double(GameNumber n) => n.value;

        public static GameNumber operator +(GameNumber a, GameNumber b) => new GameNumber(a.value + b.value);
        public static GameNumber operator -(GameNumber a, GameNumber b) => new GameNumber(a.value - b.value);
        public static GameNumber operator *(GameNumber a, GameNumber b) => new GameNumber(a.value * b.value);
        public static GameNumber operator /(GameNumber a, GameNumber b) => new GameNumber(a.value / b.value);
        public static GameNumber operator -(GameNumber a) => new GameNumber(-a.value);

        public static bool operator <(GameNumber a, GameNumber b) => a.value < b.value;
        public static bool operator >(GameNumber a, GameNumber b) => a.value > b.value;
        public static bool operator <=(GameNumber a, GameNumber b) => a.value <= b.value;
        public static bool operator >=(GameNumber a, GameNumber b) => a.value >= b.value;
        public static bool operator ==(GameNumber a, GameNumber b) => a.value == b.value;
        public static bool operator !=(GameNumber a, GameNumber b) => a.value != b.value;

        public static GameNumber Pow(GameNumber b, double exponent) => new GameNumber(Math.Pow(b.value, exponent));
        public static GameNumber Floor(GameNumber n) => new GameNumber(Math.Floor(n.value));
        public static GameNumber Ceil(GameNumber n) => new GameNumber(Math.Ceiling(n.value));
        public static GameNumber Sqrt(GameNumber n) => new GameNumber(Math.Sqrt(n.value));
        public static GameNumber Max(GameNumber a, GameNumber b) => a.value >= b.value ? a : b;
        public static GameNumber Min(GameNumber a, GameNumber b) => a.value <= b.value ? a : b;

        public bool Equals(GameNumber other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is GameNumber other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public int CompareTo(GameNumber other) => value.CompareTo(other.value);

        public override string ToString() => NumberFormatter.Format(this);
    }
}
