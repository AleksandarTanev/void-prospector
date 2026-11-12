using System.Collections.Generic;
using System.Globalization;
using R3;
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
    public class StarMapPanel : MonoBehaviour, ITabPage
    {
        const float RedrawSeconds = 0.25f;
        const float SectorRowHeight = 170f;
        const float MilestoneRowHeight = 92f;

        static readonly Color ToastColor = new Color(1f, 0.8f, 0.4f);

        class SectorRow
        {
            public SectorDefinition sector;
            public TextMeshProUGUI title, status, actionLabel;
            public UiButton action;
        }

        class MilestoneRow
        {
            public MilestoneDefinition milestone;
            public TextMeshProUGUI text;
            public Image icon;
        }

        GameRoot _root;
        RectTransform _canvas;
        readonly List<SectorRow> _sectors = new List<SectorRow>();
        readonly List<MilestoneRow> _milestones = new List<MilestoneRow>();
        readonly ReactiveProperty<bool> _open = new ReactiveProperty<bool>(false);
        float _redraw;

        GameSession Session => _root.Session;
        public bool IsOpen => _open.Value;
        public ReadOnlyReactiveProperty<bool> IsOpenRx => _open;

        public void Init(GameRoot root)
        {
            _root = root;
            _open.AddTo(this);
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
            _open.Value = true;
            Redraw();
        }

        public void Close()
        {
            _canvas.gameObject.SetActive(false);
            _open.Value = false;
        }

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
                SetAction(row, true, "HERE", ButtonStyle.Secondary, false);
            }
            else if (session.IsUnlocked(s))
            {
                row.status.text = $"Depth {depths.Deepest + 1}/{depths.MaxDepth} unlocked";
                SetAction(row, true, "TRAVEL", ButtonStyle.Primary, true);
            }
            else if (s == session.NextSector)
            {
                var state = session.JumpState;
                if (state == JumpState.NeedsDepth)
                {
                    var from = session.FurthestSector;
                    row.status.text = $"Jump drive: unlock depth {from.maxDepth} in {from.displayName} first";
                    SetAction(row, true, "LOCKED", ButtonStyle.Secondary, false);
                }
                else
                {
                    row.status.text = "Jump drive: " + CostText(s);
                    SetAction(row, true, "BUY JUMP DRIVE", ButtonStyle.Gold, state == JumpState.Affordable);
                }
            }
            else
            {
                row.status.text = "Locked";
                SetAction(row, false, "", ButtonStyle.Secondary, false);
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

        static void SetAction(SectorRow row, bool visible, string label, ButtonStyle style, bool interactable)
        {
            if (row.action.gameObject.activeSelf != visible) row.action.gameObject.SetActive(visible);
            row.actionLabel.text = label;
            if (row.action.Style != style) row.action.Style = style;
            row.action.interactable = interactable;
        }

        void DrawMilestone(MilestoneRow row)
        {
            var m = row.milestone;
            var ms = Session.Milestones;
            var inv = CultureInfo.InvariantCulture;
            if (ms.IsDone(m))
            {
                row.text.color = UiTheme.Good;
                row.icon.color = UiTheme.Good;
                row.text.text = $"<b>{m.Title}</b>  DONE\n<size=26>{m.RewardText}</size>";
                return;
            }
            row.icon.color = UiTheme.Faint;
            row.text.color = UiTheme.Text;
            string progress = $"{ms.Progress(m).ToString("N0", inv)}/{m.target.ToString("N0", inv)}";
            row.text.text = $"<b>{m.Title}</b>  <size=28>{progress}</size>\n<size=26><color=#BFD9FF>Reward: {m.RewardText}</color></size>";
        }

        // ---------- Building ----------

        void Build()
        {
            // Above the HUD, below the welcome-back popup.
            var panel = UiFactory.Modal("Star Map", "STAR MAP", 90, true, Close, out _canvas);
            var content = UiFactory.ScrollList(panel, Vector2.zero, Vector2.zero, 10);

            UiFactory.ListHeader(content, "SECTORS");
            foreach (var s in Session.Sectors) _sectors.Add(BuildSectorRow(content, s));

            if (Session.Milestones.All.Count > 0)
            {
                UiFactory.ListHeader(content, "MILESTONES");
                foreach (var m in Session.Milestones.All) _milestones.Add(BuildMilestoneRow(content, m));
            }
        }

        SectorRow BuildSectorRow(RectTransform content, SectorDefinition sector)
        {
            var rt = UiFactory.ListRow(content, sector.id, SectorRowHeight);

            var row = new SectorRow { sector = sector };
            row.title = UiFactory.Text(rt, "Title", "", 40, TextAlignmentOptions.Left, UiTheme.Text);
            UiFactory.PlaceFromBottom(row.title.rectTransform, 0f, 1f, 100, 56, 28);

            row.status = UiFactory.Text(rt, "Status", "", 27, TextAlignmentOptions.Left, UiTheme.Muted);
            row.status.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.PlaceFromBottom(row.status.rectTransform, 0f, 0.6f, 14, 84, 28);

            row.action = UiFactory.Button(rt, "Action", ButtonStyle.Primary, () => OnAction(row), out row.actionLabel, 30);
            UiFactory.PlaceFromBottom((RectTransform)row.action.transform, 0.6f, 1f, 16, 84, 18);
            return row;
        }

        static MilestoneRow BuildMilestoneRow(RectTransform content, MilestoneDefinition m)
        {
            var rt = UiFactory.ListRow(content, m.id, MilestoneRowHeight);
            var icon = UiFactory.Icon(rt, "Icon", UiTheme.Icon("trophy"), UiTheme.Faint);
            UiFactory.Square(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(54, 0), 56);
            var text = UiFactory.Text(rt, "Text", "", 30, TextAlignmentOptions.Left, UiTheme.Text);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(98, 0);
            text.rectTransform.offsetMax = new Vector2(-24, 0);
            return new MilestoneRow { milestone = m, text = text, icon = icon };
        }
    }
}
