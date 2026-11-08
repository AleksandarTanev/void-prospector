using System;
using R3;
using VoidProspector.Data;
using VoidProspector.Economy;
using VoidProspector.Mining;
using VoidProspector.Progression;

namespace VoidProspector.Core
{
    public enum CollectResult { Stored, AutoSold, CargoFull }

    /// <summary>One hit's damage after a crit roll.</summary>
    public readonly struct HitRoll
    {
        public readonly GameNumber damage;
        public readonly bool crit;

        public HitRoll(GameNumber damage, bool crit)
        {
            this.damage = damage;
            this.crit = crit;
        }
    }

    /// <summary>
    /// The state of one play session: data, sector, depth, wallet, cargo and upgrades.
    /// Pure C#, so it can be tested and driven by the economy simulator.
    /// </summary>
    public sealed class GameSession
    {
        public GameDatabase Database { get; }
        public SectorDefinition Sector { get; }
        public Wallet Wallet { get; }
        public CargoHold Cargo { get; }
        public Upgrades Upgrades { get; }
        public DepthProgress Depths { get; }
        public Random Rng { get; }

        readonly ReactiveProperty<int> _autoSellBelow = new ReactiveProperty<int>(0);

        /// <summary>Chunks below this purity grade are sold the moment they're collected. 0 = off.</summary>
        public int AutoSellBelow => _autoSellBelow.Value;
        /// <summary>Auto-sell setting for UI binding; emits the current value on subscribe, then every change.</summary>
        public ReadOnlyReactiveProperty<int> AutoSellBelowRx => _autoSellBelow;

        public GameSession(GameDatabase database, SectorDefinition sector, int seed)
        {
            Database = database ? database : throw new ArgumentNullException(nameof(database));
            Sector = sector ? sector : throw new ArgumentNullException(nameof(sector));
            Rng = new Random(seed);
            Wallet = new Wallet();
            Upgrades = new Upgrades(database.upgrades, Wallet);
            Cargo = new CargoHold(CargoCapacity);
            Depths = new DepthProgress(sector.maxDepth, database.mining != null ? database.mining.meteorsPerDepth : 15);

            // The session owns both sides, so this subscription lives as long as the session.
            Upgrades.Get(UpgradeEffect.CargoCapacity)?.LevelRx.Subscribe(_ => Cargo.Capacity = CargoCapacity);
        }

        public MiningSettings Mining => Database.mining;
        public PurityTable Purity => Database.purity;
        public int Depth => Depths.Current;

        // ---------- Stats (from upgrade levels) ----------

        public GameNumber DrillDamage => Upgrades.Value(UpgradeEffect.DrillDamage, 1);
        /// <summary>Seconds between auto-fire shots; 0 = auto-fire not bought yet.</summary>
        public float AutoFireInterval => (float)Upgrades.Value(UpgradeEffect.AutoFireInterval, 0);
        public float CritChance => (float)Math.Min(1, Upgrades.Value(UpgradeEffect.CritChance, 0));
        public float CollectorRadius => (float)Upgrades.Value(UpgradeEffect.CollectorRadius, 4.5);
        public int CargoCapacity => (int)Math.Floor(Upgrades.Value(UpgradeEffect.CargoCapacity, 50));

        // ---------- Rolls ----------

        public MeteorSpawn RollMeteor() => MeteorRoller.RollMeteor(Database, Sector, Depth, Rng);

        public ChunkData RollChunk(MineralDefinition mineral, int depth, bool fromCore) =>
            MeteorRoller.RollChunk(mineral, Purity, Sector, depth, fromCore, Rng);

        /// <summary>Damage for a tap (with the tap bonus) or an auto-fire shot, including a crit roll.</summary>
        public HitRoll RollHit(bool fromTap)
        {
            var damage = DrillDamage;
            if (fromTap && Mining != null) damage *= Mining.tapDamageMultiplier;
            bool crit = CritChance > 0 && Rng.NextDouble() < CritChance;
            if (crit) damage *= Mining != null ? Mining.critMultiplier : 2;
            return new HitRoll(damage, crit);
        }

        // ---------- Actions ----------

        public CollectResult TryCollect(ChunkData chunk)
        {
            if (chunk.grade < AutoSellBelow)
            {
                Wallet.Add(chunk.value);
                return CollectResult.AutoSold;
            }
            return Cargo.TryAdd(chunk) ? CollectResult.Stored : CollectResult.CargoFull;
        }

        /// <summary>Sells the whole cargo hold; returns the credits earned.</summary>
        public GameNumber SellCargo()
        {
            var value = Cargo.TakeAll();
            Wallet.Add(value);
            return value;
        }

        public void SetAutoSellBelow(int grade)
        {
            grade = Math.Max(0, Math.Min(grade, Purity.Count));
            _autoSellBelow.Value = grade;
        }

        /// <summary>Counts a broken meteor toward unlocking the next depth.</summary>
        public bool RegisterBreak(int meteorDepth) => Depths.RegisterBreak(meteorDepth);
    }
}
