using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.UI;

namespace VoidProspector.Fx
{
    /// <summary>Juice: particle bursts, screen shake, hit-stop, damage numbers and toasts.</summary>
    public class GameFx : MonoBehaviour
    {
        class Popup
        {
            public TextMeshProUGUI text;
            public Vector3 world;
            public float age;
            public float life;
            public Color color;
        }

        Camera _cam;
        Vector3 _camBase;
        ParticleSystem _ps;
        float _shake;
        float _hitStopUntil;
        float _lastCargoFull = -10f;

        RectTransform _layer;
        ToastView _toast;
        readonly List<Popup> _active = new List<Popup>();
        readonly Stack<Popup> _pool = new Stack<Popup>();

        public void Init(Camera cam, Material particleMaterial, RectTransform popupLayer, ToastView toast)
        {
            _cam = cam;
            _camBase = cam.transform.position;
            _layer = popupLayer;
            _toast = toast;
            CreateParticles(particleMaterial);
        }

        /// <summary>Normal game speed; the debug panel raises it.</summary>
        public float BaseTimeScale { get; set; } = 1f;

        void OnDestroy() => Time.timeScale = 1f;

        void CreateParticles(Material material)
        {
            var go = new GameObject("FX Particles");
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            var emission = _ps.emission;
            emission.enabled = false;
            var shape = _ps.shape;
            shape.enabled = false;

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;

            var size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));

            var limit = _ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f;
            limit.limit = 0f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.sortingOrder = 30;

            _ps.Play();
        }

        // ---------- Particles, shake, hit-stop ----------

        public void Burst(Vector3 pos, Color color, int count, float speed, float size)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = new Vector3(pos.x, pos.y, 0);
                ep.velocity = Random.insideUnitCircle.normalized * speed * Random.Range(0.3f, 1f);
                ep.startColor = color;
                ep.startSize = size * Random.Range(0.6f, 1.3f);
                ep.startLifetime = Random.Range(0.35f, 0.75f);
                _ps.Emit(ep, 1);
            }
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        public void HitStop(float seconds) => _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + seconds);

        // ---------- Text ----------

        public void Number(Vector3 world, GameNumber value, bool big, bool crit = false)
        {
            var p = _pool.Count > 0 ? _pool.Pop() : NewPopup();
            p.world = world;
            p.age = 0;
            p.life = big ? 0.8f : 0.5f;
            p.color = crit ? new Color(1f, 0.4f, 0.3f) : big ? Color.white : new Color(1f, 0.85f, 0.6f, 0.8f);
            p.text.text = crit ? NumberFormatter.Format(value) + "!" : NumberFormatter.Format(value);
            p.text.fontSize = crit ? 58 : big ? 46 : 30;
            p.text.gameObject.SetActive(true);
            _active.Add(p);
        }

        public void Toast(string message, Color color) => _toast.Show(message, color);

        /// <summary>Rate-limited "cargo full" toast. Returns true when it was shown.</summary>
        public bool CargoFull()
        {
            if (Time.unscaledTime - _lastCargoFull < 2f) return false;
            _lastCargoFull = Time.unscaledTime;
            Toast("Cargo full — Sell!", new Color(1f, 0.55f, 0.45f));
            return true;
        }

        Popup NewPopup()
        {
            var t = UiFactory.Text(_layer, "Popup", "", 36, TextAlignmentOptions.Center, Color.white);
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.sizeDelta = new Vector2(300, 80);
            UiFactory.AddOutline(t);
            return new Popup { text = t };
        }

        void Update()
        {
            Time.timeScale = Time.unscaledTime < _hitStopUntil ? 0.05f : BaseTimeScale;

            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.text.gameObject.SetActive(false);
                    _active.RemoveAt(i);
                    _pool.Push(p);
                    continue;
                }
                p.world += Vector3.up * 1.4f * dt;
                Vector2 screen = _cam.WorldToScreenPoint(p.world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var local);
                p.text.rectTransform.anchoredPosition = local;
                var c = p.color;
                c.a *= 1f - Mathf.Pow(p.age / p.life, 2);
                p.text.color = c;
            }
        }

        void LateUpdate()
        {
            _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 2.5f);
            _cam.transform.position = _camBase + (Vector3)(Random.insideUnitCircle * _shake * 0.3f);
        }
    }
}
