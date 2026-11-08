using System.Collections.Generic;
using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>Auto-fire laser from the ship's nose, plus orbiting drones. Also draws the collector ring.</summary>
    public class ShipWeapons : MonoBehaviour
    {
        class Drone
        {
            public Transform t;
            public LineRenderer line;
            public float timer;
            public float flash;
        }

        LineRenderer _laser;
        float _laserFlash;
        float _fireTimer;
        float _orbit;
        SpriteRenderer _ring;
        readonly List<Drone> _drones = new List<Drone>();

        static ProtoConfig C => ProtoBootstrap.I.config;

        Vector3 Nose => transform.position + Vector3.up * 0.75f;

        void Awake()
        {
            _laser = MakeLine("Laser", 0.08f, new Color(0.5f, 0.9f, 1f));

            _ring = ProtoBootstrap.NewSprite("Collector Ring", ProtoSprites.Ring, null, -50);
            _ring.color = new Color(0.5f, 0.85f, 1f, 0.12f);
        }

        LineRenderer MakeLine(string name, float width, Color color)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = ProtoBootstrap.SpriteMaterial;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width * 0.5f;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = 15;
            lr.enabled = false;
            return lr;
        }

        void Update()
        {
            var game = ProtoGame.I;
            float dt = Time.deltaTime;

            // Collector ring follows the ship and shows the pickup radius.
            _ring.transform.position = transform.position;
            _ring.transform.localScale = Vector3.one * game.CollectorRadius * 2f;

            // Auto-fire laser.
            float interval = game.AutoFireInterval;
            if (interval > 0)
            {
                _fireTimer -= dt;
                if (_fireTimer <= 0)
                {
                    var target = MeteorSpawner.I.Nearest(Nose);
                    if (target != null)
                    {
                        Fire(_laser, Nose, target, game.DrillDamage);
                        _laserFlash = 0.07f;
                        _fireTimer = interval;
                    }
                    else _fireTimer = 0.1f;
                }
            }
            _laserFlash -= dt;
            _laser.enabled = _laserFlash > 0;

            // Drones.
            while (_drones.Count < game.DroneCount) AddDrone();
            _orbit += 45f * dt;
            for (int i = 0; i < _drones.Count; i++)
            {
                var d = _drones[i];
                float angle = (_orbit + i * 360f / _drones.Count) * Mathf.Deg2Rad;
                Vector3 centre = transform.position + Vector3.up * C.droneOrbitHeight;
                d.t.position = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * C.droneOrbitRadius;

                d.timer -= dt;
                if (d.timer <= 0)
                {
                    var target = MeteorSpawner.I.Nearest(d.t.position);
                    if (target != null)
                    {
                        Fire(d.line, d.t.position, target, game.DrillDamage * C.droneDamageFactor);
                        d.flash = 0.06f;
                        d.timer = C.droneInterval;
                    }
                    else d.timer = 0.15f;
                }
                d.flash -= dt;
                d.line.enabled = d.flash > 0;
            }
        }

        void AddDrone()
        {
            var sr = ProtoBootstrap.NewSprite("Drone", ProtoSprites.Circle, null, 10);
            sr.color = new Color(1f, 0.75f, 0.35f);
            sr.transform.localScale = Vector3.one * 0.32f;
            _drones.Add(new Drone
            {
                t = sr.transform,
                line = MakeLine("Drone Beam", 0.05f, new Color(1f, 0.8f, 0.4f)),
                timer = Random.Range(0f, C.droneInterval),
            });
        }

        static void Fire(LineRenderer line, Vector3 from, Meteor target, float damage)
        {
            line.SetPosition(0, from);
            line.SetPosition(1, target.transform.position);
            target.Hit(damage, false);
        }
    }
}
