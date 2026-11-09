using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.UI
{
    /// <summary>
    /// The Star Map: every sector with its state (here / travel / buy the jump drive / locked) and the milestones.
    /// A modal over the game; it polls the session a few times a second while open, since the counters it shows
    /// change thousands of times per minute.
    /// </summary>
    public class StarMapPanel : MonoBehaviour
    {
        const float RedrawSeconds = 0.25f;
        const float SectorRowHeight = 170f;
        const float MilestoneRowHeight = 84f;

        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
        static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.18f, 0.98f);
        static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color HereColor = new Color(0.25f, 0.28f, 0.36f);
        static readonly Color TravelColor = new Color(0.16f, 0.36f, 0.56f);
        static readonly Color JumpColor = new Color(0.65f, 0.5f, 0.15f);
        static readonly Color CloseColor = new Color(0.22f, 0.24f, 0.32f);
        static readonly Color MutedColor = new Color(0.75f, 0.85f, 1f);
        static readonly Color DoneColor = new Color(0.55f, 0.95f, 0.6f);
        static readonly Color ToastColor = new Color(1f, 0.8f, 0.4f);

        class SectorRow
        {
            public SectorDefinition sector;
            public TextMeshProUGUI title, status, actionLabel;
            public Button action;
            public Image actionImage;
        }

        class MilestoneRow
        {
            public MilestoneDefinition milestone;
            public TextMeshProUGUI text;
        }

        GameRoot _root;
        RectTransform _canvas;
        readonly List<SectorRow> _sectors = new List<SectorRow>();
        readonly List<MilestoneRow> _milestones = new List<MilestoneRow>();
        float _redraw;

        GameSession Session => _root.Session;
        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            Build();
            _canvas.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        public void Open()
        {
            _canvas.gameObject.SetActive(true);
            Redraw();
        }

        public void Close() => _canvas.gameObject.SetActive(false);

        void Update()
        {
            if (!IsOpen) return;
            _redraw -= Time.unscaledDeltaTime;
            if (_redraw <= 0) Redraw();
        }

        // ---------- Actions ----------

        void OnAction(SectorRow row)
        {
            var s = row.sector;
            if (s == Session.Sector) return;
            if (Session.IsUnlocked(s))
            {
                _root.TravelTo(s);
                return;
            }
            if (s != Session.NextSector || !Session.TryBuyJumpDrive())
            {
                _root.Audio.Deny();
                return;
            }
            _root.Audio.Rare(3);
            _root.Fx.Toast($"{s.displayName} unlocked! Travel from the map", ToastColor);
            _root.Fx.Burst(_root.Ship.position, ToastColor, 40, 7f, 0.22f);
            Redraw();
        }

        // ---------- Drawing ----------

        void Redraw()
        {
            _redraw = RedrawSeconds;
            foreach (var row in _sectors) DrawSector(row);
            foreach (var row in _milestones) DrawMilestone(row);
        }

        void DrawSector(SectorRow row)
        {
            var s = row.sector;
            var session = Session;
            var depths = session.DepthsOf(s);
            row.title.text = $"<b>{s.index}. {s.displayName}</b>  <size=26>{Minerals(s)}</size>";

            if (s == session.Sector)
            {
                row.status.text = $"You are here · depth {depths.Deepest + 1}/{depths.MaxDepth} unlocked";
                SetAction(row, true, "HERE", HereColor, false);
            }
            else if (session.IsUnlocked(s))
            {
                row.status.text = $"Depth {depths.Deepest + 1}/{depths.MaxDepth} unlocked";
                SetAction(row, true, "TRAVEL", TravelColor, true);
            }
            else if (s == session.NextSector)
            {
                var state = session.JumpState;
                if (state == JumpState.NeedsDepth)
                {
                    var from = session.FurthestSector;
                    row.status.text = $"Jump drive: unlock depth {from.maxDepth} in {from.displayName} first";
                    SetAction(row, true, "LOCKED", HereColor, false);
                }
                else
                {
                    row.status.text = "Jump drive: " + CostText(s);
                    SetAction(row, true, "BUY JUMP DRIVE", JumpColor, state == JumpState.Affordable);
                }
            }
            else
            {
                row.status.text = "Locked";
                SetAction(row, false, "", HereColor, false);
            }
        }

        string CostText(SectorDefinition s)
        {
            var parts = new List<string>();
            var credits = s.UnlockCredits;
            if (credits > GameNumber.Zero)
                parts.Add(Have(Session.Wallet.Credits >= credits, $"$ {NumberFormatter.Format(credits)}"));
            foreach (var c in s.unlockCost)
            {
                if (c == null || c.mineral == null) continue;
                int have = Session.Cargo.CountOf(c.mineral);
                parts.Add(Have(have >= (int)c.amount, $"{have}/{(int)c.amount} {c.mineral.displayName} in the hold"));
            }
            return string.Join(" + ", parts);
        }

        static string Have(bool ok, string text) => ok ? $"<color=#8CF29A>{text}</color>" : text;

        static string Minerals(SectorDefinition s)
        {
            var names = new List<string>();
            foreach (var t in s.meteorTypes)
            {
                if (t?.type == null || t.weight <= 0) continue;
                foreach (var c in t.type.composition)
                    if (c?.mineral != null && c.weight > 0 && !names.Contains(c.mineral.displayName)) names.Add(c.mineral.displayName);
            }
            return string.Join(", ", names);
        }

        static void SetAction(SectorRow row, bool visible, string label, Color color, bool interactable)
        {
            if (row.action.gameObject.activeSelf != visible) row.action.gameObject.SetActive(visible);
            row.actionLabel.text = label;
            row.actionImage.color = color;
            row.action.interactable = interactable;
        }

        void DrawMilestone(MilestoneRow row)
        {
            var m = row.milestone;
            var ms = Session.Milestones;
            var inv = CultureInfo.InvariantCulture;
            if (ms.IsDone(m))
            {
                row.text.color = DoneColor;
                row.text.text = $"<b>{m.Title}</b>  DONE\n<size=26>{m.RewardText}</size>";
                return;
            }
            row.text.color = Color.white;
            string progress = $"{ms.Progress(m).ToString("N0", inv)}/{m.target.ToString("N0", inv)}";
            row.text.text = $"<b>{m.Title}</b>  <size=28>{progress}</size>\n<size=26><color=#BFD9FF>Reward: {m.RewardText}</color></size>";
        }

        // ---------- Building ----------

        void Build()
        {
            _canvas = UiFactory.Canvas("Star Map");
            _canvas.GetComponent<Canvas>().sortingOrder = 90;   // above the HUD, below the welcome-back popup

            var dim = UiFactory.Panel("Dim", _canvas, DimColor);   // swallows taps meant for meteors
            UiFactory.Stretch(dim.rectTransform);

            var panel = UiFactory.Panel("Panel", _canvas, PanelColor).rectTransform;
            panel.anchorMin = new Vector2(0, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.offsetMin = new Vector2(30, 60);
            panel.offsetMax = new Vector2(-30, -60);

            var title = UiFactory.Text(panel, "Title", "STAR MAP", 60, TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold;
            var trt = title.rectTransform;
            UiFactory.Strip(trt, true, 110);

            var close = UiFactory.Button(panel, "Close", CloseColor, Close, out var closeLabel, 44);
            closeLabel.text = "CLOSE";
            UiFactory.PlaceFromBottom((RectTransform)close.transform, 0.25f, 0.75f, 30, 110, 0);

            // Scrolling list between the title and the close button.
            var view = UiFactory.Rect("Scroll View", panel);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(24, 160);
            view.offsetMax = new Vector2(-24, -120);
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);   // receives drags
            view.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.Rect("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            Header(content, "SECTORS");
            foreach (var s in Session.Sectors) _sectors.Add(BuildSectorRow(content, s));

            if (Session.Milestones.All.Count > 0)
            {
                Header(content, "MILESTONES");
                foreach (var m in Session.Milestones.All) _milestones.Add(BuildMilestoneRow(content, m));
            }
        }

        static void Header(RectTransform content, string text)
        {
            var t = UiFactory.Text(content, text, text, 34, TextAlignmentOptions.BottomLeft, MutedColor);
            t.fontStyle = FontStyles.Bold;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
        }

        SectorRow BuildSectorRow(RectTransform content, SectorDefinition sector)
        {
            var bg = UiFactory.Panel(sector.id, content, RowColor);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = SectorRowHeight;
            var rt = bg.rectTransform;

            var row = new SectorRow { sector = sector };
            row.title = UiFactory.Text(rt, "Title", "", 40, TextAlignmentOptions.Left, Color.white);
            UiFactory.PlaceFromBottom(row.title.rectTransform, 0f, 1f, 100, 56, 24);

            row.status = UiFactory.Text(rt, "Status", "", 28, TextAlignmentOptions.Left, MutedColor);
            row.status.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.PlaceFromBottom(row.status.rectTransform, 0f, 0.6f, 12, 84, 24);

            row.action = UiFactory.Button(rt, "Action", TravelColor, () => OnAction(row), out row.actionLabel, 32);
            row.actionLabel.fontStyle = FontStyles.Bold;
            row.actionImage = (Image)row.action.targetGraphic;
            UiFactory.PlaceFromBottom((RectTransform)row.action.transform, 0.6f, 1f, 14, 84, 16);
            return row;
        }

        static MilestoneRow BuildMilestoneRow(RectTransform content, MilestoneDefinition m)
        {
            var bg = UiFactory.Panel(m.id, content, RowColor);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = MilestoneRowHeight;
            var text = UiFactory.Text(bg.rectTransform, "Text", "", 32, TextAlignmentOptions.Left, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24, 0);
            text.rectTransform.offsetMax = new Vector2(-24, 0);
            return new MilestoneRow { milestone = m, text = text };
        }
    }
}
