using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoidProspector.Analytics;
using VoidProspector.Data;
using VoidProspector.Drones;
using VoidProspector.Fx;
using VoidProspector.Idle;
using VoidProspector.Mining;
using VoidProspector.UI;

namespace VoidProspector.Core
{
    /// <summary>
    /// The one object in Game.unity. Validates the data, loads the save into a new session and builds the greybox
    /// world for the current sector: camera, starfield, ship, drones, HUD, meteor field and chunks. Then starts
    /// saving and pays offline earnings. Travelling to another sector saves and reloads the scene.
    /// Balance numbers come from the GameDatabase only.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [Tooltip("Empty = the sector with index 1")]
        [SerializeField] SectorDefinition startingSector;
        [Tooltip("Random seed for meteor and chunk rolls; 0 = different every run")]
        [SerializeField] int seed;
        [Tooltip("Save file name inside Application.persistentDataPath")]
        [SerializeField] string saveFileName = "save.json";

        [Header("Greybox look")]
        [SerializeField] float cameraSize = 8f;
        [Tooltip("Used when the sector's background colour is fully transparent")]
        [SerializeField] Color background = new Color(0.03f, 0.04f, 0.09f);
        [SerializeField] Color shipColor = new Color(0.55f, 0.85f, 1f);

        public GameSession Session { get; private set; }
        public Camera Cam { get; private set; }
        public Transform Ship { get; private set; }
        public float HalfH => Cam.orthographicSize;
        public float HalfW => Cam.orthographicSize * Cam.aspect;

        public GameAudio Audio { get; private set; }
        public GameFx Fx { get; private set; }
        public Hud Hud { get; private set; }
        public ChunkField Chunks { get; private set; }
        public MeteorField Meteors { get; private set; }
        public DroneSwarm Drones { get; private set; }
        public SaveController Saves { get; private set; }
        public OfflineController Offline { get; private set; }
        public AnalyticsController Analytics { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            Application.runInBackground = true;   // keep simulating while the editor isn't focused
#endif
            if (!ValidateData()) { enabled = false; return; }

            var store = new SaveStore(Path.Combine(Application.persistentDataPath, saveFileName));
            var save = LoadSave(store);
            var sector = SaveSystem.SectorOf(save, database)
                ?? (startingSector ? startingSector : database.sectors.Where(s => s != null).OrderBy(s => s.index).First());
            Session = new GameSession(database, sector, seed != 0 ? seed : System.Environment.TickCount);
            if (save != null)
            {
                var warnings = new List<string>();
                SaveSystem.Apply(Session, save, warnings);
                if (warnings.Count > 0) Debug.LogWarning("Save loaded with changes:\n" + string.Join("\n", warnings));
            }

            SetupCamera();
            CreateStarfield();
            var shipRenderer = CreateShip();
            var particleMaterial = new Material(shipRenderer.sharedMaterial) { name = "FX Particles", mainTexture = ShapeSprites.Circle.texture };

            Audio = gameObject.AddComponent<GameAudio>();
            Hud = gameObject.AddComponent<Hud>();
            Fx = gameObject.AddComponent<GameFx>();
            Chunks = gameObject.AddComponent<ChunkField>();
            Meteors = gameObject.AddComponent<MeteorField>();
            var input = gameObject.AddComponent<TapInput>();
            var autoFire = gameObject.AddComponent<AutoFire>();
            Drones = gameObject.AddComponent<DroneSwarm>();

            Hud.Init(this);
            Fx.Init(Cam, particleMaterial, Hud.PopupLayer, Hud.Toast);
            Chunks.Init(this);
            Meteors.Init(this);
            input.Init(this);
            autoFire.Init(this, shipRenderer.sharedMaterial);
            Drones.Init(this, shipRenderer.sharedMaterial);

            Saves = gameObject.AddComponent<SaveController>();
            Saves.Init(this, store);
            Analytics = gameObject.AddComponent<AnalyticsController>();
            Analytics.Init(this, Path.Combine(Application.persistentDataPath, "analytics.jsonl"));
            Offline = gameObject.AddComponent<OfflineController>();
            Offline.Init(this, save != null ? save.SavedAtUtc : (System.DateTime?)null);
        }

        /// <summary>Moves the ship to another unlocked sector: saves, then rebuilds the world there.</summary>
        public bool TravelTo(SectorDefinition sector)
        {
            if (!Session.IsUnlocked(sector) || sector == Session.Sector) return false;
            Analytics.Game.Travel(sector.id);
            if (!Session.TravelTo(sector)) return false;
            Saves.SaveAndReload();
            return true;
        }

        static SaveData LoadSave(SaveStore store)
        {
            if (!store.Exists) return null;
            var problems = new List<string>();
            var save = store.Load(problems);
            if (problems.Count > 0)
                Debug.LogWarning($"Save problems ({store.FilePath}):\n" + string.Join("\n", problems)
                    + (save == null ? "\nStarting a new game." : "\nUsed the backup."));
            return save;
        }

        bool ValidateData()
        {
            if (database == null)
            {
                Debug.LogError("GameRoot: no GameDatabase assigned", this);
                return false;
            }
            var errors = new List<string>();
            if (database.Validate(errors)) return true;
            Debug.LogError("GameRoot: the GameDatabase has errors:\n" + string.Join("\n", errors), database);
            return false;
        }

        /// <summary>Converts a height in reference UI pixels (1920 tall) to world units.</summary>
        public float UiToWorld(float uiPixels) => 2f * HalfH * uiPixels / UiFactory.ReferenceResolution.y;

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
            var sectorColor = Session.Sector.backgroundColor;
            Cam.backgroundColor = sectorColor.a > 0 ? sectorColor : background;
            Cam.transform.position = new Vector3(0, 0, -10);
            if (FindAnyObjectByType<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
        }

        void CreateStarfield()
        {
            var root = new GameObject("Starfield").transform;
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            for (int i = 0; i < 80; i++)
            {
                var star = ShapeSprites.NewRenderer("Star", ShapeSprites.Circle, root, -100);
                star.transform.position = new Vector3((R() * 2 - 1) * HalfW * 1.1f, (R() * 2 - 1) * HalfH * 1.05f, 0);
                star.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.09f, R());
                star.color = new Color(0.8f, 0.85f, 1f, Mathf.Lerp(0.15f, 0.6f, R()));
            }
        }

        SpriteRenderer CreateShip()
        {
            var hull = ShapeSprites.NewRenderer("Ship", ShapeSprites.Circle, null, 10);
            hull.color = shipColor;
            Ship = hull.transform;
            // Just above the bottom panel; the space around the ship stays clear of UI (prototype lesson).
            Ship.position = new Vector3(0, -HalfH + UiToWorld(Hud.BottomPanelHeight) + 1.0f, 0);
            Ship.localScale = Vector3.one * 0.9f;

            var drill = ShapeSprites.NewRenderer("Drill", ShapeSprites.Square, Ship, 11);
            drill.color = new Color(0.85f, 0.95f, 1f);
            drill.transform.localPosition = new Vector3(0, 0.55f, 0);
            drill.transform.localScale = new Vector3(0.22f, 0.45f, 1);
            return hull;
        }
    }
}
