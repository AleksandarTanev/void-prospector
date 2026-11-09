using System;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoidProspector.Core
{
    /// <summary>
    /// Decides when to save: every 30 seconds, right after purchases, depth and sector unlocks, milestones and setting changes, and
    /// whenever the app may be killed (pause, focus loss, quit, scene unload). Saves are small, so they're
    /// written synchronously; several requests in one frame become one write.
    /// </summary>
    public class SaveController : MonoBehaviour
    {
        /// <summary>Seconds of real time between automatic saves.</summary>
        public const float AutosaveSeconds = 30f;

        GameRoot _root;
        SaveStore _store;
        bool _requested;
        bool _suspended;
        float _sinceSave;

        public SaveStore Store => _store;
        public DateTime LastSavedUtc { get; private set; }

        public void Init(GameRoot root, SaveStore store)
        {
            _root = root;
            _store = store;
            var s = root.Session;
            s.Upgrades.Bought.Subscribe(_ => _requested = true).AddTo(this);
            s.Depths.DeepestRx.Skip(1).Subscribe(_ => _requested = true).AddTo(this);
            s.AutoSellBelowRx.Skip(1).Subscribe(_ => _requested = true).AddTo(this);
            s.UnlockedSectorRx.Skip(1).Subscribe(_ => _requested = true).AddTo(this);
            s.Milestones.Completed.Subscribe(_ => _requested = true).AddTo(this);
        }

        /// <summary>Saves at the end of this frame.</summary>
        public void RequestSave() => _requested = true;

        void Update()
        {
            _sinceSave += Time.unscaledDeltaTime;
            if (_sinceSave >= AutosaveSeconds) Save();
        }

        void LateUpdate()
        {
            if (_requested) Save();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }

        void OnApplicationQuit() => Save();

        void OnDestroy() => Save();

        public void Save() => Save(DateTime.UtcNow);

        void Save(DateTime savedAtUtc)
        {
            _requested = false;
            _sinceSave = 0;
            if (_suspended || _store == null || _root == null || _root.Session == null) return;
            try
            {
                _store.Write(SaveSystem.ToJson(SaveSystem.Capture(_root.Session, savedAtUtc)));
                LastSavedUtc = savedAtUtc;
            }
            catch (Exception e)
            {
                Debug.LogError($"Save failed ({_store.FilePath}): {e.Message}");
            }
        }

        /// <summary>Saves now and reloads the scene, which rebuilds the world from the save (used to travel between sectors).</summary>
        public void SaveAndReload()
        {
            Save();
            _suspended = true;
            Reload();
        }

        // ---------- Debug tools ----------

        /// <summary>Deletes the save and restarts as a new player.</summary>
        public void WipeAndRestart()
        {
            _suspended = true;
            _store.Delete();
            Reload();
        }

        /// <summary>Saves as if it were written <paramref name="away"/> ago, then restarts, to test offline earnings.</summary>
        public void PretendAwayAndRestart(TimeSpan away)
        {
            Save(DateTime.UtcNow - away);
            _suspended = true;
            Reload();
        }

        static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
