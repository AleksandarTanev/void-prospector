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
    /// The refinery: its HUD button (with progress), the dialog (batch status, odds, collect / sell, target grade)
    /// and the list of cargo stacks that can be loaded. Also ticks the refinery each frame (scaled time).
    /// The dialog polls the session a few times a second while open.
    /// </summary>
    public class RefineryPanel : MonoBehaviour
    {
        const float RedrawSeconds = 0.25f;
        const float RowHeight = 96f;

        static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.18f, 0.98f);
        static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.32f);
        static readonly Color HudReadyColor = new Color(0.65f, 0.5f, 0.15f);
        static readonly Color LoadColor = new Color(0.16f, 0.36f, 0.56f);
        static readonly Color SellColor = new Color(0.18f, 0.5f, 0.3f);
        static readonly Color MutedColor = new Color(0.75f, 0.85f, 1f);
        static readonly Color GoodColor = new Color(0.55f, 0.95f, 0.6f);
        static readonly Color ToastColor = new Color(0.85f, 0.75f, 1f);
        static readonly Color FillColor = new Color(0.7f, 0.55f, 1f);

        class StackRow
        {
            public RectTransform root;
            public TextMeshProUGUI text, loadLabel;
            public Button load;
            public MineralDefinition mineral;
            public int grade;
        }

        GameRoot _root;
        RectTransform _canvas, _list, _fill;
        Button _hudButton, _collect, _sell, _target;
        Image _hudImage;
        TextMeshProUGUI _hudLabel, _batch, _odds, _worth, _targetLabel, _listHeader;
        readonly List<StackRow> _rows = new List<StackRow>();
        readonly List<(MineralDefinition mineral, int grade, int count, GameNumber value)> _stacks =
            new List<(MineralDefinition, int, int, GameNumber)>();
        float _redraw;

        GameSession Session => _root.Session;
        Refinery Refinery => Session.Refinery;
        bool IsOpen => _canvas.gameObject.activeSelf;

        public void Init(GameRoot root, RectTransform hudCanvas, int slot)
        {
            _root = root;
            BuildHudButton(hudCanvas, slot);
            Build();
            _canvas.gameObject.SetActive(false);

            Session.UnlockedSectorRx
                .Subscribe(_ => _hudButton.gameObject.SetActive(Session.RefineryUnlocked))
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
            DrawHudButton();
            if (IsOpen) Redraw();
        }

        // ---------- Actions ----------

        void Open()
        {
            _canvas.gameObject.SetActive(true);
            Redraw();
        }

        void Close() => _canvas.gameObject.SetActive(false);

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

        void DrawHudButton()
        {
            var r = Refinery;
            if (r.IsEmpty) _hudLabel.text = "REFINE";
            else if (r.IsDone) _hudLabel.text = "REFINE <size=24>DONE</size>";
            else
            {
                double cycle = r.CycleSeconds(r.Grade, Session.RefineryStats);
                int pct = cycle > 0 ? Mathf.FloorToInt((float)(r.Progress / cycle) * 100) : 0;
                _hudLabel.text = $"REFINE <size=24>{pct}%</size>";
            }
            _hudImage.color = r.IsDone ? HudReadyColor : ButtonColor;
        }

        void Redraw()
        {
            _redraw = RedrawSeconds;
            DrawHudButton();
            var r = Refinery;
            var purity = Session.Purity;
            var stats = Session.RefineryStats;
            var inv = CultureInfo.InvariantCulture;

            _targetLabel.text = $"Stop at\n<size=26>{purity[r.TargetGrade].name}</size>";

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

        void BuildHudButton(RectTransform hudCanvas, int slot)
        {
            _hudButton = UiFactory.Button(hudCanvas, "Refine", ButtonColor, Open, out _hudLabel, 30);
            _hudLabel.fontStyle = FontStyles.Bold;
            _hudImage = (Image)_hudButton.targetGraphic;
            HudSlot((RectTransform)_hudButton.transform, slot);
        }

        /// <summary>Small buttons down the right edge, under the top bar.</summary>
        public static void HudSlot(RectTransform rt, int slot)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-16, -Hud.TopBarHeight - 16 - slot * 80);
            rt.sizeDelta = new Vector2(220, 68);
        }

        void Build()
        {
            var panel = UiFactory.Modal("Refinery", 92, PanelColor, new Vector2(30, 60), out _canvas);

            var title = UiFactory.Text(panel, "Title", "REFINERY", 60, TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold;
            UiFactory.Strip(title.rectTransform, true, 110);

            // Batch status, under the title.
            var status = UiFactory.Rect("Status", panel);
            status.anchorMin = new Vector2(0, 1);
            status.anchorMax = new Vector2(1, 1);
            status.pivot = new Vector2(0.5f, 1);
            status.offsetMin = new Vector2(30, -530);
            status.offsetMax = new Vector2(-30, -120);

            _batch = UiFactory.Text(status, "Batch", "", 44, TextAlignmentOptions.Left, Color.white);
            UiFactory.PlaceFromBottom(_batch.rectTransform, 0f, 1f, 350, 60, 0);

            _odds = UiFactory.Text(status, "Odds", "", 28, TextAlignmentOptions.TopLeft, MutedColor);
            _odds.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.PlaceFromBottom(_odds.rectTransform, 0f, 1f, 220, 120, 0);

            _worth = UiFactory.Text(status, "Worth", "", 32, TextAlignmentOptions.Left, new Color(1f, 0.9f, 0.5f));
            UiFactory.PlaceFromBottom(_worth.rectTransform, 0f, 1f, 172, 44, 0);

            var bar = UiFactory.Panel("Progress", status, new Color(1, 1, 1, 0.12f)).rectTransform;
            UiFactory.PlaceFromBottom(bar, 0f, 1f, 140, 22, 0);
            _fill = UiFactory.Rect("Fill", bar);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0, 1);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _fill.gameObject.AddComponent<Image>().color = FillColor;

            _target = UiFactory.Button(status, "Target", ButtonColor, OnCycleTarget, out _targetLabel, 30);
            UiFactory.PlaceFromBottom((RectTransform)_target.transform, 0f, 0.333f, 10, 110, 6);
            _collect = UiFactory.Button(status, "Collect", LoadColor, OnCollect, out var collectLabel, 36);
            collectLabel.text = "<b>COLLECT</b>\n<size=24>to cargo</size>";
            UiFactory.PlaceFromBottom((RectTransform)_collect.transform, 0.333f, 0.667f, 10, 110, 6);
            _sell = UiFactory.Button(status, "Sell", SellColor, OnSell, out var sellLabel, 36);
            sellLabel.text = "<b>SELL</b>";
            UiFactory.PlaceFromBottom((RectTransform)_sell.transform, 0.667f, 1f, 10, 110, 6);

            _listHeader = UiFactory.Text(panel, "Cargo Header", "CARGO", 34, TextAlignmentOptions.BottomLeft, MutedColor);
            _listHeader.fontStyle = FontStyles.Bold;
            var lrt = _listHeader.rectTransform;
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.pivot = new Vector2(0.5f, 1);
            lrt.offsetMin = new Vector2(30, -600);
            lrt.offsetMax = new Vector2(-30, -540);

            var close = UiFactory.Button(panel, "Close", ButtonColor, Close, out var closeLabel, 44);
            closeLabel.text = "CLOSE";
            UiFactory.PlaceFromBottom((RectTransform)close.transform, 0.25f, 0.75f, 30, 110, 0);

            _list = UiFactory.ScrollList(panel, new Vector2(24, 160), new Vector2(-24, -610), 10);
        }

        StackRow BuildRow(RectTransform content)
        {
            var rt = UiFactory.ListRow(content, "Stack", RowHeight, RowColor);
            var row = new StackRow { root = rt };
            row.text = UiFactory.Text(rt, "Text", "", 32, TextAlignmentOptions.Left, Color.white);
            row.text.rectTransform.anchorMin = Vector2.zero;
            row.text.rectTransform.anchorMax = new Vector2(0.72f, 1);
            row.text.rectTransform.offsetMin = new Vector2(20, 0);
            row.text.rectTransform.offsetMax = Vector2.zero;
            row.load = UiFactory.Button(rt, "Load", LoadColor, () => OnLoad(row), out row.loadLabel, 30);
            var brt = (RectTransform)row.load.transform;
            brt.anchorMin = new Vector2(0.72f, 0);
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(10, 10);
            brt.offsetMax = new Vector2(-10, -10);
            return row;
        }
    }
}
