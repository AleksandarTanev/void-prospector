using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Fx;

namespace VoidProspector.Mining
{
    /// <summary>
    /// The Pulse Laser: once the Auto-fire upgrade is bought, the ship fires at the nearest meteor on a timer.
    /// Also draws the collector's pickup radius as a faint ring around the ship.
    /// </summary>
    public class AutoFire : MonoBehaviour
    {
        static readonly Color LaserColor = new Color(0.5f, 0.9f, 1f);

        GameRoot _root;
        LineRenderer _laser;
        SpriteRenderer _ring;
        float _timer, _flash;

        Vector3 Nose => _root.Ship.position + Vector3.up * 0.75f;

        public void Init(GameRoot root, Material lineMaterial)
        {
            _root = root;

            var go = new GameObject("Pulse Laser");
            _laser = go.AddComponent<LineRenderer>();
            _laser.sharedMaterial = lineMaterial;
            _laser.positionCount = 2;
            _laser.useWorldSpace = true;
            _laser.startWidth = 0.08f;
            _laser.endWidth = 0.04f;
            _laser.startColor = _laser.endColor = LaserColor;
            _laser.sortingOrder = 15;
            _laser.enabled = false;

            _ring = ShapeSprites.NewRenderer("Collector Ring", ShapeSprites.Ring, null, -50);
            _ring.color = new Color(0.5f, 0.85f, 1f, 0.12f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var session = _root.Session;

            _ring.transform.position = _root.Ship.position;
            _ring.transform.localScale = Vector3.one * session.CollectorRadius * 2f;

            float interval = session.AutoFireInterval;
            if (interval > 0)
            {
                _timer -= dt;
                if (_timer <= 0)
                {
                    var target = _root.Meteors.Nearest(Nose);
                    if (target != null)
                    {
                        _laser.SetPosition(0, Nose);
                        _laser.SetPosition(1, target.transform.position);
                        _flash = 0.07f;
                        _timer = interval;
                        target.Hit(session.RollHit(false), false);
                    }
                    else _timer = 0.1f;
                }
            }

            _flash -= dt;
            _laser.enabled = _flash > 0;
        }
    }
}
