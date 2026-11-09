using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using VoidProspector.Data;
using VoidProspector.Drones;
using VoidProspector.Economy;
using VoidProspector.Mining;
using VoidProspector.Progression;
using VoidProspector.Refining;

namespace VoidProspector.Core
{
    public enum CollectResult { Stored, AutoSold, CargoFull }

    /// <summary>Where the player stands with the jump drive to the next sector.</summary>
    public enum JumpState
    {
        /// <summary>There is no further sector in the data.</summary>
        NoNextSector,
        /// <summary>The last depth of the furthest sector isn't unlocked yet.</summary>
        NeedsDepth,
        /// <summary>Available, but the credits or minerals aren't there yet (the minerals are being kept in the hold).</summary>
        Saving,
        Affordable,
    }

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
    /// The state of one play session: data, sectors, depths, wallet, cargo, upgrades, drones, milestones, the
    /// Mineral Codex and the refinery.
    /// Pure C#, so it can be tested and driven by the economy simulator.
    /// </summary>
    public sealed class GameSession
    {
        public GameDatabase Database { get; }
        public Wallet Wallet { get; }
        public CargoHold Cargo { get; }
        public Upgrades Upgrades { get; }
        public Milestones Milestones { get; }
        public DroneBay Drones { get; } = new DroneBay();
        public Codex Codex { get; }
        public Refinery Refinery { get; }
        /// <summary>Lifetime play time and session count (analytics).</summary>
        public PlayStats Stats { get; } = new PlayStats();
        public Random Rng { get; }

        /// <summary>All sectors in travel order (by index).</summary>
        public IReadOnlyList<SectorDefinition> Sectors { get; }

        readonly ReactiveProperty<SectorDefinition> _sector;
        readonly ReactiveProperty<int> _unlockedSector;
        readonly ReactiveProperty<int> _autoSellBelow = new ReactiveProperty<int>(0);
        readonly Dictionary<SectorDefinition, DepthProgress> _depths = new Dictionary<SectorDefinition, DepthProgress>();
        readonly List<(MineralDefinition mineral, int count)> _reserve = new List<(MineralDefinition, int)>();

        /// <summary>The sector the ship is in. Changes only through <see cref="TravelTo"/>.</summary>
        public SectorDefinition Sector => _sector.Value;
        public ReadOnlyReactiveProperty<SectorDefinition> SectorRx => _sector;

        /// <summary>Index of the furthest unlocked sector; every sector up to it can be visited.</summary>
        public int UnlockedSector => _unlockedSector.Value;
        public ReadOnlyReactiveProperty<int> UnlockedSectorRx => _unlockedSector;

        /// <summary>Chunks below this purity grade are sold the moment they're collected. 0 = off.</summary>
        public int AutoSellBelow => _autoSellBelow.Value;
        /// <summary>Auto-sell setting for UI binding; emits the current value on subscribe, then every change.</summary>
        public ReadOnlyReactiveProperty<int> AutoSellBelowRx => _autoSellBelow;

        public GameSession(GameDatabase database, SectorDefinition sector, int seed)
        {
            Database = database ? database : throw new ArgumentNullException(nameof(database));
            if (!sector) throw new ArgumentNullException(nameof(sector));
            Sectors = database.sectors.Where(s => s != null).OrderBy(s => s.index).ToArray();
            _sector = new ReactiveProperty<SectorDefinition>(sector);
            _unlockedSector = new ReactiveProperty<int>(sector.index);
            Rng = new Random(seed);
            Wallet = new Wallet();
            Upgrades = new Upgrades(database.upgrades, Wallet);
            Cargo = new CargoHold(CargoCapacity);
            Milestones = new Milestones(database.milestones, s => DepthsOf(s).Deepest + 1);
            Codex = new Codex(database.codex);
            Refinery = new Refinery(database.refinery ?? new RefinerySettings(), database.purity, Rng);

            // The session owns all sides, so these subscriptions live as long as the session.
            Upgrades.Get(UpgradeEffect.CargoCapacity)?.LevelRx.Subscribe(_ => Cargo.Capacity = CargoCapacity);
            Milestones.Completed.Subscribe(m => { if (m.rewardCredits > 0) Wallet.Add(m.rewardCredits); });
            Refinery.Stepped.Subscribe(step => { if (step.success) Codex.Record(Refinery.Mineral, step.grade); });
        }

