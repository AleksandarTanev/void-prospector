using UnityEngine;

namespace VoidProspector.Fx
{
    /// <summary>
    /// The player's ship as a low-poly model built from generated parts: hull, wings, cockpit, two engines with a
    /// warm glow and a spinning drill at the nose. Sways gently so it reads as 3D. Points up (+Y), about 1 unit across.
    /// </summary>
    public class LowPolyShip : MonoBehaviour
    {
        static readonly Color HullColor = new Color(0.78f, 0.82f, 0.9f);
        static readonly Color DarkColor = new Color(0.32f, 0.35f, 0.42f);
        static readonly Color DrillColor = new Color(0.92f, 0.94f, 1f);
        static readonly Color EngineGlow = new Color(1f, 0.6f, 0.25f);

        Transform _model, _drill;
        Renderer[] _engines;
        float _t;

        /// <summary>Adds the model under <paramref name="ship"/>; <paramref name="accent"/> colours the wings and cockpit.</summary>
        public static LowPolyShip Build(Transform ship, Color accent)
        {
            var view = ship.gameObject.AddComponent<LowPolyShip>();
            var model = new GameObject("Model").transform;
            model.SetParent(ship, false);
            view._model = model;

            Part("Hull", LowPolyMeshes.Gem, model, new Vector3(0, 0, 0), new Vector3(0.62f, 1.05f, 0.42f), HullColor);
            Part("Wings", LowPolyMeshes.Gem, model, new Vector3(0, -0.2f, 0.04f), new Vector3(1.75f, 0.42f, 0.16f), accent);
            Part("Cockpit", LowPolyMeshes.Gem, model, new Vector3(0, 0.1f, -0.17f), new Vector3(0.22f, 0.34f, 0.2f), accent, 0.45f);

            view._engines = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.42f : 0.42f;
                var pod = Part("Engine", LowPolyMeshes.Cone(6), model, new Vector3(x, -0.3f, 0.02f), new Vector3(0.24f, 0.36f, 0.24f), DarkColor);
                pod.transform.localRotation = Quaternion.Euler(0, 0, 180f);   // nozzle points back
                view._engines[i] = Part("Engine Glow", LowPolyMeshes.Gem, model, new Vector3(x, -0.52f, 0.02f), new Vector3(0.14f, 0.18f, 0.14f), EngineGlow, 1.2f);
            }

            var drill = Part("Drill", LowPolyMeshes.Cone(6), model, new Vector3(0, 0.66f, 0), new Vector3(0.3f, 0.36f, 0.3f), DrillColor);
            view._drill = drill.transform;
            return view;
        }

        static MeshRenderer Part(string name, Mesh mesh, Transform parent, Vector3 pos, Vector3 scale, Color color, float glow = 0f)
        {
            var r = LowPolyMeshes.NewRenderer(name, mesh, parent);
            r.transform.localPosition = pos;
            r.transform.localScale = scale;
            LowPolyMeshes.SetColor(r, color, glow);
            return r;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            // Slow roll and a little pitch, so the faceted hull catches the light.
            _model.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.7f) * 6f - 10f, Mathf.Sin(_t * 0.9f) * 18f, 0);
            _drill.Rotate(0, 360f * dt, 0, Space.Self);
            float pulse = 1f + 0.18f * Mathf.Sin(_t * 14f);
            foreach (var e in _engines) e.transform.localScale = new Vector3(0.14f, 0.18f * pulse, 0.14f);
        }
    }
}
