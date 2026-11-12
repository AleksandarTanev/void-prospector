using UnityEngine;
using VoidProspector.Data;
using VoidProspector.Fx;

namespace VoidProspector.Mining
{
    /// <summary>A loose mineral chunk. Moved by <see cref="ChunkField"/>; tinted and glowing by purity grade.</summary>
    public class Chunk : MonoBehaviour
    {
        public enum TickResult { Moving, ReachedShip, Faded }

        const float GlowScale = 3.2f;
        const float PickupDistance = 0.35f;

        public ChunkData Data { get; private set; }
        public Color Tint { get; private set; }

        SpriteRenderer _sr, _glow;
        MeshRenderer _gem;
        Vector3 _spinAxis;
        float _scale;
        Vector2 _vel;
        float _age, _glowAlpha, _fade;
        bool _magnet, _rejected;

        /// <param name="lowPoly">A spinning gem mesh instead of the circle sprite (the glow stays a sprite).</param>
        public void Build(bool lowPoly = false)
        {
            if (lowPoly) _gem = LowPolyMeshes.NewRenderer("Gem", LowPolyMeshes.Gem, transform);
            else
            {
                _sr = gameObject.AddComponent<SpriteRenderer>();
                _sr.sprite = ShapeSprites.Circle;
                _sr.sortingOrder = 6;
            }
            _glow = ShapeSprites.NewRenderer("Glow", ShapeSprites.Glow, transform, 5);
            _glow.transform.localScale = Vector3.one * GlowScale;
        }

        public void Launch(Vector2 pos, ChunkData data, PurityGrade grade, Vector2 velocity)
        {
            Data = data;
            _vel = velocity;
            _age = 0;
            _fade = 1;
            _magnet = _rejected = false;

            // Crude looks dull (grade tint is grey); each grade above Standard gets a bit brighter.
            var c = data.mineral.color * grade.tint;
            if (data.grade > 1) c = Color.Lerp(c, Color.white, 0.12f * (data.grade - 1));
            c.a = 1;
            Tint = c;
            if (_gem != null)
            {
                // A little self-glow so dark minerals still read; more for higher grades.
                LowPolyMeshes.SetColor(_gem, c, 0.3f + 0.1f * data.grade);
                _gem.transform.localRotation = Random.rotationUniform;
                _spinAxis = Random.onUnitSphere;
            }
            else _sr.color = c;

            _glowAlpha = Mathf.Clamp01(grade.glow * 0.7f);
            _glow.enabled = grade.glow > 0;
            _glow.color = new Color(c.r, c.g, c.b, _glowAlpha);

            transform.position = pos;
            _scale = 0.2f + 0.03f * data.grade;
            if (_gem != null) _scale *= 1.7f;   // a gem covers much less than a full circle
            transform.localScale = Vector3.one * _scale;
            gameObject.SetActive(true);
        }

        /// <summary>Cargo was full: bounce away and fade out.</summary>
        public void Reject()
        {
            _magnet = false;
            _rejected = true;
            _vel = new Vector2(Random.Range(-2f, 2f), 3f);
        }

        public TickResult Tick(float dt, Vector2 ship, float collectorRadius, MiningSettings s)
        {
            _age += dt;
            Vector2 p = transform.position;
            float d = Vector2.Distance(p, ship);
            bool settled = _age > s.chunkSettleTime;

            if (_rejected)
            {
                _vel *= Mathf.Exp(-2.2f * dt);
                _fade -= dt / s.rejectedFadeTime;
            }
            else if (settled)
            {
                if (!_magnet && d <= collectorRadius) _magnet = true;
                Vector2 dir = (ship - p).normalized;
                Vector2 want = _magnet ? dir * s.magnetSpeed * (1f + _age * 0.5f) : dir * s.driftToShip;
                float pull = _magnet ? 8f : 2.2f;
                _vel = Vector2.Lerp(_vel, want, 1f - Mathf.Exp(-pull * dt));
            }
            else _vel *= Mathf.Exp(-2.2f * dt);   // the burst slows down

            p += _vel * dt;
            transform.position = p;

            float a = Mathf.Clamp01(_fade);
            if (_gem != null)
            {
                // Opaque mesh: fading shrinks it instead.
                _gem.transform.Rotate(_spinAxis, 200f * dt, Space.World);
                transform.localScale = Vector3.one * (_scale * a);
            }
            else _sr.color = new Color(Tint.r, Tint.g, Tint.b, a);
            if (_glow.enabled)
            {
                _glow.transform.localScale = Vector3.one * GlowScale * (1f + 0.15f * Mathf.Sin(_age * 8f));
                _glow.color = new Color(Tint.r, Tint.g, Tint.b, _glowAlpha * a);
            }

            if (_rejected) return _fade <= 0 ? TickResult.Faded : TickResult.Moving;
            return _magnet && d < PickupDistance ? TickResult.ReachedShip : TickResult.Moving;
        }
    }
}
