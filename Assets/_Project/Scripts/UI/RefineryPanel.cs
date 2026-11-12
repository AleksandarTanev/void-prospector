using System.Collections.Generic;
using System.Globalization;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Refining;

namespace VoidProspector.UI
{
    /// <summary>
    /// The refinery: its tab (locked until the refinery's sector, then showing progress), the dialog (batch status,
    /// odds, collect / sell, target grade) and the list of cargo stacks that can be loaded. Also ticks the refinery
    /// each frame (scaled time). The dialog polls the session a few times a second while open.
    /// </summary>
    public class RefineryPanel : MonoBehaviour, ITabPage
    {
        const float RedrawSeconds = 0.25f;
        const float RowHeight = 100f;

        static readonly Color GoodColor = new Color(0.55f, 0.95f, 0.6f);
        static readonly Color ToastColor = new Color(0.85f, 0.75f, 1f);
        static readonly Color FillColor = new Color(0.7f, 0.55f, 1f);

        class StackRow
        {
            public RectTransform root;
            public TextMeshProUGUI text, loadLabel;
            public Image icon;
            public UiButton load;
            public MineralDefinition mineral;
            public int grade;
        }

        GameRoot _root;
        TabBar.Tab _tab;
        RectTransform _canvas, _list, _fill;
        UiButton _collect, _sell, _target;
        TextMeshProUGUI _batch, _odds, _worth, _targetLabel, _listHeader;
        Image _batchIcon;
        readonly List<StackRow> _rows = new List<StackRow>();
        readonly List<(MineralDefinition mineral, int grade, int count, GameNumber value)> _stacks =
            new List<(MineralDefinition, int, int, GameNumber)>();
        readonly ReactiveProperty<bool> _open = new ReactiveProperty<bool>(false);
        float _redraw;

        GameSession Session => _root.Session;
        Refinery Refinery => Session.Refinery;
        bool IsOpen => _open.Value;
        public ReadOnlyReactiveProperty<bool> IsOpenRx => _open;

