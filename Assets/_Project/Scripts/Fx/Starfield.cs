using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Fx
{
    /// <summary>
    /// Background stars in parallax layers that scroll down, so the ship reads as flying forward through the belt.
    /// Stars that leave the bottom come back at the top at a new x. Uses scaled time: hit-stop freezes space too.
    /// </summary>
    public class Starfield : MonoBehaviour
    {
        const float Depth = 5f;   // behind the play plane (z = 0)

        Transform[] _stars;
        float[] _speeds;
        float _halfW, _wrapH;

        public void Init(GameRoot root, MiningSettings s)
        {
            // Generous margins: the tilted low-poly camera sees a slightly different range at z = 5 than at z = 0.
            _halfW = root.HalfW * 1.1f;
            _wrapH = root.HalfH * 1.15f;

            var layers = s.starLayers ?? new StarLayer[0];
            int total = 0;
            foreach (var l in layers) if (l != null) total += l.count;
            _stars = new Transform[total];
            _speeds = new float[total];

            int n = 0;
            for (int li = 0; li < layers.Length; li++)
            {
                var layer = layers[li];
                if (layer == null) continue;
                var group = new GameObject($"Stars {li}").transform;
                group.SetParent(transform, false);
                bool streak = layer.stretch > 1f;
                for (int i = 0; i < layer.count; i++)
                {
                    var star = ShapeSprites.NewRenderer("Star", streak ? ShapeSprites.Glow : ShapeSprites.Circle, group, -100 + li);
                    float size = Random.Range(layer.size.x, layer.size.y);
                    // A glow sprite fades out well before its edge, so streaks get a little extra width.
                    star.transform.localScale = streak ? new Vector3(size * 2f, size * layer.stretch * 2f, 1f) : Vector3.one * size;
                    star.transform.position = new Vector3(Random.Range(-_halfW, _halfW), Random.Range(-_wrapH, _wrapH), Depth);
                    star.color = new Color(0.8f, 0.85f, 1f, Random.Range(layer.alpha.x, layer.alpha.y));
                    _stars[n] = star.transform;
                    // A little speed spread inside a layer so it doesn't move as one sheet.
                    _speeds[n] = layer.speed * Random.Range(0.85f, 1.15f);
                    n++;
                }
            }
        }

        void Update()
        {
            if (_stars == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _stars.Length; i++)
            {
                var t = _stars[i];
                var p = t.position;
                p.y -= _speeds[i] * dt;
                if (p.y < -_wrapH)
                {
                    p.y += 2f * _wrapH;
                    p.x = Random.Range(-_halfW, _halfW);
                }
                t.position = p;
            }
        }
    }
}
