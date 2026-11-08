using System;
using System.Collections.Generic;
using VoidProspector.Core;

namespace VoidProspector.Mining
{
    /// <summary>What one hit did to a meteor.</summary>
    public struct HitResult
    {
        /// <summary>Crack stages passed by this hit (0, 1 or more).</summary>
        public int cracks;
        /// <summary>Chunks spilled by those cracks.</summary>
        public int crackChunks;
        public bool broke;
        /// <summary>Chunks left inside when it broke; they all spill now.</summary>
        public int breakChunks;

        public int TotalChunks => crackChunks + breakChunks;
    }

    /// <summary>
    /// A meteor's HP, crack stages and remaining chunks. Pure logic, no Unity objects.
    /// Each crack threshold spills a fraction of the chunks; breaking spills the rest.
    /// </summary>
    public sealed class MeteorState
    {
        readonly IReadOnlyList<float> _thresholds;
        readonly int _chunksPerCrack;

        public GameNumber MaxHp { get; }
        public GameNumber Hp { get; private set; }
        public int TotalChunks { get; }
        public int RemainingChunks { get; private set; }
        public int Stage { get; private set; }
        public bool IsBroken { get; private set; }

        public float HpFraction => MaxHp > GameNumber.Zero ? (float)((double)Hp / (double)MaxHp) : 0f;

        public MeteorState(GameNumber maxHp, int chunks, IReadOnlyList<float> crackThresholds, float crackSpillFraction)
        {
            if (maxHp <= GameNumber.Zero) throw new ArgumentOutOfRangeException(nameof(maxHp));
            MaxHp = Hp = maxHp;
            TotalChunks = RemainingChunks = Math.Max(0, chunks);
            _thresholds = crackThresholds ?? Array.Empty<float>();
            _chunksPerCrack = (int)Math.Floor(TotalChunks * Math.Max(0f, crackSpillFraction));
        }

        public HitResult Hit(GameNumber damage)
        {
            var result = new HitResult();
            if (IsBroken || damage <= GameNumber.Zero) return result;

            Hp = GameNumber.Max(Hp - damage, GameNumber.Zero);

            while (Stage < _thresholds.Count && HpFraction <= _thresholds[Stage])
            {
                Stage++;
                result.cracks++;
                int n = Math.Min(_chunksPerCrack, RemainingChunks);
                result.crackChunks += n;
                RemainingChunks -= n;
            }

            if (Hp <= GameNumber.Zero)
            {
                IsBroken = true;
                result.broke = true;
                result.breakChunks = RemainingChunks;
                RemainingChunks = 0;
            }
            return result;
        }
    }
}
