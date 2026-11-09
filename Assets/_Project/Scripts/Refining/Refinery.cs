using System;
using R3;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Refining
{
    /// <summary>What one refining attempt did.</summary>
    public readonly struct RefineStep
    {
        public readonly bool success;
        /// <summary>The batch's grade after the attempt.</summary>
        public readonly int grade;
        /// <summary>Chunks lost (failed attempts only).</summary>
        public readonly int lost;
        /// <summary>The batch reached its target grade (or the top) and stopped.</summary>
        public readonly bool finished;

        public RefineStep(bool success, int grade, int lost, bool finished)
        {
            this.success = success;
            this.grade = grade;
            this.lost = lost;
            this.finished = finished;
        }
    }

    /// <summary>The upgrade values the refinery works with.</summary>
    public readonly struct RefineryStats
    {
        public readonly double speed;
        public readonly double successBonus;
        public readonly double stabiliser;

        public RefineryStats(double speed, double successBonus, double stabiliser)
        {
            this.speed = Math.Max(0.01, speed);
            this.successBonus = successBonus;
            this.stabiliser = Math.Max(0, Math.Min(1, stabiliser));
        }
    }

    /// <summary>
    /// One batch of chunks of a single mineral and grade. Each attempt takes the next stage's time; it either
    /// raises the grade (value × the purity multiplier ratio) or fails and loses part of the batch. It keeps
    /// going until the batch reaches the target grade. Tick-based and pure, so offline time can run it too.
    /// </summary>
    public sealed class Refinery
    {
        readonly RefinerySettings _settings;
        readonly PurityTable _purity;
        readonly Random _rng;
        readonly Subject<RefineStep> _stepped = new Subject<RefineStep>();
        int _targetGrade;

        public Refinery(RefinerySettings settings, PurityTable purity, Random rng)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _purity = purity ? purity : throw new ArgumentNullException(nameof(purity));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _targetGrade = TopGrade;
        }

        /// <summary>Fires after every attempt.</summary>
        public Observable<RefineStep> Stepped => _stepped;

        public MineralDefinition Mineral { get; private set; }
        public int Grade { get; private set; }
        public int Count { get; private set; }
        /// <summary>Total sell value of the batch.</summary>
        public GameNumber Value { get; private set; }
        /// <summary>Seconds into the current attempt.</summary>
        public double Progress { get; private set; }

        public int TopGrade => _purity.TopGrade;
        public bool IsEmpty => Count <= 0;
        public bool IsDone => !IsEmpty && Grade >= Math.Min(TargetGrade, TopGrade);
        public bool IsRunning => !IsEmpty && !IsDone;

        /// <summary>Refining stops when the batch reaches this grade. Never below the next grade up from the lowest.</summary>
        public int TargetGrade
        {
            get => _targetGrade;
            set => _targetGrade = Math.Max(1, Math.Min(value, TopGrade));
        }

        /// <summary>The stage for the attempt from <paramref name="grade"/> to the next grade, or null at the top.</summary>
        public RefineStage StageFrom(int grade) =>
            grade >= 0 && grade < _settings.stages.Length && grade < TopGrade ? _settings.stages[grade] : null;

        public double CycleSeconds(int grade, RefineryStats stats) => (StageFrom(grade)?.seconds ?? 0) / stats.speed;

        public double SuccessChance(int grade, RefineryStats stats)
        {
            var st = StageFrom(grade);
            return st == null ? 0 : Math.Min(_settings.maxSuccessChance, st.successChance + stats.successBonus);
        }

        public double FailLoss(int grade, RefineryStats stats) => (StageFrom(grade)?.failLoss ?? 0) * (1 - stats.stabiliser);

        /// <summary>Value multiplier of the next grade over this one (5× / 2× for Standard → Refined is 2.5).</summary>
        public double GradeRatio(int grade) =>
            grade < TopGrade ? _purity[grade + 1].valueMultiplier / Math.Max(1e-9, _purity[grade].valueMultiplier) : 1;

        /// <summary>
        /// Expected batch value after one attempt from <paramref name="grade"/>, as a multiple of its value now.
        /// Above 1 = worth trying (ignoring time); below 1 = a gamble that loses on average.
        /// </summary>
        public double ExpectedValueRatio(int grade, RefineryStats stats)
        {
            if (StageFrom(grade) == null) return 1;
            double p = SuccessChance(grade, stats);
            return p * GradeRatio(grade) + (1 - p) * (1 - FailLoss(grade, stats));
        }

        /// <summary>Starts a batch. Only when empty; the grade must be below the top.</summary>
        public bool Load(MineralDefinition mineral, int grade, int count, GameNumber value)
        {
            if (!IsEmpty || mineral == null || count <= 0 || grade < 0 || grade >= TopGrade) return false;
            Mineral = mineral;
            Grade = grade;
            Count = count;
            Value = GameNumber.Max(value, GameNumber.Zero);
            Progress = 0;
            if (TargetGrade <= grade) TargetGrade = TopGrade;
            return true;
        }

        /// <summary>Empties the refinery and returns what was in it.</summary>
        public (MineralDefinition mineral, int grade, int count, GameNumber value) Take()
        {
            var batch = (Mineral, Grade, Count, Value);
            Clear();
            return batch;
        }

        void Clear()
        {
            Mineral = null;
            Grade = 0;
            Count = 0;
            Value = GameNumber.Zero;
            Progress = 0;
        }

        /// <summary>Advances time; any number of attempts may complete (offline time is one big tick).</summary>
        public void Tick(double dt, RefineryStats stats)
        {
            while (dt > 0 && IsRunning)
            {
                double cycle = CycleSeconds(Grade, stats);
                double need = cycle - Progress;
                if (dt < need)
                {
                    Progress += dt;
                    return;
                }
                dt -= need;
                Progress = 0;
                Attempt(stats);
            }
        }

        void Attempt(RefineryStats stats)
        {
            if (_rng.NextDouble() < SuccessChance(Grade, stats))
            {
                Value *= GradeRatio(Grade);
                Grade++;
                _stepped.OnNext(new RefineStep(true, Grade, 0, IsDone));
                return;
            }

            int lost = Math.Min(Count, (int)Math.Ceiling(Count * FailLoss(Grade, stats) - 1e-9));
            if (lost > 0)
            {
                Value = Count - lost > 0 ? Value * (Count - lost) / Count : GameNumber.Zero;
                Count -= lost;
            }
            int grade = Grade;
            if (Count <= 0) Clear();
            _stepped.OnNext(new RefineStep(false, grade, lost, false));
        }

        /// <summary>Restores a saved batch exactly.</summary>
        public void Restore(MineralDefinition mineral, int grade, int count, GameNumber value, double progress, int targetGrade)
        {
            Clear();
            TargetGrade = targetGrade > 0 ? targetGrade : TopGrade;
            if (mineral == null || count <= 0) return;
            Mineral = mineral;
            Grade = Math.Max(0, Math.Min(grade, TopGrade));
            Count = count;
            Value = GameNumber.Max(value, GameNumber.Zero);
            Progress = Math.Max(0, progress);
        }
    }
}
