using System.Collections.Generic;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Fx;

namespace VoidProspector.Mining
{
    /// <summary>A drifting meteor: visuals, movement and hit reactions. HP and chunk logic live in <see cref="MeteorState"/>.</summary>
    public class Meteor : MonoBehaviour
    {
        const int MaxVeins = 3;
        const int CracksPerStage = 2;

        public float Radius { get; private set; }
        public bool Active { get; private set; }
        public MeteorState State { get; private set; }
        public MeteorSpawn Spawn => _spawn;
        /// <summary>Chunks already spilled; the scanner reads what's left.</summary>
        public int SpilledChunks => _nextChunk;
        /// <summary>Sum of the base values of the minerals still inside (drones' "richest" targeting).</summary>
        public double ContentValue
        {
            get
            {
                double v = 0;
                for (int i = _nextChunk; i < _spawn.minerals.Length; i++) v += _spawn.minerals[i].baseValue;
                return v;
            }
        }

        MeteorField _field;
        SpriteRenderer _body;
        readonly List<SpriteRenderer> _cracks = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _veins = new List<SpriteRenderer>();

        MeteorSpawn _spawn;
        int _nextChunk;
        Vector2 _vel;
        float _spin, _flash, _punch;
        Color _baseColor, _color;

        GameRoot Root => _field.Root;
        MiningSettings S => Root.Session.Mining;

        public void Build(MeteorField field)
        {
            _field = field;
            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = ShapeSprites.Circle;
            _body.sortingOrder = 0;

            for (int i = 0; i < MaxVeins; i++)
            {
                var vein = ShapeSprites.NewRenderer("Vein", ShapeSprites.Circle, transform, 1);
                vein.gameObject.SetActive(false);
                _veins.Add(vein);
            }
        }

        public void Launch(MeteorSpawn spawn, Vector2 pos, Vector2 velocity)
        {
            _spawn = spawn;
            _nextChunk = 0;
            State = new MeteorState(spawn.hp, spawn.minerals.Length, S.crackThresholds, S.crackSpillFraction);

            Radius = spawn.size.radius;
            _vel = velocity;
            _spin = Random.Range(-35f, 35f);
            _flash = _punch = 0;

            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            transform.localScale = Vector3.one * Radius * 2f;

            // The type's colour with a little per-meteor variation, so Metallic and Stony meteors read differently.
            float shade = Random.Range(0.85f, 1.15f);
            var tint = spawn.type.bodyColor;
            _baseColor = _color = new Color(tint.r * shade, tint.g * shade, tint.b * shade);
            _body.color = _color;

            foreach (var c in _cracks) c.gameObject.SetActive(false);
            ShowVeins();

            Active = true;
            gameObject.SetActive(true);
        }

        /// <summary>Coloured dots hint at the valuable minerals inside (anything above tier 0).</summary>
        void ShowVeins()
        {
            int shown = 0;
            var seen = new HashSet<MineralDefinition>();
            foreach (var m in _spawn.minerals)
            {
                if (shown >= _veins.Count) break;
                if (m.tier == 0 || !seen.Add(m)) continue;
                var vein = _veins[shown++];
                vein.color = m.color;
                vein.transform.localPosition = Random.insideUnitCircle * 0.3f;
                vein.transform.localScale = Vector3.one * Random.Range(0.12f, 0.2f);
                vein.gameObject.SetActive(true);
            }
            for (int i = shown; i < _veins.Count; i++) _veins[i].gameObject.SetActive(false);
        }

        public void Deactivate() => Active = false;

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            Vector2 p = transform.position;
            p += _vel * dt;

            // Bounce inside the vertical band so meteors stay in the field.
            if ((p.y < _field.MinY + Radius && _vel.y < 0) || (p.y > _field.MaxY - Radius && _vel.y > 0))
                _vel.y = -_vel.y;

            transform.position = p;
            transform.Rotate(0, 0, _spin * dt);

