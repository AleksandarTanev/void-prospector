using System;
using UnityEngine;

namespace VoidProspector.Fx
{
    /// <summary>Generated greybox sprites (circle, glow, square), 1 world unit across. Replaced by real art in Phase 9.</summary>
    public static class ShapeSprites
    {
        const int Size = 128;

        static Sprite _circle, _glow, _square;

        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(CircleAlpha, "Circle"));
        public static Sprite Glow => _glow != null ? _glow : (_glow = Make(GlowAlpha, "Glow"));
        public static Sprite Square => _square != null ? _square : (_square = Make((x, y) => 1f, "Square"));

        static Sprite _ring;
        /// <summary>Thin ring at the edge of the 1-unit circle.</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = Make(RingAlpha, "Ring"));

        static float RingAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.97f) * Size * 0.25f);
        }

        static float CircleAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((1f - d) * Size * 0.5f);   // ~1 px soft edge
        }

        static float GlowAlpha(float x, float y)
        {
            float a = 1f - Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            return a * a;
        }

        static Sprite Make(Func<float, float, float> alpha, string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(nx, ny)) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }

        /// <summary>Creates a GameObject with a SpriteRenderer.</summary>
        public static SpriteRenderer NewRenderer(string name, Sprite sprite, Transform parent, int order)
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
