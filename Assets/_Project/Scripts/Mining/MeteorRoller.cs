using System;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Mining
{
    /// <summary>A meteor's contents, rolled when it spawns.</summary>
    public sealed class MeteorSpawn
    {
        public MeteorSizeDefinition size;
        public MeteorTypeDefinition type;
        public int depth;
        public GameNumber hp;
        /// <summary>One mineral per chunk, in spill order.</summary>
        public MineralDefinition[] minerals;
    }

    /// <summary>Rolls meteors and chunks from the data. Deterministic for a given System.Random.</summary>
    public static class MeteorRoller
    {
        public static MeteorSpawn RollMeteor(GameDatabase db, SectorDefinition sector, int depth, Random rng)
        {
            var size = db.RollMeteorSize(rng) ?? throw new InvalidOperationException("No meteor size could be rolled");
            var type = sector.RollMeteorType(rng) ?? throw new InvalidOperationException($"Sector '{sector.id}' has no meteor type to roll");

            var minerals = new MineralDefinition[size.RollChunks(rng)];
            for (int i = 0; i < minerals.Length; i++) minerals[i] = type.RollMineral(rng);

            return new MeteorSpawn
            {
                size = size,
                type = type,
                depth = depth,
                hp = sector.MeteorHp(size, type, depth),
                minerals = minerals,
            };
        }

        /// <summary>Rolls a purity grade for a mineral and works out its value. <paramref name="valueMultiplier"/> carries permanent bonuses (milestones).</summary>
        public static ChunkData RollChunk(MineralDefinition mineral, PurityTable purity, SectorDefinition sector,
            int depth, bool fromCore, Random rng, double valueMultiplier = 1)
        {
            int grade = purity.Roll(rng, fromCore ? 1 : 0);
            var value = Formulas.ChunkValue(mineral.BaseValue, purity[grade].valueMultiplier, depth, sector.depthValueStep, valueMultiplier);
            return new ChunkData(mineral, grade, value);
        }
    }
}
