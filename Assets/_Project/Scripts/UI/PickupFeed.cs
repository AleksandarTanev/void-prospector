using R3;
using TMPro;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.UI
{
    /// <summary>
    /// Short "+5 Copper" lines above the bottom panel as chunks reach the ship. Pickups of the same mineral merge into
    /// one line while it shows; auto-sold pickups show what they earned. Up to three lines, newest at the bottom.
    /// Moved and faded by hand in unscaled time (it changes many times a second; no tweens).
    /// </summary>
    public class PickupFeed : MonoBehaviour
    {
        const int Lines = 3;
        const float LineHeight = 44f;
        const float ShowSeconds = 1.6f;
        const float FadeSeconds = 0.4f;
        const float PunchSeconds = 0.15f;

        class Line
        {
            public TextMeshProUGUI text;
            public CanvasGroup group;   // fades the whole line, colour tags included
            public MineralDefinition mineral;
            public bool sold;
            public int count;
            public GameNumber value;
            public float age, punch;
            public bool Active => mineral != null;
        }

        readonly Line[] _lines = new Line[Lines];   // index 0 = newest (bottom)
        RectTransform _area;

        public void Init(GameSession session, RectTransform layer)
        {
            _area = UiFactory.Rect("Pickup Feed", layer);
            _area.anchorMin = _area.anchorMax = Vector2.zero;
            _area.pivot = Vector2.zero;
            _area.anchoredPosition = new Vector2(40, Hud.BottomPanelHeight + 16);
            _area.sizeDelta = new Vector2(600, LineHeight * Lines);

            for (int i = 0; i < Lines; i++)
            {
                var t = UiFactory.Text(_area, "Line", "", 34, TextAlignmentOptions.BottomLeft, Color.white);
                t.fontStyle = FontStyles.Bold;
                UiFactory.AddOutline(t);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.sizeDelta = new Vector2(600, LineHeight);
                var group = t.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                t.gameObject.SetActive(false);
                _lines[i] = new Line { text = t, group = group };
            }

            session.Collected.Subscribe(c => Add(c.chunk.mineral, c.result == CollectResult.AutoSold, c.chunk.value)).AddTo(this);
        }

        void Add(MineralDefinition mineral, bool sold, GameNumber value)
        {
            if (mineral == null) return;
            foreach (var l in _lines)
            {
                if (l.Active && l.mineral == mineral && l.sold == sold)
                {
                    l.count++;
                    l.value += value;
                    l.age = 0;
                    l.punch = PunchSeconds;
                    Draw(l);
                    return;
                }
            }

            // A new line at the bottom: the others move up and the oldest drops off.
            var reuse = _lines[Lines - 1];
            for (int i = Lines - 1; i > 0; i--) _lines[i] = _lines[i - 1];
            _lines[0] = reuse;
            reuse.mineral = mineral;
            reuse.sold = sold;
            reuse.count = 1;
            reuse.value = value;
            reuse.age = 0;
            reuse.punch = PunchSeconds;
            reuse.text.gameObject.SetActive(true);
            Draw(reuse);
        }

        static void Draw(Line l)
        {
            string name = $"<color=#{UiFactory.Hex(l.mineral.color)}>+{l.count} {l.mineral.displayName}</color>";
            l.text.text = l.sold
                ? $"{name} <size=28><color=#{UiFactory.Hex(UiTheme.Credits)}>sold $ {NumberFormatter.Format(l.value)}</color></size>"
                : name;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            int slot = 0;
            for (int i = 0; i < Lines; i++)
            {
                var l = _lines[i];
                if (!l.Active) continue;
                l.age += dt;
                if (l.age >= ShowSeconds + FadeSeconds)
                {
                    l.mineral = null;
                    l.text.gameObject.SetActive(false);
                    continue;
                }
                l.group.alpha = l.age <= ShowSeconds ? 1f : 1f - (l.age - ShowSeconds) / FadeSeconds;
                l.punch = Mathf.Max(0, l.punch - dt);
                l.text.rectTransform.localScale = Vector3.one * (1f + 0.15f * (l.punch / PunchSeconds));
                l.text.rectTransform.anchoredPosition = new Vector2(0, slot * LineHeight);
                slot++;
            }
        }
    }
}
