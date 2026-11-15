using System.Collections.Generic;
using UnityEngine;
using R3;
using UnityEngine.Pool;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.UI;

namespace VoidProspector.Mining
{
    /// <summary>
    /// Keeps 6–12 meteors drifting through the field above the ship. Meteors enter from the top or a side, drift down
    /// and sideways, leave through a side, and are pooled.
    /// </summary>
    public class MeteorField : MonoBehaviour
    {
        public GameRoot Root { get; private set; }
        public IReadOnlyList<Meteor> Active => _active;

        readonly List<Meteor> _active = new List<Meteor>();
        readonly Subject<Meteor> _spawned = new Subject<Meteor>();

        /// <summary>Fires when a meteor enters the field (the scanner checks it for rare contents).</summary>
        public Observable<Meteor> Spawned => _spawned;
        ObjectPool<Meteor> _pool;
        Transform _container;
        int _target;
        float _timer;

        MiningSettings S => Root.Session.Mining;

        /// <summary>Bottom of the meteor band (just above the ship).</summary>
        public float MinY => Root.Ship.position.y + 2.2f;
        /// <summary>Top of the meteor band (just under the top bar and toast).</summary>
        public float MaxY => Root.HalfH - Root.UiToWorld(Hud.TopBarHeight + 60f);

        public void Init(GameRoot root)
        {
            Root = root;
            _container = new GameObject("Meteors").transform;
            _pool = new ObjectPool<Meteor>(Create, null, m => m.gameObject.SetActive(false), m => Destroy(m.gameObject),
                collectionCheck: false, defaultCapacity: 16);
            PickTarget();
            for (int i = 0; i < _target; i++) Spawn(true);
        }

        void PickTarget() => _target = Random.Range(S.minMeteors, S.maxMeteors + 1);

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_active.Count < _target && _timer <= 0)
            {
                Spawn(false);
                _timer = Random.Range(S.respawnDelay.x, S.respawnDelay.y);
                PickTarget();
            }
        }

        Meteor Create()
        {
            var go = new GameObject("Meteor");
            go.transform.SetParent(_container, false);
            var m = go.AddComponent<Meteor>();
            m.Build(this);
            return m;
        }

        void Spawn(bool onScreen)
        {
            var spawn = Root.Session.RollMeteor();
            float r = spawn.size.radius;
            // fromLeft = moving right. The belt travels with the ship, so meteors drift down and sideways
            // (the starfield scrolling past sells the forward motion) and leave through a side.
            bool fromLeft = Random.value < 0.5f;
            bool fromTop = !onScreen && Random.value < S.meteorTopEntryChance;
            float halfW = Root.HalfW;
            float low = MinY + r, high = Mathf.Max(low, MaxY - r);
            Vector2 pos = default;

            // Try a few positions and keep the first that doesn't overlap another meteor.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float x, y;
                if (onScreen)
                {
                    x = Random.Range(-halfW + r, halfW - r);
                    y = Random.Range(low, high);
                }
                else if (fromTop)
                {
                    // Just behind the top bar; not targetable until it drifts into the band.
                    x = Random.Range(-halfW + r, halfW - r);
                    y = MaxY + r + 0.2f;
                }
                else
                {
                    // Upper two thirds of the band, so it has room to fall before it crosses.
                    x = fromLeft ? -halfW - r - 0.2f : halfW + r + 0.2f;
                    y = Random.Range(Mathf.Lerp(low, high, 0.33f), high);
                }
                pos = new Vector2(x, y);
                if (!Overlaps(pos, r)) break;
            }

            float speed = Random.Range(S.driftSpeed.x, S.driftSpeed.y);
            float fall = Random.Range(S.meteorFallSpeed.x, S.meteorFallSpeed.y);
            var vel = new Vector2((fromLeft ? 1 : -1) * speed, -fall);

            var m = _pool.Get();
            m.Launch(spawn, pos, vel);
            _active.Add(m);
            _spawned.OnNext(m);
        }

        bool Overlaps(Vector2 p, float r)
        {
            foreach (var m in _active)
                if (Vector2.Distance(m.transform.position, p) < m.Radius + r + 0.15f) return true;
            return false;
        }

        public bool IsOutside(Meteor m)
        {
            float x = m.transform.position.x;
            float limit = Root.HalfW + m.Radius + 0.5f;
            return x < -limit || x > limit;
        }

        public void Release(Meteor m)
        {
            if (!m.Active) return;
            m.Deactivate();
            _active.Remove(m);
            _pool.Release(m);
        }

        /// <summary>The meteor under a world point (allowing some padding), or null.</summary>
        public Meteor AtPoint(Vector2 p, float padding)
        {
            Meteor best = null;
            float bestEdge = float.MaxValue;
            foreach (var m in _active)
            {
                float edge = Vector2.Distance(m.transform.position, p) - m.Radius;
                if (edge <= padding && edge < bestEdge) { bestEdge = edge; best = m; }
            }
            return best;
        }

        /// <summary>
        /// On screen and inside the band: auto-fire and drones only shoot these (not meteors still entering from a
        /// side or from behind the top bar).
        /// </summary>
        public bool IsTargetable(Meteor m)
        {
            Vector2 p = m.transform.position;
            return m.Active && Mathf.Abs(p.x) <= Root.HalfW - 0.2f && p.y <= MaxY;
        }

        /// <summary>Nearest targetable meteor (used by auto-fire), or null.</summary>
        public Meteor Nearest(Vector2 from)
        {
            Meteor best = null;
            float bestD = float.MaxValue;
            foreach (var m in _active)
            {
                if (!IsTargetable(m)) continue;
                float d = ((Vector2)m.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }
    }
}