            _punch = Mathf.MoveTowards(_punch, 0, dt * 1.2f);
            transform.localScale = Vector3.one * Radius * 2f * (1f + _punch);

            if (_flash > 0) _flash -= dt;
            _body.color = Color.Lerp(_color, Color.white, Mathf.Clamp01(_flash / 0.08f) * 0.8f);

            if (_field.IsOutside(this)) _field.Release(this);
        }

        public void Hit(HitRoll roll, bool fromTap, bool quiet = false)
        {
            if (!Active) return;
            var result = State.Hit(roll.damage);

            _flash = 0.08f;
            _punch = fromTap ? 0.12f : 0.05f;
            Vector3 pos = transform.position;
            var fx = Root.Fx;
            fx.Number(pos + (Vector3)(Random.insideUnitCircle * Radius * 0.4f), roll.damage, fromTap, roll.crit);
            fx.Burst(pos, Color.Lerp(_color, Color.white, 0.4f), fromTap ? 6 : 2, 3f, 0.12f);
            if (fromTap) fx.Shake(S.hitShake);
            if (!quiet) Root.Audio.Hit(fromTap);

            if (result.cracks > 0) Crack(result);
            if (result.broke) Break(result);
        }

        void Crack(HitResult result)
        {
            for (int i = 0; i < result.cracks * CracksPerStage; i++) AddCrackLine();
            // Darker with each stage.
            _color = Color.Lerp(_baseColor, Color.black, 0.15f * State.Stage);

            Spill(result.crackChunks, 0);
            if (result.broke) return;   // Break() plays the bigger effect
            Root.Audio.Crack(Radius);
            Root.Fx.Shake(S.crackShake * Radius);
            Root.Fx.Burst(transform.position, _baseColor, 8, 3.5f, 0.18f);
        }

        void AddCrackLine()
        {
            SpriteRenderer line = null;
            foreach (var c in _cracks)
                if (!c.gameObject.activeSelf) { line = c; break; }
            if (line == null)
            {
                line = ShapeSprites.NewRenderer("Crack", ShapeSprites.Square, transform, 2);
                line.color = new Color(0f, 0f, 0f, 0.6f);
                _cracks.Add(line);
            }
            line.transform.localPosition = Random.insideUnitCircle * 0.15f;
            line.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 180f));
            line.transform.localScale = new Vector3(0.05f, Random.Range(0.45f, 0.7f), 1);
            line.gameObject.SetActive(true);
        }

        void Break(HitResult result)
        {
            int core = _spawn.size.hasCore ? S.coreChunks : 0;
            Spill(result.breakChunks, core);
            if (Root.Session.RegisterBreak(_spawn.depth))
                Root.Fx.Toast($"Depth {Root.Session.Depths.Deepest + 1} unlocked!", new Color(0.6f, 0.9f, 1f));

            int sizeBoost = Mathf.RoundToInt(Radius * 10);
            Root.Fx.Burst(transform.position, Color.Lerp(_baseColor, Color.white, 0.2f), 14 + sizeBoost, 5f, 0.25f);
            Root.Fx.Shake(S.breakShake * Radius);
            Root.Audio.Crack(Radius * 1.4f);
            _field.Release(this);
        }

        /// <summary>Spills the next <paramref name="count"/> chunks; the last <paramref name="coreCount"/> roll one grade higher.</summary>
        void Spill(int count, int coreCount)
        {
            Vector2 centre = transform.position;
            var session = Root.Session;
            for (int i = 0; i < count && _nextChunk < _spawn.minerals.Length; i++)
            {
                var mineral = _spawn.minerals[_nextChunk++];
                bool fromCore = i >= count - coreCount;
                var chunk = session.RollChunk(mineral, _spawn.depth, fromCore);
                Vector2 dir = Random.insideUnitCircle.normalized;
                float speed = Random.Range(S.chunkBurstSpeed.x, S.chunkBurstSpeed.y);
                Root.Chunks.Spawn(centre + dir * Radius * 0.6f, chunk, dir * speed);
            }
        }
    }
}