        public MiningSettings Mining => Database.mining;
        public PurityTable Purity => Database.purity;
        /// <summary>Depth progress of the current sector.</summary>
        public DepthProgress Depths => DepthsOf(Sector);
        public int Depth => Depths.Current;

        /// <summary>Depth progress of any sector; each sector keeps its own.</summary>
        public DepthProgress DepthsOf(SectorDefinition sector)
        {
            if (!_depths.TryGetValue(sector, out var d))
            {
                d = new DepthProgress(sector.maxDepth, Mining != null ? Mining.meteorsPerDepth : 15);
                _depths[sector] = d;
            }
            return d;
        }

        /// <summary>Sectors whose depth progress exists (visited or restored), for saving.</summary>
        public IEnumerable<KeyValuePair<SectorDefinition, DepthProgress>> SectorDepths => _depths;

        // ---------- Stats (from upgrade levels) ----------

        public GameNumber DrillDamage => Upgrades.Value(UpgradeEffect.DrillDamage, 1);
        /// <summary>Seconds between auto-fire shots; 0 = auto-fire not bought yet.</summary>
        public float AutoFireInterval => (float)Upgrades.Value(UpgradeEffect.AutoFireInterval, 0);
        public float CritChance => (float)Math.Min(1, Upgrades.Value(UpgradeEffect.CritChance, 0));
        public float CollectorRadius => (float)Upgrades.Value(UpgradeEffect.CollectorRadius, 4.5);
        public int CargoCapacity => (int)Math.Floor(Upgrades.Value(UpgradeEffect.CargoCapacity, 50));

        public int DroneCount => Math.Max(0, (int)Math.Floor(Upgrades.Value(UpgradeEffect.DroneCount, 0)));
        /// <summary>A drone shot deals drill damage × this.</summary>
        public double DroneDamageFactor => Upgrades.Value(UpgradeEffect.DroneDamage, 1);
        public double DroneInterval => Database.drones != null ? Database.drones.fireInterval : 0.8;
        public TargetRule DroneTargetRule => DroneTargeting.RuleForLevel(Upgrades.Value(UpgradeEffect.DroneTargeting, 0));

        /// <summary>Scanner range in world units around the ship; 0 = no scanner yet.</summary>
        public float ScannerRange => (float)Upgrades.Value(UpgradeEffect.ScannerRange, 0);

        public RefineryStats RefineryStats => new RefineryStats(
            Upgrades.Value(UpgradeEffect.RefinerySpeed, 1),
            Upgrades.Value(UpgradeEffect.RefineSuccess, 0),
            Upgrades.Value(UpgradeEffect.RefineryStabiliser, 0));

        public int RefineryBatchSize => Math.Max(1, (int)Math.Floor(Upgrades.Value(UpgradeEffect.RefineryBatch, 10)));

        /// <summary>Something mines without input (auto-fire or drones), so idle and offline income can be above 0.</summary>
        public bool HasIdleMining => AutoFireInterval > 0 || DroneCount > 0;

        // ---------- Upgrades ----------

        /// <summary>Upgrades tied to a later sector (drones) appear once that sector is unlocked.</summary>
        public bool IsAvailable(UpgradeTrack track) => track != null && track.Definition.requiresSector <= UnlockedSector;

        /// <summary>Buys an available upgrade; returns the levels bought.</summary>
        public int TryBuy(UpgradeTrack track, BuyMode mode) => IsAvailable(track) ? Upgrades.TryBuy(track, mode) : 0;

        // ---------- Rolls ----------

        public MeteorSpawn RollMeteor() => MeteorRoller.RollMeteor(Database, Sector, Depth, Rng);

