using System;
using System.Collections.Generic;
using R3;
using VoidProspector.Data;

namespace VoidProspector.Progression
{
    /// <summary>
    /// Lifetime counters (chunks mined per mineral, meteors broken per sector) and the milestones they complete.
    /// Each milestone completes once; <see cref="Completed"/> fires so the session can pay the reward.
    /// Completed milestones give permanent value bonuses through <see cref="ValueMultiplier"/>.
    /// </summary>
    public sealed class Milestones
    {
        readonly MilestoneDefinition[] _all;
        readonly HashSet<MilestoneDefinition> _done = new HashSet<MilestoneDefinition>();
        readonly Dictionary<MineralDefinition, long> _mined = new Dictionary<MineralDefinition, long>();
        readonly Dictionary<SectorDefinition, long> _broken = new Dictionary<SectorDefinition, long>();
        readonly Dictionary<MineralDefinition, double> _bonus = new Dictionary<MineralDefinition, double>();
        readonly Subject<MilestoneDefinition> _completed = new Subject<MilestoneDefinition>();
        readonly Func<SectorDefinition, int> _deepestShown;
        double _globalBonus;
        long _brokenTotal;

        /// <param name="deepestShown">Deepest unlocked depth of a sector as shown to the player (starting at 1).</param>
        public Milestones(IEnumerable<MilestoneDefinition> definitions, Func<SectorDefinition, int> deepestShown)
        {
            var list = new List<MilestoneDefinition>();
            foreach (var d in definitions) if (d != null) list.Add(d);
            _all = list.ToArray();
            _deepestShown = deepestShown ?? throw new ArgumentNullException(nameof(deepestShown));
        }

        /// <summary>Fires once per milestone, the moment it's completed (not when a save is restored).</summary>
        public Observable<MilestoneDefinition> Completed => _completed;

        public IReadOnlyList<MilestoneDefinition> All => _all;
        public int DoneCount => _done.Count;
        public bool IsDone(MilestoneDefinition m) => _done.Contains(m);

        public long MinedCount(MineralDefinition mineral) => mineral != null && _mined.TryGetValue(mineral, out long n) ? n : 0;
        public long BrokenCount(SectorDefinition sector) => sector == null ? _brokenTotal : _broken.TryGetValue(sector, out long n) ? n : 0;

        /// <summary>Progress toward a milestone, capped at its target.</summary>
        public long Progress(MilestoneDefinition m)
        {
            long current = m.goal switch
            {
                MilestoneGoal.MineMineral => MinedCount(m.mineral),
                MilestoneGoal.BreakMeteors => BrokenCount(m.sector),
                MilestoneGoal.ReachDepth => m.sector != null ? _deepestShown(m.sector) : 0,
                _ => 0,
            };
            return Math.Min(current, m.target);
        }

        /// <summary>Chunk value multiplier from completed milestones: 1 + bonuses for all minerals + bonuses for this one.</summary>
        public double ValueMultiplier(MineralDefinition mineral) =>
            1 + _globalBonus + (mineral != null && _bonus.TryGetValue(mineral, out double b) ? b : 0);

        public void RecordMined(MineralDefinition mineral)
        {
            if (mineral == null) return;
            _mined[mineral] = MinedCount(mineral) + 1;
            Check(MilestoneGoal.MineMineral);
        }

        public void RecordBreak(SectorDefinition sector)
        {
            _brokenTotal++;
            if (sector != null) _broken[sector] = BrokenCount(sector) + 1;
            Check(MilestoneGoal.BreakMeteors);
        }

        /// <summary>Call when a depth is unlocked.</summary>
        public void CheckDepths() => Check(MilestoneGoal.ReachDepth);

        void Check(MilestoneGoal goal)
        {
            foreach (var m in _all)
            {
                if (m.goal != goal || _done.Contains(m) || Progress(m) < m.target) continue;
                MarkDone(m);
                _completed.OnNext(m);
            }
        }

        void MarkDone(MilestoneDefinition m)
        {
            if (!_done.Add(m) || m.valueBonus <= 0) return;
            if (m.bonusMineral == null) _globalBonus += m.valueBonus;
            else _bonus[m.bonusMineral] = (_bonus.TryGetValue(m.bonusMineral, out double b) ? b : 0) + m.valueBonus;
        }

        public IEnumerable<MilestoneDefinition> Done => _done;
        public IEnumerable<KeyValuePair<MineralDefinition, long>> Mined => _mined;
        public IEnumerable<KeyValuePair<SectorDefinition, long>> Broken => _broken;
        public long BrokenTotal => _brokenTotal;

        /// <summary>Restores counters and completed milestones from a save. Rewards are not paid again.</summary>
        public void Restore(IEnumerable<MilestoneDefinition> done, IEnumerable<(MineralDefinition, long)> mined,
            IEnumerable<(SectorDefinition, long)> broken, long brokenTotal)
        {
            _done.Clear();
            _mined.Clear();
            _broken.Clear();
            _bonus.Clear();
            _globalBonus = 0;
            foreach (var m in done) if (m != null) MarkDone(m);
            foreach (var (mineral, n) in mined) if (mineral != null && n > 0) _mined[mineral] = n;
            long sum = 0;
            foreach (var (sector, n) in broken)
                if (sector != null && n > 0) { _broken[sector] = n; sum += n; }
            _brokenTotal = Math.Max(brokenTotal, sum);
        }
    }
}
