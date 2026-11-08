using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidProspector.Prototype
{
    /// <summary>Juice: particles, screen shake, damage numbers, toasts, hit-stop and game speed.</summary>
    public class ProtoFx : MonoBehaviour
    {
        public static ProtoFx I { get; private set; }

        /// <summary>Normal game speed (the debug speed button changes it).</summary>
        public float BaseTimeScale { get; set; } = 1f;

        class Popup
        {
            public Text text;
            public Vector3 world;
            public float age;
            public float life;
            public Color color;
        }

        ParticleSystem _ps;
        float _shake;
        Vector3 _camBase;
        float _hitStopUntil;
        float _lastCargoFull = -10f;

        RectTransform _layer;
        Text _toast;
        float _toastAge = 99f;
        Color _toastColor;
        readonly List<Popup> _active = new List<Popup>();
        readonly Stack<Popup> _pool = new Stack<Popup>();

        void Awake()
        {
            I = this;
            _camBase = ProtoBootstrap.I.Cam.transform.position;
            _layer = ProtoUI.I.PopupLayer;
            _toast = ProtoUI.I.ToastText;
            CreateParticles();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        void CreateParticles()
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
            r.sharedMaterial = ProtoBootstrap.ParticleMaterial;
            r.sortingOrder = 30;

            _ps.Play();
        }

        // ---------- Particles ----------

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

        // ---------- Shake and hit-stop ----------

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        public void HitStop(float seconds) => _hitStopUntil = Time.unscaledTime + seconds;

        // ---------- Text ----------

        public void Number(Vector3 world, float value, bool big)
        {
            var p = _pool.Count > 0 ? _pool.Pop() : NewPopup();
            p.world = world;
            p.age = 0;
            p.life = big ? 0.8f : 0.5f;
            p.color = big ? Color.white : new Color(1f, 0.85f, 0.6f, 0.8f);
            p.text.text = ProtoGame.Fmt(value);
            p.text.fontSize = big ? 46 : 30;
            p.text.gameObject.SetActive(true);
            _active.Add(p);
        }

        public void Toast(string message, Color color)
        {
            _toast.text = message;
            _toastColor = color;
            _toastAge = 0;
        }

        public void CargoFull()
        {
            if (Time.unscaledTime - _lastCargoFull < 2f) return;
            _lastCargoFull = Time.unscaledTime;
            Toast("Cargo full — Sell!", new Color(1f, 0.55f, 0.45f));
            ProtoAudio.I.Deny();
        }

        Popup NewPopup()
        {
            var t = ProtoUI.MakeText(_layer, "Popup", "", 36, TextAnchor.MiddleCenter, Color.white);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.sizeDelta = new Vector2(300, 80);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            return new Popup { text = t };
        }

        void Update()
        {
            Time.timeScale = Time.unscaledTime < _hitStopUntil ? 0.05f : BaseTimeScale;

            // Popups float up and fade.
            var cam = ProtoBootstrap.I.Cam;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.age += Time.unscaledDeltaTime;
                if (p.age >= p.life)
                {
                    p.text.gameObject.SetActive(false);
                    _active.RemoveAt(i);
                    _pool.Push(p);
                    continue;
                }
                p.world += Vector3.up * 1.4f * Time.unscaledDeltaTime;
                Vector2 screen = cam.WorldToScreenPoint(p.world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var local);
                p.text.rectTransform.anchoredPosition = local;
                var c = p.color;
                c.a *= 1f - Mathf.Pow(p.age / p.life, 2);
                p.text.color = c;
            }

            // Toast fades after 1.6 s.
            _toastAge += Time.unscaledDeltaTime;
            float ta = Mathf.Clamp01(2f - _toastAge / 0.8f);
            _toast.color = new Color(_toastColor.r, _toastColor.g, _toastColor.b, ta);
        }

        void LateUpdate()
        {
            var camT = ProtoBootstrap.I.Cam.transform;
            _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 2.5f);
            camT.position = _camBase + (Vector3)(Random.insideUnitCircle * _shake * 0.3f);
        }
    }
}
