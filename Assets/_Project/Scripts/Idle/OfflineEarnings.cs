using System;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Economy;

namespace VoidProspector.Idle
{
    /// <summary>What the player earned while away.</summary>
    public readonly struct OfflineResult
    {
        /// <summary>Real time away in seconds (0 if the clock went backwards).</summary>
        public readonly double awaySeconds;
        /// <summary>Time that pays out: away time capped at the offline cap.</summary>
        public readonly double paidSeconds;
        public readonly double incomePerSecond;
        public readonly GameNumber credits;
        public readonly bool capped;
        /// <summary>The saved time is in the future: the device clock was changed, so nothing is paid.</summary>
        public readonly bool clockWentBack;

        public OfflineResult(double awaySeconds, double paidSeconds, double incomePerSecond, GameNumber credits, bool capped, bool clockWentBack)
        {
            this.awaySeconds = awaySeconds;
            this.paidSeconds = paidSeconds;
            this.incomePerSecond = incomePerSecond;
            this.credits = credits;
            this.capped = capped;
            this.clockWentBack = clockWentBack;
        }

        /// <summary>Away long enough to tell the player about it (even if nothing was earned).</summary>
        public bool IsAway => paidSeconds > 0;
    }

    public static class OfflineEarnings
    {
        /// <summary>
        /// Income per second × time away (capped), × efficiency. Absences shorter than the minimum pay nothing.
        /// If the saved time is later than now, the clock was moved back after saving (or forward before): ignore it.
        /// </summary>
        public static OfflineResult Calculate(DateTime savedUtc, DateTime nowUtc, double incomePerSecond, OfflineSettings settings)
        {
            double away = (nowUtc.ToUniversalTime() - savedUtc.ToUniversalTime()).TotalSeconds;
            if (away < 0) return new OfflineResult(0, 0, incomePerSecond, GameNumber.Zero, false, true);
            if (away < settings.minAwaySeconds) return new OfflineResult(away, 0, incomePerSecond, GameNumber.Zero, false, false);

            double paid = Math.Min(away, settings.CapSeconds);
            double credits = Math.Floor(Math.Max(0, incomePerSecond) * paid * settings.efficiency);
            return new OfflineResult(away, paid, incomePerSecond, credits, away > settings.CapSeconds, false);
        }

        /// <summary>
        /// Estimates idle income by simulating a copy of the session with no taps and no purchases: auto-fire and
        /// drones mine at the current depth and everything collected counts. 0 until auto-fire or a drone is bought.
        /// Deterministic. Also shown in the HUD as the idle income readout.
        /// </summary>
        public static double EstimateIncomePerSecond(GameSession live, double sampleSeconds, int seed = 1)
        {
            if (!live.HasIdleMining || sampleSeconds <= 0) return 0;

            var copy = new GameSession(live.Database, live.Sector, seed);
            SaveSystem.Apply(copy, SaveSystem.Capture(live, DateTime.UtcNow));
            copy.Cargo.TakeAll();   // only count what's mined during the sample

            var result = EconomySimulator.Run(copy, new EconomySimulator.Settings
            {
                duration = sampleSeconds,
                tapsPerSecond = 0,
                pushDepth = false,
                buyUpgrades = false,
                buyJumpDrive = false,
            });
            return result.earned.Value / sampleSeconds;
        }
    }
}
