using System.Collections.Generic;
using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>A mineral chunk with a purity grade. Flies to the ship when inside the collector radius.</summary>
    public class Chunk : MonoBehaviour
    {
        public static readonly List<Chunk> ActiveList = new List<Chunk>();
        static readonly Stack<Chunk> Pool = new Stack<Chunk>();
        static Transform _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ActiveList.Clear();
            Pool.Clear();
            _root = null;
        }

        SpriteRenderer _sr;
        SpriteRenderer _glow;
        int _mineral, _grade;
        Vector2 _vel;
        float _age;
        bool _magnet;
        bool _rejected;
        float _scale;
        float _glowAlpha;
        Color _color;

        static ProtoConfig C => ProtoBootstrap.I.config;

        public static void Spawn(Vector2 pos, int mineral, int grade, Vector2 velocity)
        {
            if (_root == null) _root = new GameObject("Chunks").transform;
            var c = Pool.Count > 0 ? Pool.Pop() : Create();
            c.Launch(pos, mineral, grade, velocity);
            ActiveList.Add(c);

            if (grade >= C.rareGrade)
            {
                var gd = C.grades[grade];
                ProtoFx.I.Toast($"{gd.name} {C.minerals[mineral].name}!", Color.Lerp(C.minerals[mineral].color, Color.white, 0.5f));
                ProtoFx.I.Burst(pos, Color.white, 20, 4f, 0.18f);
                ProtoAudio.I.Rare(grade);
                if (grade == C.grades.Length - 1) ProtoFx.I.HitStop(0.25f);
            }
        }

        static Chunk Create()
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(_root, false);
            var c = go.AddComponent<Chunk>();
            c._sr = go.AddComponent<SpriteRenderer>();
            c._sr.sprite = ProtoSprites.Circle;
            c._sr.sortingOrder = 6;
            c._glow = ProtoBootstrap.NewSprite("Glow", ProtoSprites.Glow, go.transform, 5);
            c._glow.transform.localScale = Vector3.one * 3.2f;
            return c;
        }

        void Launch(Vector2 pos, int mineral, int grade, Vector2 velocity)
        {
            _mineral = mineral;
            _grade = grade;
            _vel = velocity;
            _age = 0;
            _magnet = false;
            _rejected = false;

            var mc = C.minerals[mineral].color;
            var gd = C.grades[grade];
            _color = grade == 0
                ? Color.Lerp(mc, new Color(0.45f, 0.45f, 0.45f), 0.5f)   // Crude: dull
                : Color.Lerp(mc, Color.white, 0.12f * (grade - 1));
            _sr.color = _color;
            _scale = 0.2f + 0.03f * grade;
            _glowAlpha = Mathf.Clamp01(gd.glow * 0.7f);
            _glow.color = new Color(_color.r, _color.g, _color.b, _glowAlpha);
            _glow.enabled = gd.glow > 0;

            transform.position = pos;
            transform.localScale = Vector3.one * _scale;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            var game = ProtoGame.I;
            Vector2 ship = ProtoBootstrap.I.Ship.position;
            Vector2 p = transform.position;
            float d = Vector2.Distance(p, ship);

            if (!_magnet && !_rejected && _age > 0.35f && d <= game.CollectorRadius) _magnet = true;

            if (_magnet)
            {
                Vector2 dir = (ship - p).normalized;
                Vector2 want = dir * C.magnetSpeed * (1f + _age * 0.5f);
                _vel = Vector2.Lerp(_vel, want, 1f - Mathf.Exp(-8f * dt));
            }
            else if (!_rejected && _age > 0.35f)
            {
                // Outside the collector: settle into a slow drift toward the ship.
                Vector2 drift = (ship - p).normalized * C.chunkDriftToShip;
                _vel = Vector2.Lerp(_vel, drift, 1f - Mathf.Exp(-2.2f * dt));
            }
            else _vel *= Mathf.Exp(-2.2f * dt);

            p += _vel * dt;
            transform.position = p;

            if (_magnet && d < 0.35f)
            {
                Collect();
                return;
            }

            // Fade out and disappear if never collected.
            float life = C.chunkLifetime;
            float a = !_magnet && _age > life - 1f ? Mathf.Clamp01(life - _age) : 1f;
            _sr.color = new Color(_color.r, _color.g, _color.b, a);
            if (_glow.enabled)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(_age * 8f);
                _glow.transform.localScale = Vector3.one * 3.2f * pulse;
                _glow.color = new Color(_color.r, _color.g, _color.b, _glowAlpha * a);
            }
            if (!_magnet && _age >= life) Release();
        }

        void Collect()
        {
            if (ProtoGame.I.AddCargo(_mineral, _grade))
            {
                ProtoAudio.I.Pickup(_grade);
                ProtoFx.I.Burst(ProtoBootstrap.I.Ship.position, _color, 3, 1.5f, 0.1f);
                Release();
            }
            else
            {
                // Cargo full: bounce away and fade.
                _magnet = false;
                _rejected = true;
                _vel = new Vector2(Random.Range(-2f, 2f), 3f);
                _age = Mathf.Max(_age, C.chunkLifetime - 1.2f);
                ProtoFx.I.CargoFull();
            }
        }

        void Release()
        {
            gameObject.SetActive(false);
            ActiveList.Remove(this);
            Pool.Push(this);
        }
    }
}
