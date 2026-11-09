using System;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.UI;

namespace VoidProspector.Idle
{
    /// <summary>
    /// Pays offline earnings when the game starts from a save and when the app returns from the background
    /// (a phone may resume the app instead of restarting it). Credits go straight into the wallet and are saved,
    /// so closing the app before reading the popup loses nothing.
    /// </summary>
    public class OfflineController : MonoBehaviour
    {
        static readonly Color HintColor = new Color(0.75f, 0.85f, 1f);

        GameRoot _root;
        WelcomeBackPopup _popup;
        DateTime? _pausedAtUtc;

        public void Init(GameRoot root, DateTime? savedAtUtc)
        {
            _root = root;
            _popup = gameObject.AddComponent<WelcomeBackPopup>();
            _popup.Init(root);
            if (savedAtUtc.HasValue) Pay(savedAtUtc.Value);
        }

        void OnApplicationPause(bool paused)
        {
            if (_root == null) return;
            if (paused) _pausedAtUtc = DateTime.UtcNow;
            else if (_pausedAtUtc.HasValue)
            {
                Pay(_pausedAtUtc.Value);
                _pausedAtUtc = null;
            }
        }

        /// <summary>Pays for the time since <paramref name="sinceUtc"/>. Also used by the debug panel.</summary>
        public void Pay(DateTime sinceUtc)
        {
            var session = _root.Session;
            var settings = session.Database.offline;
            var now = DateTime.UtcNow;

            var probe = OfflineEarnings.Calculate(sinceUtc, now, 0, settings);
            if (probe.clockWentBack)
            {
                Debug.LogWarning($"Offline: saved time {sinceUtc:u} is after now {now:u}; the device clock changed, nothing paid");
                return;
            }
            if (!probe.IsAway) return;

            string refined = AdvanceRefinery(Math.Min(probe.awaySeconds, settings.CapSeconds));

            double rate = OfflineEarnings.EstimateIncomePerSecond(session, settings.incomeSampleSeconds);
            var result = OfflineEarnings.Calculate(sinceUtc, now, rate, settings);
            if (result.credits <= GameNumber.Zero)
            {
                if (refined != null)
                {
                    _root.Saves.Save();
                    _root.Fx.Toast(refined, HintColor);
                }
                else if (!session.HasIdleMining)
                    _root.Fx.Toast("Buy Auto-fire to mine while you're away", HintColor);
                return;
            }

            session.Wallet.Add(result.credits);
            _root.Saves.Save();
            _popup.Show(result, refined);
        }

        /// <summary>Runs the refinery for the time away; returns a line for the player, or null if it was idle.</summary>
        string AdvanceRefinery(double seconds)
        {
            var session = _root.Session;
            var r = session.Refinery;
            if (!r.IsRunning) return null;
            var mineral = r.Mineral;
            int grade = r.Grade, count = r.Count;
            session.TickRefinery(seconds);

            if (r.IsEmpty) return $"Refinery: the {mineral.displayName} batch was lost while you were away";
            string name = session.Purity[r.Grade].name;
            string lost = r.Count < count ? $", {count - r.Count} lost" : "";
            return r.Grade > grade
                ? $"Refinery: {r.Count} {mineral.displayName} refined to {name}{lost}"
                : $"Refinery: {mineral.displayName} still {name}{lost}";
        }
    }
}
