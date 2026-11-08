using System;
using VoidProspector.Data;
using VoidProspector.Economy;
using VoidProspector.Mining;

namespace VoidProspector.Core
{
    /// <summary>
    /// The state of one play session: data, current sector, wallet and cargo. Pure C#, so it can be tested.
    /// Phase 3 uses every upgrade at level 0; Phase 4 adds levels.
    /// </summary>
    public sealed class GameSession
    {
        public GameDatabase Database { get; }
        public SectorDefinition Sector { get; private set; }
        public int Depth { get; private set; }
        public Wallet Wallet { get; }
        public CargoHold Cargo { get; }
        public Random Rng { get; }

        public GameSession(GameDatabase database, SectorDefinition sector, int seed)
        {
            Database = database ? database : throw new ArgumentNullException(nameof(database));
            Sector = sector ? sector : throw new ArgumentNullException(nameof(sector));
            Rng = new Random(seed);
            Wallet = new Wallet();
            Cargo = new CargoHold(CargoCapacity);
        }

        public MiningSettings Mining => Database.mining;
        public PurityTable Purity => Database.purity;

        // ---------- Stats (from upgrade data) ----------

        public GameNumber DrillDamage => Effect(UpgradeEffect.DrillDamage, 1);
        public float CollectorRadius => (float)Effect(UpgradeEffect.CollectorRadius, 4.5);
        public int CargoCapacity => (int)Math.Floor(Effect(UpgradeEffect.CargoCapacity, 50));

        double Effect(UpgradeEffect effect, double fallback)
        {
            var upgrade = Database.GetUpgrade(effect);
            return upgrade != null ? upgrade.EffectAt(0) : fallback;
        }

        // ---------- Rolls ----------

        public MeteorSpawn RollMeteor() => MeteorRoller.RollMeteor(Database, Sector, Depth, Rng);

        public ChunkData RollChunk(MineralDefinition mineral, bool fromCore) =>
            MeteorRoller.RollChunk(mineral, Purity, Sector, Depth, fromCore, Rng);

        // ---------- Actions ----------

        public bool TryCollect(ChunkData chunk) => Cargo.TryAdd(chunk);

        /// <summary>Sells the whole cargo hold; returns the credits earned.</summary>
        public GameNumber SellCargo()
        {
            var value = Cargo.TakeAll();
            Wallet.Add(value);
            return value;
        }
    }
}