        public void Init(GameRoot root, TabBar.Tab tab)
        {
            _root = root;
            _tab = tab;
            _open.AddTo(this);
            Build();
            _canvas.gameObject.SetActive(false);

            int unlockSector = Session.Database.refinery != null ? Session.Database.refinery.unlockSector : 3;
            _tab.onLocked = () =>
            {
                _root.Audio.Deny();
                _root.Fx.Toast($"The refinery opens in Sector {unlockSector}", UiTheme.Muted);
            };
            Session.UnlockedSectorRx
                .Subscribe(_ =>
                {
                    _tab.locked = !Session.RefineryUnlocked;
                    _tab.SetIcon(UiTheme.Icon(_tab.locked ? "lock" : "refinery"));
                    DrawTab();
                })
                .AddTo(this);
            Refinery.Stepped.Subscribe(OnStep).AddTo(this);
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        void Update()
        {
            Session.TickRefinery(Time.deltaTime);
            _redraw -= Time.unscaledDeltaTime;
            if (_redraw > 0) return;
            _redraw = RedrawSeconds;
            DrawTab();
            if (IsOpen) Redraw();
        }

        // ---------- Actions ----------

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

        void OnLoad(StackRow row)
        {
            if (!Session.TryLoadRefinery(row.mineral, row.grade)) { _root.Audio.Deny(); return; }
            _root.Audio.Pickup(2);
            Redraw();
        }

        void OnCollect()
        {
            if (!Session.CollectRefinery())
            {
                _root.Audio.Deny();
                _root.Fx.Toast("Not enough room in the cargo hold", new Color(1f, 0.55f, 0.45f));
                return;
            }
            _root.Audio.Pickup(3);
            Redraw();
        }

        void OnSell()
        {
            var value = Session.SellRefinery();
            if (value <= GameNumber.Zero) { _root.Audio.Deny(); return; }
            _root.Audio.Sell();
            _root.Fx.Toast($"+$ {NumberFormatter.Format(value)}", new Color(1f, 0.9f, 0.5f));
            Redraw();
        }

        /// <summary>Stop at: Standard → Refined → Pristine → Flawless → Standard ...</summary>
        void OnCycleTarget()
        {
            int next = Refinery.TargetGrade + 1;
            if (next > Refinery.TopGrade) next = Mathf.Max(1, Refinery.IsEmpty ? 1 : Refinery.Grade + 1);
            Refinery.TargetGrade = next;
            _root.Saves?.RequestSave();
            Redraw();
        }

        void OnStep(RefineStep step)
        {
            var purity = Session.Purity;
            if (step.success)
            {
                _root.Audio.Rare(step.grade);
                _root.Fx.Toast(step.finished
                    ? $"Refinery done: {Refinery.Count} {purity[step.grade].name} {Refinery.Mineral.displayName}"
                    : $"Refined to {purity[step.grade].name}!", ToastColor);
            }
            else if (step.lost > 0)
            {
                _root.Audio.Deny();
                _root.Fx.Toast(Refinery.IsEmpty ? "Refining failed: the batch is gone" : $"Refining failed: {step.lost} lost", new Color(1f, 0.6f, 0.5f));
            }
            _root.Saves?.RequestSave();
        }

        // ---------- Drawing ----------

        /// <summary>The tab's label shows progress; it turns gold when a batch is done.</summary>
        void DrawTab()
        {
            var r = Refinery;
            if (_tab.locked || r.IsEmpty) _tab.label.text = "Refinery";
            else if (r.IsDone) _tab.label.text = "DONE";
            else
            {
                double cycle = r.CycleSeconds(r.Grade, Session.RefineryStats);
                int pct = cycle > 0 ? Mathf.FloorToInt((float)(r.Progress / cycle) * 100) : 0;
                _tab.label.text = $"Refining {pct}%";
            }
            _tab.SetAlert(!_tab.locked && r.IsDone);
        }

        void Redraw()
        {
            _redraw = RedrawSeconds;
            DrawTab();
            var r = Refinery;
            var purity = Session.Purity;
            var stats = Session.RefineryStats;
            var inv = CultureInfo.InvariantCulture;

            _targetLabel.text = $"Stop at\n<size=26>{purity[r.TargetGrade].name}</size>";

            _batchIcon.sprite = r.IsEmpty ? UiTheme.Icon("refinery") : UiTheme.Mineral(r.Mineral.id);
            _batchIcon.color = r.IsEmpty ? UiTheme.Faint : Color.white;
            _batchIcon.enabled = _batchIcon.sprite != null;

            if (r.IsEmpty)
            {
                _batch.text = "<b>Empty</b>";
                _odds.text = $"Load up to {Session.RefineryBatchSize} chunks of one mineral and grade from the cargo hold.";
                _worth.text = "";
                _fill.anchorMax = new Vector2(0, 1);
            }
            else
            {
                string mineral = $"<color=#{UiFactory.Hex(r.Mineral.color)}>{r.Mineral.displayName}</color>";
                _batch.text = $"<b>{r.Count} × {purity[r.Grade].name} {mineral}</b>";
                _worth.text = $"Worth $ {NumberFormatter.Format(r.Value)} now";
                if (r.IsDone)
                {
                    _odds.text = $"<color=#{UiFactory.Hex(GoodColor)}>Done</color>: reached {purity[r.Grade].name}. Collect it or sell it.";
                    _fill.anchorMax = Vector2.one;
                }
                else
                {
                    double p = r.SuccessChance(r.Grade, stats);
                    double loss = r.FailLoss(r.Grade, stats);
                    double ev = r.ExpectedValueRatio(r.Grade, stats);
                    double cycle = r.CycleSeconds(r.Grade, stats);
                    string evText = ev >= 1
                        ? $"<color=#{UiFactory.Hex(GoodColor)}>×{ev.ToString("0.00", inv)} on average</color>"
                        : $"<color=#FF9A80>×{ev.ToString("0.00", inv)} on average: a gamble</color>";
                    _odds.text = $"→ {purity[r.Grade + 1].name}: {(p * 100).ToString("0", inv)}% chance, {cycle.ToString("0", inv)}s per try; " +
                                 $"a failure loses {(loss * 100).ToString("0", inv)}%. Next try: {evText}";
                    _fill.anchorMax = new Vector2(cycle > 0 ? Mathf.Clamp01((float)(r.Progress / cycle)) : 0, 1);
                }
            }
            _collect.interactable = !r.IsEmpty;
            _sell.interactable = !r.IsEmpty;
            DrawStacks();
        }

        void DrawStacks()
        {
            _stacks.Clear();
            int top = Session.Purity.TopGrade;
            foreach (var st in Session.Cargo.Stacks)
                if (st.grade < top && st.count > 0) _stacks.Add(st);
            _stacks.Sort((a, b) => b.value.CompareTo(a.value));

            _listHeader.text = _stacks.Count == 0 ? "CARGO  <size=26>nothing to refine</size>" : "CARGO";
            bool canLoad = Refinery.IsEmpty;
            int batch = Session.RefineryBatchSize;
            var purity = Session.Purity;
            var stats = Session.RefineryStats;
            var inv = CultureInfo.InvariantCulture;

            for (int i = 0; i < _stacks.Count; i++)
            {
                if (i >= _rows.Count) _rows.Add(BuildRow(_list));
                var row = _rows[i];
                var (mineral, grade, count, value) = _stacks[i];
                if (row.mineral != mineral)
                {
                    row.icon.sprite = UiTheme.Mineral(mineral.id);
                    row.icon.enabled = row.icon.sprite != null;
                }
                row.mineral = mineral;
                row.grade = grade;
                row.root.gameObject.SetActive(true);
                double ev = Refinery.ExpectedValueRatio(grade, stats);
                string evColor = ev >= 1 ? UiFactory.Hex(GoodColor) : "FF9A80";
                row.text.text = $"<b><color=#{UiFactory.Hex(mineral.color)}>{mineral.displayName}</color></b> {purity[grade].name} ×{count}" +
                                $"  <size=26>$ {NumberFormatter.Format(value)}</size>\n" +
                                $"<size=26><color=#{evColor}>→ {purity[grade + 1].name}: ×{ev.ToString("0.00", inv)} average per try</color></size>";
                row.loadLabel.text = $"LOAD {Mathf.Min(batch, count)}";
                row.load.interactable = canLoad;
            }
            for (int i = _stacks.Count; i < _rows.Count; i++) _rows[i].root.gameObject.SetActive(false);
        }

        // ---------- Building ----------

        void Build()
        {
            var panel = UiFactory.Modal("Refinery", "REFINERY", 92, true, Close, out _canvas);

            // Batch status in a card at the top.
            var status = UiFactory.Frame("Status", panel, UiTheme.Card, new Color(1f, 1f, 1f, 0.06f), 0.8f).rectTransform;
            status.anchorMin = new Vector2(0, 1);
            status.anchorMax = new Vector2(1, 1);
            status.pivot = new Vector2(0.5f, 1);
            status.offsetMin = new Vector2(0, -440);
            status.offsetMax = Vector2.zero;

            _batchIcon = UiFactory.Icon(status, "Batch Icon", null, Color.white);
            UiFactory.Square(_batchIcon.rectTransform, Vector2.zero, new Vector2(78, 384), 80);

            _batch = UiFactory.Text(status, "Batch", "", 42, TextAlignmentOptions.Left, UiTheme.Text);
            UiFactory.PlaceFromBottom(_batch.rectTransform, 0f, 1f, 356, 60, 30);
            _batch.rectTransform.offsetMin = new Vector2(132, 356);

            _odds = UiFactory.Text(status, "Odds", "", 27, TextAlignmentOptions.TopLeft, UiTheme.Muted);
            _odds.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.PlaceFromBottom(_odds.rectTransform, 0f, 1f, 222, 116, 30);

            _worth = UiFactory.Text(status, "Worth", "", 30, TextAlignmentOptions.Left, UiTheme.Credits);
            UiFactory.PlaceFromBottom(_worth.rectTransform, 0f, 1f, 180, 42, 30);

            var bar = UiFactory.Rect("Progress", status);
            UiFactory.PlaceFromBottom(bar, 0f, 1f, 142, 30, 30);
            _fill = UiFactory.Bar(bar, "Bar", FillColor, out _);
            UiFactory.Stretch((RectTransform)_fill.parent);

            _target = UiFactory.Button(status, "Target", ButtonStyle.Secondary, OnCycleTarget, out _targetLabel, 28);
            UiFactory.PlaceFromBottom((RectTransform)_target.transform, 0f, 0.333f, 24, 104, 0);
            ((RectTransform)_target.transform).offsetMin = new Vector2(24, 24);
            _collect = UiFactory.Button(status, "Collect", ButtonStyle.Primary, OnCollect, out var collectLabel, 34);
            collectLabel.text = "COLLECT\n<size=22>to cargo</size>";
            UiFactory.PlaceFromBottom((RectTransform)_collect.transform, 0.333f, 0.667f, 24, 104, 8);
            _sell = UiFactory.Button(status, "Sell", ButtonStyle.Confirm, OnSell, out var sellLabel, 34);
            sellLabel.text = "SELL";
            UiFactory.PlaceFromBottom((RectTransform)_sell.transform, 0.667f, 1f, 24, 104, 0);
            ((RectTransform)_sell.transform).offsetMax = new Vector2(-24, 128);

            _listHeader = UiFactory.Text(panel, "Cargo Header", "CARGO", 32, TextAlignmentOptions.BottomLeft, UiTheme.Accent);
            _listHeader.fontStyle = FontStyles.Bold;
            _listHeader.characterSpacing = 4;
            var lrt = _listHeader.rectTransform;
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.pivot = new Vector2(0.5f, 1);
            lrt.offsetMin = new Vector2(8, -510);
            lrt.offsetMax = new Vector2(-8, -450);

            _list = UiFactory.ScrollList(panel, Vector2.zero, new Vector2(0, -520), 8);
        }

        StackRow BuildRow(RectTransform content)
        {
            var rt = UiFactory.ListRow(content, "Stack", RowHeight);
            var row = new StackRow { root = rt };
            row.icon = UiFactory.Icon(rt, "Icon", null, Color.white);
            UiFactory.Square(row.icon.rectTransform, new Vector2(0, 0.5f), new Vector2(54, 0), 64);
            row.text = UiFactory.Text(rt, "Text", "", 30, TextAlignmentOptions.Left, UiTheme.Text);
            row.text.rectTransform.anchorMin = Vector2.zero;
            row.text.rectTransform.anchorMax = new Vector2(0.72f, 1);
            row.text.rectTransform.offsetMin = new Vector2(100, 0);
            row.text.rectTransform.offsetMax = Vector2.zero;
            row.load = UiFactory.Button(rt, "Load", ButtonStyle.Primary, () => OnLoad(row), out row.loadLabel, 28);
            var brt = (RectTransform)row.load.transform;
            brt.anchorMin = new Vector2(0.72f, 0);
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(10, 10);
            brt.offsetMax = new Vector2(-10, -10);
            return row;
        }
    }
}
