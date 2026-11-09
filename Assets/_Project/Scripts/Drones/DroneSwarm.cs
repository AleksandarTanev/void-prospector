using System.Collections.Generic;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Fx;
using VoidProspector.Mining;

namespace VoidProspector.Drones
{
    /// <summary>
    /// Draws the drones orbiting the ship and fires them. Timing lives in the session's <see cref="DroneBay"/>
    /// (so the simulator matches); this picks each shot's target with <see cref="DroneTargeting"/> and shows it.
    /// </summary>
    public class DroneSwarm : MonoBehaviour
    {
        static readonly Color DroneColor = new Color(1f, 0.75f, 0.35f);
        static readonly Color BeamColor = new Color(1f, 0.8f, 0.4f);
        const float BeamTime = 0.06f;

        class View
        {
            public SpriteRenderer body;
            public LineRenderer beam;
            public float flash;
        }

        GameRoot _root;
        Material _lineMaterial;
        readonly List<View> _views = new List<View>();
        readonly List<int> _ready = new List<int>();
        readonly List<TargetInfo> _targets = new List<TargetInfo>();
        readonly List<Meteor> _candidates = new List<Meteor>();
        readonly System.Random _rng = new System.Random();
        float _angle;

        public int Count => _root != null ? _root.Session.Drones.Count : 0;

        public void Init(GameRoot root, Material lineMaterial)
        {
            _root = root;
            _lineMaterial = lineMaterial;
        }

        void Update()
        {
            if (_root == null) return;
            var session = _root.Session;
            float dt = Time.deltaTime;
            var s = session.Mining;

            session.TickDrones(dt, _ready);
            EnsureViews(session.Drones.Count);

            _angle += s.droneOrbitSpeed * dt;
            Vector2 ship = _root.Ship.position;
            int n = session.Drones.Count;
            for (int i = 0; i < n; i++)
            {
                var v = _views[i];
                float a = (_angle + 360f * i / n) * Mathf.Deg2Rad;
                v.body.transform.position = ship + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.droneOrbitRadius;
                v.body.transform.rotation = Quaternion.Euler(0, 0, _angle + 360f * i / n);
                v.body.transform.localScale = Vector3.one * s.droneSize;
            }

            if (_ready.Count > 0) FireReady(session);

            for (int i = 0; i < n; i++)
            {
                var v = _views[i];
                v.flash -= dt;
                v.beam.enabled = v.flash > 0;
            }
        }

        void FireReady(GameSession session)
        {
            CollectTargets();
            if (_candidates.Count == 0) return;   // ready drones wait for a meteor
            var rule = session.DroneTargetRule;
            foreach (int i in _ready)
            {
                var v = _views[i];
                Vector2 from = v.body.transform.position;
                int pick = DroneTargeting.Pick(_targets, from.x, from.y, rule, _rng);
                if (pick < 0) break;
                var target = _candidates[pick];
                if (!target.Active) continue;

                target.Hit(session.RollDroneHit(), false, quiet: true);
                session.Drones.Fired(i, session.DroneInterval);
                v.beam.SetPosition(0, from);
                v.beam.SetPosition(1, target.transform.position);
                v.flash = BeamTime;

                if (!target.Active) CollectTargets();   // it broke: pick from what's left
                if (_candidates.Count == 0) break;
            }
        }

        void CollectTargets()
        {
            _candidates.Clear();
            _targets.Clear();
            float halfW = _root.HalfW - 0.2f;
            foreach (var m in _root.Meteors.Active)
            {
                if (!m.Active || Mathf.Abs(m.transform.position.x) > halfW) continue;
                _candidates.Add(m);
                var p = m.transform.position;
                _targets.Add(new TargetInfo(p.x, p.y, (double)m.State.Hp, m.ContentValue));
            }
        }

        void EnsureViews(int count)
        {
            while (_views.Count < count)
            {
                var body = ShapeSprites.NewRenderer("Drone", ShapeSprites.Square, transform, 12);
                body.color = DroneColor;

                var go = new GameObject("Drone Beam");
                go.transform.SetParent(body.transform, false);
                var beam = go.AddComponent<LineRenderer>();
                beam.sharedMaterial = _lineMaterial;
                beam.positionCount = 2;
                beam.useWorldSpace = true;
                beam.startWidth = 0.05f;
                beam.endWidth = 0.025f;
                beam.startColor = beam.endColor = BeamColor;
                beam.sortingOrder = 14;
                beam.enabled = false;
                _views.Add(new View { body = body, beam = beam });
            }
            for (int i = 0; i < _views.Count; i++)
            {
                bool on = i < count;
                if (_views[i].body.gameObject.activeSelf != on) _views[i].body.gameObject.SetActive(on);
            }
        }
    }
}