        public ChunkData RollChunk(MineralDefinition mineral, int depth, bool fromCore) =>
            MeteorRoller.RollChunk(mineral, Purity, Sector, depth, fromCore, Rng, ValueMultiplier(mineral));

        /// <summary>Permanent value bonuses of a mineral: milestones × Codex entries.</summary>
        public double ValueMultiplier(MineralDefinition mineral) => Milestones.ValueMultiplier(mineral) * Codex.ValueMultiplier(mineral);

        /// <summary>Damage for a tap (with the tap bonus) or an auto-fire shot, including a crit roll.</summary>
        public HitRoll RollHit(bool fromTap)
        {
            var damage = DrillDamage;
            if (fromTap && Mining != null) damage *= Mining.tapDamageMultiplier;
            return Crit(damage);
        }

        /// <summary>Damage for one drone shot, including a crit roll.</summary>
        public HitRoll RollDroneHit() => Crit(DrillDamage * DroneDamageFactor);

        HitRoll Crit(GameNumber damage)
        {
            bool crit = CritChance > 0 && Rng.NextDouble() < CritChance;
            if (crit) damage *= Mining != null ? Mining.critMultiplier : 2;
            return new HitRoll(damage, crit);
        }

        /// <summary>Matches the drone bay to the upgrade and advances its timers; lists the drones ready to fire.</summary>
        public void TickDrones(double dt, List<int> ready)
        {
            Drones.Sync(DroneCount, DroneInterval);
            Drones.Tick(dt, ready);
        }

        // ---------- Actions ----------

        public CollectResult TryCollect(ChunkData chunk)
        {
            if (chunk.grade < AutoSellBelow && !NeedsForJump(chunk.mineral))
            {
                Wallet.Add(chunk.value);
                Found(chunk);
                return CollectResult.AutoSold;
            }
            if (!Cargo.TryAdd(chunk)) return CollectResult.CargoFull;
            Found(chunk);
            return CollectResult.Stored;
        }

        void Found(ChunkData chunk)
        {
            Milestones.RecordMined(chunk.mineral);
            Codex.Record(chunk.mineral, chunk.grade);
        }

        // ---------- Refinery ----------

        public bool RefineryUnlocked => UnlockedSector >= (Database.refinery != null ? Database.refinery.unlockSector : 3);

        /// <summary>Moves up to a batch of one cargo stack into the empty refinery.</summary>
        public bool TryLoadRefinery(MineralDefinition mineral, int grade)
        {
            if (!RefineryUnlocked || !Refinery.IsEmpty || grade >= Purity.TopGrade) return false;
            var (count, value) = Cargo.TakeFromStack(mineral, grade, RefineryBatchSize);
            if (count == 0) return false;
            Refinery.Load(mineral, grade, count, value);
            return true;
        }

        public void TickRefinery(double dt)
        {
            if (Refinery.IsRunning) Refinery.Tick(dt, RefineryStats);
        }

        /// <summary>Puts the batch back into the cargo hold (all of it, or nothing if there isn't room).</summary>
        public bool CollectRefinery()
        {
            if (Refinery.IsEmpty || Cargo.FreeSpace < Refinery.Count) return false;
            var (mineral, grade, count, value) = Refinery.Take();
            return Cargo.TryAddStack(mineral, grade, count, value);
        }

        /// <summary>Sells the batch straight from the refinery.</summary>
        public GameNumber SellRefinery()
        {
            if (Refinery.IsEmpty) return GameNumber.Zero;
            var value = Refinery.Take().value;
            Wallet.Add(value);
            return value;
        }

        /// <summary>Sells the cargo hold, except the minerals kept for the jump drive; returns the credits earned.</summary>
        public GameNumber SellCargo()
        {
            var value = Cargo.TakeAllExcept(JumpReserve);
            Wallet.Add(value);
            return value;
        }

