using System;

namespace VoidProspector.Core
{
    /// <summary>
    /// Lifetime play statistics kept in the save: total active play time (real seconds with the game open, not
    /// offline time) and the number of sessions. Used for analytics ("time to Sector 2") and future features.
    /// </summary>
    public sealed class PlayStats
    {
        public double PlaySeconds { get; private set; }
        public int Sessions { get; private set; }
        /// <summary>UTC time of the very first session; MinValue until one has started.</summary>
        public DateTime FirstPlayUtc { get; private set; } = DateTime.MinValue;

        public void Tick(double realSeconds)
        {
            if (realSeconds > 0) PlaySeconds += realSeconds;
        }

        public void StartSession(DateTime utcNow)
        {
            Sessions++;
            if (FirstPlayUtc == DateTime.MinValue) FirstPlayUtc = utcNow.ToUniversalTime();
        }

        public void Restore(double playSeconds, int sessions, long firstPlayUtcTicks)
        {
            PlaySeconds = Math.Max(0, playSeconds);
            Sessions = Math.Max(0, sessions);
            FirstPlayUtc = firstPlayUtcTicks > 0 && firstPlayUtcTicks <= DateTime.MaxValue.Ticks
                ? new DateTime(firstPlayUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
        }
    }
}
