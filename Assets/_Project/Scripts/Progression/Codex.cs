using System.Collections.Generic;
using R3;
using VoidProspector.Data;

namespace VoidProspector.Progression
{
    /// <summary>
    /// The Mineral Codex: one entry per mineral × purity grade. Finding a grade for the first time (collecting it or
    /// refining up to it) fills the entry and gives that mineral a small permanent value bonus.
    /// </summary>
    public sealed class Codex
    {
        readonly CodexSettings _settings;
        readonly HashSet<(MineralDefinition mineral, int grade)> _found = new HashSet<(MineralDefinition, int)>();
        readonly Dictionary<MineralDefinition, double> _bonus = new Dictionary<MineralDefinition, double>();
        readonly Subject<(MineralDefinition mineral, int grade)> _discovered = new Subject<(MineralDefinition, int)>();

        public Codex(CodexSettings settings) => _settings = settings ?? new CodexSettings();

        /// <summary>Fires when an entry is filled during play (not when a save is restored).</summary>
        public Observable<(MineralDefinition mineral, int grade)> Discovered => _discovered;

        public int Count => _found.Count;
        public IEnumerable<(MineralDefinition mineral, int grade)> Entries => _found;
        public bool Has(MineralDefinition mineral, int grade) => _found.Contains((mineral, grade));

        public double BonusFor(int grade) =>
            _settings.bonusPerGrade != null && grade >= 0 && grade < _settings.bonusPerGrade.Length ? _settings.bonusPerGrade[grade] : 0;

        /// <summary>1 + the bonuses of this mineral's filled entries.</summary>
        public double ValueMultiplier(MineralDefinition mineral) =>
            1 + (mineral != null && _bonus.TryGetValue(mineral, out double b) ? b : 0);

        /// <summary>Fills an entry. Returns true the first time.</summary>
        public bool Record(MineralDefinition mineral, int grade)
        {
            if (!Add(mineral, grade)) return false;
            _discovered.OnNext((mineral, grade));
            return true;
        }

        bool Add(MineralDefinition mineral, int grade)
        {
            if (mineral == null || grade < 0 || !_found.Add((mineral, grade))) return false;
            _bonus[mineral] = (_bonus.TryGetValue(mineral, out double b) ? b : 0) + BonusFor(grade);
            return true;
        }

        public void Restore(IEnumerable<(MineralDefinition mineral, int grade)> entries)
        {
            _found.Clear();
            _bonus.Clear();
            foreach (var (m, g) in entries) Add(m, g);
        }
    }
}
