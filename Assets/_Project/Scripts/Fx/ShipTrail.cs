using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Fx
{
    /// <summary>Streams engine particles down from the ship's nozzles (through <see cref="GameFx.Trail"/>), so it reads as flying forward.</summary>
    public class ShipTrail : MonoBehaviour
    {
        // Slightly behind the ship so the trail doesn't draw over the hull.
        static readonly Vector3 BehindShip = new Vector3(0, 0, 0.3f);

        GameRoot _root;
        Transform[] _nozzles;
        float[] _pending;

        MiningSettings S => _root.Session.Mining;

        public void Init(GameRoot root, Transform[] nozzles)
        {
            _root = root;
            _nozzles = nozzles ?? new Transform[0];
            _pending = new float[_nozzles.Length];
        }

        void Update()
        {
            if (_root == null) return;
            float dt = Time.deltaTime;
            var s = S;
            for (int i = 0; i < _nozzles.Length; i++)
            {
                var nozzle = _nozzles[i];
                if (nozzle == null) continue;
                _pending[i] += s.trailRate * dt;
                while (_pending[i] >= 1f)
                {
                    _pending[i] -= 1f;
                    var pos = nozzle.position + BehindShip + (Vector3)(Random.insideUnitCircle * 0.04f);
                    var vel = new Vector2(Random.Range(-0.25f, 0.25f), -s.trailSpeed * Random.Range(0.8f, 1.2f));
                    _root.Fx.Trail(pos, vel, s.trailColor, s.trailSize * Random.Range(0.7f, 1.2f),
                        Random.Range(s.trailLife.x, s.trailLife.y));
                }
            }
        }
    }
}