        /// <summary>What <see cref="SellCargo"/> would pay right now.</summary>
        public GameNumber SellableValue
        {
            get
            {
                var kept = GameNumber.Zero;
                foreach (var (mineral, count) in JumpReserve) kept += Cargo.ValueOfCheapest(mineral, count);
                return GameNumber.Max(Cargo.Value - kept, GameNumber.Zero);
            }
        }

        public void SetAutoSellBelow(int grade)
        {
            grade = Math.Max(0, Math.Min(grade, Purity.Count));
            _autoSellBelow.Value = grade;
        }

        /// <summary>Counts a broken meteor toward unlocking the next depth and toward milestones.</summary>
        public bool RegisterBreak(int meteorDepth)
        {
            bool unlocked = Depths.RegisterBreak(meteorDepth);
            Milestones.RecordBreak(Sector);
            if (unlocked) Milestones.CheckDepths();
            return unlocked;
        }

        // ---------- Sectors and the jump drive ----------

        public SectorDefinition FurthestSector => Sectors.LastOrDefault(s => s.index <= UnlockedSector) ?? Sector;
        public SectorDefinition NextSector => Sectors.FirstOrDefault(s => s.index > UnlockedSector);
        public bool IsUnlocked(SectorDefinition sector) => sector != null && sector.index <= UnlockedSector;

        public JumpState JumpState
        {
            get
            {
                var next = NextSector;
                if (next == null) return JumpState.NoNextSector;
                if (!DepthsOf(FurthestSector).IsDeepestUnlocked) return JumpState.NeedsDepth;
                if (!Wallet.CanAfford(next.UnlockCredits)) return JumpState.Saving;
                foreach (var c in next.unlockCost)
                    if (c != null && c.mineral != null && Cargo.CountOf(c.mineral) < (int)c.amount) return JumpState.Saving;
                return JumpState.Affordable;
            }
        }

        /// <summary>
        /// Minerals the jump drive needs, while it's available but not bought. Selling and auto-sell keep these
        /// in the hold (the cheapest chunks), so the player can pay with them.
        /// </summary>
        public IReadOnlyList<(MineralDefinition mineral, int count)> JumpReserve
        {
            get
            {
                _reserve.Clear();
                var next = NextSector;
                if (next == null || !DepthsOf(FurthestSector).IsDeepestUnlocked) return _reserve;
                foreach (var c in next.unlockCost)
                    if (c != null && c.mineral != null) _reserve.Add((c.mineral, (int)c.amount));
                return _reserve;
            }
        }

        bool NeedsForJump(MineralDefinition mineral)
        {
            foreach (var (m, count) in JumpReserve)
                if (m == mineral) return Cargo.CountOf(m) < count;
            return false;
        }

        /// <summary>Pays the jump drive (credits from the wallet, minerals from the hold) and unlocks the next sector.</summary>
        public bool TryBuyJumpDrive()
        {
            if (JumpState != JumpState.Affordable) return false;
            var next = NextSector;
            if (!Wallet.TrySpend(next.UnlockCredits)) return false;
            foreach (var c in next.unlockCost)
                if (c != null && c.mineral != null) Cargo.Remove(c.mineral, (int)c.amount);
            _unlockedSector.Value = next.index;
            return true;
        }

        /// <summary>Moves the ship to an unlocked sector. Its depths are kept per sector.</summary>
        public bool TravelTo(SectorDefinition sector)
        {
            if (!IsUnlocked(sector) || sector == Sector || !Sectors.Contains(sector)) return false;
            _sector.Value = sector;
            return true;
        }

        /// <summary>Restores the furthest unlocked sector from a save (never below the current one).</summary>
        public void RestoreUnlockedSector(int index)
        {
            int max = Sectors.Count > 0 ? Sectors[Sectors.Count - 1].index : Sector.index;
            _unlockedSector.Value = Math.Max(Sector.index, Math.Min(index, max));
        }

        /// <summary>Unlocks the next sector without paying (debug tools).</summary>
        public void DebugUnlockNextSector()
        {
            var next = NextSector;
            if (next != null) _unlockedSector.Value = next.index;
        }
    }
}
