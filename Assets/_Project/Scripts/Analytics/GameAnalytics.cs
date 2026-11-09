using System;
using R3;
using VoidProspector.Core;

namespace VoidProspector.Analytics
{
    /// <summary>
    /// Turns what happens in a <see cref="GameSession"/> into analytics events: sessions (with where the player
    /// stopped), purchases, sector and depth unlocks, milestones, codex entries, refining and offline payouts.
    /// Pure C# (the clock is passed in), so it's tested like the rest of the game logic. Times are in play seconds
    /// (<see cref="PlayStats.PlaySeconds"/>): active time with the game open, across all sessions.
    /// </summary>
    public sealed class GameAnalytics : IDisposable
    {
        readonly GameSession _session;
        readonly IAnalyticsSink _sink;
        readonly Func<DateTime> _utcNow;
        readonly CompositeDisposable _subs = new CompositeDisposable();
        double _sessionStartPlay, _lastPurchasePlay = -1;
        string _lastUpgrade = "";
        bool _inSession;

        public GameAnalytics(GameSession session, IAnalyticsSink sink, Func<DateTime> utcNow)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);

            var s = session;
            s.Upgrades.Bought
                .Where(b => b.levels > 0)   // levels 0 = set without paying (save load, debug)
                .Subscribe(b =>
                {
                    _lastPurchasePlay = Play;
                    _lastUpgrade = b.track.Definition.id;
                    Send(Ev("upgrade_bought").Add("id", b.track.Definition.id).Add("level", b.track.Level).Add("levels", b.levels));
                })
                .AddTo(_subs);
            s.UnlockedSectorRx.Skip(1)
                .Subscribe(index => Send(Ev("sector_unlocked").Add("sector_index", index)))
                .AddTo(_subs);
            s.Depths.DeepestRx.Skip(1)
                .Subscribe(d => Send(Ev("depth_unlocked").Add("unlocked_depth", d + 1)))
                .AddTo(_subs);
            s.Milestones.Completed
                .Subscribe(m => Send(Ev("milestone").Add("id", m.id)))
                .AddTo(_subs);
            s.Codex.Discovered
                .Subscribe(e => Send(Ev("codex_entry").Add("mineral", e.mineral.id).Add("grade", e.grade).Add("entries", s.Codex.Count)))
                .AddTo(_subs);
            s.Refinery.Stepped
                .Where(st => st.finished || (!st.success && s.Refinery.IsEmpty))
                .Subscribe(st => Send(Ev(st.finished ? "refine_done" : "refine_lost").Add("grade", st.grade).Add("count", s.Refinery.Count)))
                .AddTo(_subs);
        }

        double Play => _session.Stats.PlaySeconds;

        /// <summary>A new event with the common fields: play time, sector and depth.</summary>
        public AnalyticsEvent Ev(string name) => new AnalyticsEvent(name, _utcNow())
            .Add("play_s", Math.Round(Play, 1))
            .Add("sector", _session.Sector.id)
            .Add("depth", _session.Depth + 1);

        public void Send(AnalyticsEvent e)
        {
            try { _sink.Send(e); }
            catch { /* analytics must never break the game */ }
        }

        /// <param name="reason">launch (app started), resume (back from the background) or reload (scene restart, e.g. travel).</param>
        public void StartSession(string reason)
        {
            if (_inSession) return;
            _inSession = true;
            if (reason == "launch") _session.Stats.StartSession(_utcNow());
            _sessionStartPlay = Play;
            Send(Ev("session_start")
                .Add("reason", reason)
                .Add("session", _session.Stats.Sessions)
                .Add("credits", _session.Wallet.Credits.Value)
                .Add("unlocked_sector", _session.UnlockedSector));
        }

        /// <summary>Records where the player stopped. <paramref name="reason"/>: pause, quit or reload.</summary>
        public void EndSession(string reason)
        {
            if (!_inSession) return;
            _inSession = false;
            Send(Ev("session_end")
                .Add("reason", reason)
                .Add("session_s", Math.Round(Play - _sessionStartPlay, 1))
                .Add("since_purchase_s", _lastPurchasePlay >= 0 ? Math.Round(Play - _lastPurchasePlay, 1) : -1)
                .Add("last_upgrade", _lastUpgrade)
                .Add("credits", _session.Wallet.Credits.Value)
                .Add("cargo", _session.Cargo.Count)
                .Add("drones", _session.DroneCount)
                .Add("milestones", _session.Milestones.DoneCount)
                .Add("codex", _session.Codex.Count));
            Flush();
        }

        public void Offline(double awaySeconds, double paidSeconds, GameNumber credits) =>
            Send(Ev("offline_paid").Add("away_s", Math.Round(awaySeconds)).Add("paid_s", Math.Round(paidSeconds)).Add("credits", credits.Value));

        public void Travel(string toSectorId) => Send(Ev("travel").Add("to", toSectorId));

        public void Flush()
        {
            try { _sink.Flush(); }
            catch { }
        }

        public void Dispose() => _subs.Dispose();
    }
}
