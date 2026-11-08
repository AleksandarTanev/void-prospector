using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>Procedurally generated sprites, so the prototype needs no image files.</summary>
    public static class ProtoSprites
    {
        const int Size = 128;

        static Sprite _circle;
        static Sprite _glow;
        static Sprite _square;

        /// <summary>Solid anti-aliased circle, 1 world unit in diameter.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(CircleAlpha, "ProtoCircle"));

        /// <summary>Soft radial glow, 1 world unit in diameter.</summary>
        public static Sprite Glow => _glow != null ? _glow : (_glow = Make(GlowAlpha, "ProtoGlow"));

        static Sprite _ring;

        /// <summary>Thin ring, 1 world unit in diameter.</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = Make(RingAlpha, "ProtoRing"));

        static float RingAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.97f) * Size * 0.5f / 2f);
        }

        /// <summary>Plain white square, 1 world unit.</summary>
        public static Sprite Square => _square != null ? _square : (_square = Make((x, y) => 1f, "ProtoSquare"));

        static float CircleAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);           // 0 at centre, 1 at edge
            return Mathf.Clamp01((1f - d) * Size * 0.5f);  // ~1px soft edge
        }

        static float GlowAlpha(float x, float y)
        {
            float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            float a = 1f - d;
            return a * a;
        }

        static Sprite Make(System.Func<float, float, float> alpha, string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(alpha(nx, ny)) * 255f);
                px[y * Size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }
    }
}
