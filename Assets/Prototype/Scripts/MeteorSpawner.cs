using System.Collections.Generic;
using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>Keeps a field of drifting meteors alive above the ship. Pools meteors.</summary>
    public class MeteorSpawner : MonoBehaviour
    {
        public static MeteorSpawner I { get; private set; }

        public readonly List<Meteor> Active = new List<Meteor>();
        readonly Stack<Meteor> _pool = new Stack<Meteor>();
        Transform _root;
        int _target;
        float _timer;

        static ProtoBootstrap B => ProtoBootstrap.I;
        static ProtoConfig C => B.config;

        /// <summary>Bottom of the meteor field (just above the ship).</summary>
        public float MinY => B.Ship.position.y + 2.4f;
        /// <summary>Top of the meteor field (just under the top bar).</summary>
        public float MaxY => B.HalfH - 1.5f;

        void Awake()
        {
            I = this;
            _root = new GameObject("Meteors").transform;
            _target = Random.Range(C.minMeteors, C.maxMeteors + 1);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Start()
        {
            for (int i = 0; i < _target; i++) Spawn(true);
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (Active.Count < _target && _timer <= 0)
            {
                Spawn(false);
                _timer = Random.Range(0.3f, 1.0f);
                _target = Random.Range(C.minMeteors, C.maxMeteors + 1);
            }
        }

        void Spawn(bool onScreen)
        {
            var m = _pool.Count > 0 ? _pool.Pop() : CreateMeteor();
            int size = RollSize();
            float r = C.sizes[size].radius;
            bool fromLeft = Random.value < 0.5f;
            float x = 0, y = 0;
            // Try a few positions and keep the first that doesn't overlap another meteor.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                y = Random.Range(MinY + r, Mathf.Max(MinY + r, MaxY - r));
                x = onScreen
                    ? Random.Range(-B.HalfW + r, B.HalfW - r)
                    : (fromLeft ? -B.HalfW - r - 0.2f : B.HalfW + r + 0.2f);
                if (!Overlaps(new Vector2(x, y), r)) break;
            }
            float speed = Random.Range(C.driftSpeed.x, C.driftSpeed.y);
            var vel = new Vector2((fromLeft ? 1 : -1) * speed, Random.Range(-0.15f, 0.15f));
            m.Launch(size, new Vector2(x, y), vel);
            Active.Add(m);
        }

        bool Overlaps(Vector2 p, float r)
        {
            foreach (var m in Active)
                if (Vector2.Distance(m.transform.position, p) < m.Radius + r + 0.15f) return true;
            return false;
        }

        Meteor CreateMeteor()
        {
            var go = new GameObject("Meteor");
            go.transform.SetParent(_root, false);
            var m = go.AddComponent<Meteor>();
            m.Init(this);
            return m;
        }

        static int RollSize()
        {
            float total = 0;
            foreach (var s in C.sizes) total += s.weight;
            float r = Random.value * total;
            for (int i = 0; i < C.sizes.Length; i++)
            {
                r -= C.sizes[i].weight;
                if (r <= 0) return i;
            }
            return 0;
        }

        public bool IsOutside(Meteor m)
        {
            Vector2 p = m.transform.position;
            float mx = B.HalfW + m.Radius + 0.5f;
            return p.x < -mx || p.x > mx;
        }

        public void Release(Meteor m)
        {
            if (!m.Active) return;
            m.Deactivate();
            Active.Remove(m);
            _pool.Push(m);
        }

        bool OnScreen(Meteor m) => Mathf.Abs(m.transform.position.x) < B.HalfW - 0.2f;

        /// <summary>Nearest on-screen meteor to a point, or null.</summary>
        public Meteor Nearest(Vector2 from)
        {
            Meteor best = null;
            float bestD = float.MaxValue;
            foreach (var m in Active)
            {
                if (!OnScreen(m)) continue;
                float d = ((Vector2)m.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }

        /// <summary>The meteor under a world point (with some padding), or null.</summary>
        public Meteor AtPoint(Vector2 p, float padding)
        {
            Meteor best = null;
            float bestEdge = float.MaxValue;
            foreach (var m in Active)
            {
                float edge = Vector2.Distance(m.transform.position, p) - m.Radius;
                if (edge <= padding && edge < bestEdge) { bestEdge = edge; best = m; }
            }
            return best;
        }
    }
}
