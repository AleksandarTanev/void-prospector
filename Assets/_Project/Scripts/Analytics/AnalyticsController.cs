using System;
using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Analytics
{
    /// <summary>
    /// Runs <see cref="GameAnalytics"/> in the game: counts play time, starts and ends sessions around app launch,
    /// pause, resume, quit and scene reloads, and flushes the local event file every 30 seconds.
    /// </summary>
    public class AnalyticsController : MonoBehaviour
    {
        const float FlushSeconds = 30f;

        static bool _launched;
        GameRoot _root;
        float _sinceFlush;

        public GameAnalytics Game { get; private set; }
        public JsonLinesSink Sink { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _launched = false;   // play mode without domain reload

        public void Init(GameRoot root, string filePath)
        {
            _root = root;
            Sink = new JsonLinesSink(filePath);
            Game = new GameAnalytics(root.Session, Sink, () => DateTime.UtcNow);
            Game.StartSession(_launched ? "reload" : "launch");
            _launched = true;
        }

        void Update()
        {
            if (_root == null) return;
            // Real time while the game is open; a long first frame after resuming doesn't count as play.
            _root.Session.Stats.Tick(Math.Min(Time.unscaledDeltaTime, 1f));
            _sinceFlush += Time.unscaledDeltaTime;
            if (_sinceFlush >= FlushSeconds)
            {
                _sinceFlush = 0;
                Game.Flush();
                if (Sink.Error != null) Debug.LogWarning($"Analytics: couldn't write {Sink.FilePath}: {Sink.Error}");
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (Game == null) return;
            if (paused) Game.EndSession("pause");
            else Game.StartSession("resume");
        }

        void OnApplicationQuit() => Game?.EndSession("quit");

        void OnDestroy()
        {
            if (Game == null) return;
            Game.EndSession("reload");
            Game.Dispose();
        }
    }
}
