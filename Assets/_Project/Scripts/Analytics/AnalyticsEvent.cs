using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VoidProspector.Analytics
{
    /// <summary>
    /// One analytics event: a name and flat parameters (strings, numbers, booleans). Written as one JSON object per
    /// line, so any backend (a local file now, Firebase or Unity Analytics later) can take the same events.
    /// </summary>
    public sealed class AnalyticsEvent
    {
        readonly List<(string key, object value)> _params = new List<(string, object)>();

        public string Name { get; }
        public DateTime UtcTime { get; }
        public IReadOnlyList<(string key, object value)> Params => _params;

        public AnalyticsEvent(string name, DateTime utcTime)
        {
            Name = name;
            UtcTime = utcTime;
        }

        public AnalyticsEvent Add(string key, string value) { _params.Add((key, value)); return this; }
        public AnalyticsEvent Add(string key, double value) { _params.Add((key, value)); return this; }
        public AnalyticsEvent Add(string key, long value) { _params.Add((key, value)); return this; }
        public AnalyticsEvent Add(string key, int value) { _params.Add((key, (long)value)); return this; }
        public AnalyticsEvent Add(string key, bool value) { _params.Add((key, value)); return this; }

        public object Get(string key)
        {
            foreach (var (k, v) in _params) if (k == key) return v;
            return null;
        }

        /// <summary>{"event":"name","utc":"2026-11-09T12:00:00.000Z",...params}</summary>
        public string ToJson()
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"event\":");
            Quote(sb, Name);
            sb.Append(",\"utc\":");
            Quote(sb, UtcTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            foreach (var (k, v) in _params)
            {
                sb.Append(',');
                Quote(sb, k);
                sb.Append(':');
                switch (v)
                {
                    case null: sb.Append("null"); break;
                    case string s: Quote(sb, s); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                    case double d:
                        sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture));
                        break;
                    default: Quote(sb, Convert.ToString(v, CultureInfo.InvariantCulture)); break;
                }
            }
            sb.Append('}');
            return sb.ToString();
        }

        static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Where events go. Implementations must not throw into gameplay.</summary>
    public interface IAnalyticsSink
    {
        void Send(AnalyticsEvent e);
        void Flush();
    }

    /// <summary>Keeps events in memory (tests, and the debug panel's count).</summary>
    public sealed class MemorySink : IAnalyticsSink
    {
        public readonly List<AnalyticsEvent> events = new List<AnalyticsEvent>();
        public void Send(AnalyticsEvent e) => events.Add(e);
        public void Flush() { }
    }
}
