using System;
using System.Collections.Generic;

namespace VoidProspector.Core
{
    /// <summary>
    /// Everything that persists between runs, as plain serialisable fields (JsonUtility). Content is referenced
    /// by id so data assets can be renamed or reordered. Bump <see cref="SaveSystem.CurrentVersion"/> and add a
    /// migration step whenever a field's meaning changes.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version;
        /// <summary>When the save was written, as UTC <see cref="DateTime.Ticks"/>.</summary>
        public long savedAtUtcTicks;

        public double credits;
        /// <summary>The sector the ship is in.</summary>
        public string sectorId;
        /// <summary>Index of the furthest unlocked sector (0 in version 1 saves = only the current one).</summary>
        public int unlockedSector;
        public int autoSellBelow;

        /// <summary>Depth progress per sector (version 2+).</summary>
        public List<SectorSave> sectors = new List<SectorSave>();
        /// <summary>Version 1 only: depth of the current sector. Moved into <see cref="sectors"/> by the migration.</summary>
        public DepthSave depth = new DepthSave();

        public List<UpgradeSave> upgrades = new List<UpgradeSave>();
        public CargoSave cargo = new CargoSave();
        public MilestoneSave milestones = new MilestoneSave();

        public DateTime SavedAtUtc => new DateTime(Math.Max(0, Math.Min(savedAtUtcTicks, DateTime.MaxValue.Ticks)), DateTimeKind.Utc);
    }

    [Serializable]
    public class DepthSave
    {
        public int current;
        public int deepest;
        public int progress;
    }

    [Serializable]
    public class SectorSave
    {
        public string id;
        public int current;
        public int deepest;
        public int progress;
    }

    [Serializable]
    public class UpgradeSave
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class CargoSave
    {
        public double value;
        public List<CargoStackSave> stacks = new List<CargoStackSave>();
    }

    [Serializable]
    public class CargoStackSave
    {
        public string mineralId;
        public int grade;
        public int count;
        /// <summary>Total value of the stack (0 in version 1 saves; the cargo total is then shared out).</summary>
        public double value;
    }

    [Serializable]
    public class MilestoneSave
    {
        public List<string> done = new List<string>();
        /// <summary>Chunks collected per mineral id.</summary>
        public List<CountSave> mined = new List<CountSave>();
        /// <summary>Meteors broken per sector id.</summary>
        public List<CountSave> broken = new List<CountSave>();
        public long brokenTotal;
    }

    [Serializable]
    public class CountSave
    {
        public string id;
        public long count;
    }
}
