using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Mining
{
    /// <summary>What a chunk is: its mineral, purity grade and sell value (fixed when it spills).</summary>
    public readonly struct ChunkData
    {
        public readonly MineralDefinition mineral;
        public readonly int grade;
        public readonly GameNumber value;

        public ChunkData(MineralDefinition mineral, int grade, GameNumber value)
        {
            this.mineral = mineral;
            this.grade = grade;
            this.value = value;
        }
    }
}
