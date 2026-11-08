using System.Collections.Generic;
using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>A drifting meteor. Pooled by MeteorSpawner.</summary>
    public class Meteor : MonoBehaviour
    {
        public float Radius { get; private set; }
        public bool Active { get; private set; }

        SpriteRenderer _body;
        readonly List<SpriteRenderer> _cracks = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
        readonly List<int> _minerals = new List<int>();   // pre-rolled chunk contents

        MeteorSpawner _owner;
        int _sizeIndex;
        float _hp, _maxHp;
        Vector2 _vel;
        float _spin;
        int _stage;
        float _flash;
        float _punch;
        Color _baseColor;

        static ProtoConfig C => ProtoBootstrap.I.config;

        public void Init(MeteorSpawner owner)
        {
            _owner = owner;
            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = ProtoSprites.Circle;
            _body.sortingOrder = 0;

            for (int i = 0; i < 4; i++)
            {
                var crack = ProtoBootstrap.NewSprite("Crack", ProtoSprites.Square, transform, 2);
                crack.color = new Color(0f, 0f, 0f, 0.6f);
                crack.gameObject.SetActive(false);
                _cracks.Add(crack);
            }
            for (int i = 0; i < 3; i++)
            {
                var dot = ProtoBootstrap.NewSprite("Vein", ProtoSprites.Circle, transform, 1);
                dot.gameObject.SetActive(false);
                _dots.Add(dot);
            }
        }

        public void Launch(int sizeIndex, Vector2 pos, Vector2 velocity)
        {
            var def = C.sizes[sizeIndex];
            _sizeIndex = sizeIndex;
            Radius = def.radius;
            _hp = _maxHp = def.hp;
            _vel = velocity;
            _spin = Random.Range(-35f, 35f);
            _stage = 0;
            _flash = 0;
            _punch = 0;

            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            transform.localScale = Vector3.one * Radius * 2f;

            float shade = Random.Range(0.32f, 0.45f);
            _baseColor = new Color(shade + 0.03f, shade, shade - 0.02f);
            _body.color = _baseColor;

            _minerals.Clear();
            for (int i = 0; i < def.chunks; i++) _minerals.Add(ProtoGame.RollMineral());

            foreach (var c in _cracks) c.gameObject.SetActive(false);

            // Visual tells: coloured veins for the non-Rock minerals inside.
            int dotIndex = 0;
            var shown = new HashSet<int>();
            foreach (int m in _minerals)
            {
                if (m == 0 || shown.Contains(m) || dotIndex >= _dots.Count) continue;
                shown.Add(m);
                var dot = _dots[dotIndex++];
                dot.color = C.minerals[m].color;
                dot.transform.localPosition = Random.insideUnitCircle * 0.3f;
                dot.transform.localScale = Vector3.one * Random.Range(0.12f, 0.2f);
                dot.gameObject.SetActive(true);
            }
            for (int i = dotIndex; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);

            Active = true;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            Active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 p = transform.position;
            p += _vel * dt;

            // Bounce inside the vertical band so meteors stay in the field.
            if ((p.y < _owner.MinY + Radius && _vel.y < 0) || (p.y > _owner.MaxY - Radius && _vel.y > 0))
                _vel.y = -_vel.y;

            transform.position = p;
            transform.Rotate(0, 0, _spin * dt);

            _punch = Mathf.MoveTowards(_punch, 0, dt * 1.2f);
            transform.localScale = Vector3.one * Radius * 2f * (1f + _punch);

            if (_flash > 0)
            {
                _flash -= dt;
                _body.color = Color.Lerp(_baseColor, Color.white, Mathf.Clamp01(_flash / 0.08f) * 0.8f);
            }
            else _body.color = _baseColor;

            if (_owner.IsOutside(this)) _owner.Release(this);
        }

        public void Hit(float damage, bool fromTap)
        {
            if (!Active) return;

            _hp -= damage;
            _flash = 0.08f;
            _punch = fromTap ? 0.12f : 0.05f;

            Vector3 pos = transform.position;
            ProtoFx.I.Number(pos + (Vector3)(Random.insideUnitCircle * Radius * 0.4f), damage, fromTap);
            ProtoFx.I.Burst(pos, Color.Lerp(_baseColor, Color.white, 0.4f), fromTap ? 6 : 2, 3f, 0.12f);
            ProtoAudio.I.Hit(fromTap);

            int total = C.sizes[_sizeIndex].chunks;
            if (_stage == 0 && _hp <= _maxHp * 0.66f) { _stage = 1; Crack(total / 4); }
            if (_stage == 1 && _hp <= _maxHp * 0.33f) { _stage = 2; Crack(total / 4); }
            if (_hp <= 0) Break();
        }

        void Crack(int chunks)
        {
            for (int i = (_stage - 1) * 2; i < _stage * 2 && i < _cracks.Count; i++)
            {
                var c = _cracks[i];
                c.transform.localPosition = Random.insideUnitCircle * 0.15f;
                c.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 180f));
                c.transform.localScale = new Vector3(0.05f, Random.Range(0.45f, 0.7f), 1);
                c.gameObject.SetActive(true);
            }
            Spill(chunks, false);
            ProtoAudio.I.Crack(Radius);
            ProtoFx.I.Shake(0.25f * Radius);
            ProtoFx.I.Burst(transform.position, _baseColor, 8, 3.5f, 0.18f);
        }

        void Break()
        {
            bool isLargest = _sizeIndex == C.sizes.Length - 1;
            Spill(_minerals.Count, isLargest);
            ProtoFx.I.Burst(transform.position, Color.Lerp(_baseColor, Color.white, 0.2f), 14 + 10 * _sizeIndex, 5f, 0.25f);
            ProtoFx.I.Shake(0.5f * Radius);
            ProtoAudio.I.Crack(Radius * 1.4f);
            _owner.Release(this);
        }

        void Spill(int count, bool coreBonus)
        {
            Vector2 centre = transform.position;
            for (int i = 0; i < count && _minerals.Count > 0; i++)
            {
                int m = _minerals[_minerals.Count - 1];
                _minerals.RemoveAt(_minerals.Count - 1);
                int bonus = coreBonus && i >= count - C.coreBonusChunks ? 1 : 0;
                int grade = ProtoGame.RollGrade(bonus);
                Vector2 dir = Random.insideUnitCircle.normalized;
                Chunk.Spawn(centre + dir * Radius * 0.6f, m, grade, dir * Random.Range(2.5f, 5f));
            }
        }
    }
}
