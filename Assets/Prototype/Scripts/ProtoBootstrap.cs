using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VoidProspector.Prototype
{
    /// <summary>
    /// The only object in Prototype.unity. Builds the whole prototype at runtime and owns the tuning config.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ProtoBootstrap : MonoBehaviour
    {
        public static ProtoBootstrap I { get; private set; }

        [Tooltip("All tuning values. Safe to edit during Play mode.")]
        public ProtoConfig config = new ProtoConfig();

        [Header("Scene")]
        public float cameraSize = 8f;
        public Color background = new Color(0.03f, 0.04f, 0.09f);
        public Color shipColor = new Color(0.55f, 0.85f, 1f);

        public Camera Cam { get; private set; }
        public Transform Ship { get; private set; }
        public float HalfH => Cam.orthographicSize;
        public float HalfW => Cam.orthographicSize * Cam.aspect;

        /// <summary>The 2D renderer's default sprite material (used for lines).</summary>
        public static Material SpriteMaterial { get; private set; }
        /// <summary>Sprite material with the generated circle texture (used for particles).</summary>
        public static Material ParticleMaterial { get; private set; }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;   // keep simulating when the editor isn't focused

            SetupCamera();
            SetupLight();
            CreateStarfield();
            CreateShip();

            // Order matters: later systems use earlier ones in their Awake.
            gameObject.AddComponent<ProtoGame>();
            gameObject.AddComponent<ProtoAudio>();
            gameObject.AddComponent<ProtoUI>();
            gameObject.AddComponent<ProtoFx>();
            gameObject.AddComponent<MeteorSpawner>();
            gameObject.AddComponent<TapInput>();
            Ship.gameObject.AddComponent<ShipWeapons>();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                Cam = new GameObject("Main Camera").AddComponent<Camera>();
                Cam.tag = "MainCamera";
            }
            Cam.orthographic = true;
            Cam.orthographicSize = cameraSize;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = background;
            Cam.transform.position = new Vector3(0, 0, -10);
            if (FindAnyObjectByType<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
        }

        static void SetupLight()
        {
            foreach (var l in FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global) return;

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
        }

        void CreateStarfield()
        {
            var root = new GameObject("Starfield").transform;
            var rng = new System.Random(7);
            for (int i = 0; i < 80; i++)
            {
                var star = NewSprite("Star", ProtoSprites.Circle, root, -100);
                float x = (float)(rng.NextDouble() * 2 - 1) * HalfW * 1.1f;
                float y = (float)(rng.NextDouble() * 2 - 1) * HalfH * 1.05f;
                star.transform.position = new Vector3(x, y, 0);
                star.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.09f, (float)rng.NextDouble());
                star.color = new Color(0.8f, 0.85f, 1f, Mathf.Lerp(0.15f, 0.6f, (float)rng.NextDouble()));
            }
        }

        void CreateShip()
        {
            var hull = NewSprite("Ship", ProtoSprites.Circle, null, 10);
            hull.color = shipColor;
            Ship = hull.transform;
            // Sit just above the bottom UI panel (600 of 1920 reference pixels).
            float panelWorld = 2f * HalfH * ProtoUI.BottomPanelHeight / 1920f;
            Ship.position = new Vector3(0, -HalfH + panelWorld + 1.0f, 0);
            Ship.localScale = Vector3.one * 0.9f;

            SpriteMaterial = hull.sharedMaterial;
            ParticleMaterial = new Material(SpriteMaterial) { name = "Proto Particles", mainTexture = ProtoSprites.Circle.texture };

            var nose = NewSprite("Drill", ProtoSprites.Square, Ship, 11);
            nose.color = new Color(0.85f, 0.95f, 1f);
            nose.transform.localPosition = new Vector3(0, 0.55f, 0);
            nose.transform.localScale = new Vector3(0.22f, 0.45f, 1);
        }

        /// <summary>Creates a GameObject with a SpriteRenderer.</summary>
        public static SpriteRenderer NewSprite(string name, Sprite sprite, Transform parent, int order)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
