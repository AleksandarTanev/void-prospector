using System;

namespace VoidProspector.Core
{
    /// <summary>Short durations for UI: "45s", "12m", "2h 05m", "3d 4h".</summary>
    public static class TimeFormatter
    {
        public static string Format(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(Math.Floor(seconds));
            if (t.TotalMinutes < 1) return $"{t.Seconds}s";
            if (t.TotalHours < 1) return $"{t.Minutes}m";
            if (t.TotalDays < 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
            return $"{(int)t.TotalDays}d {t.Hours}h";
        }
    }
}
