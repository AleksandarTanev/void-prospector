using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.UI
{
    /// <summary>
    /// The Mineral Codex: a grid of minerals × purity grades. Filled entries show their value bonus. Also toasts
    /// new entries of a good grade, and owns its HUD button.
    /// </summary>
    public class CodexPanel : MonoBehaviour
    {
        const float RowHeight = 76f;
        const int ToastFromGrade = 1;

        static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.18f, 0.98f);
        static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.32f);
        static readonly Color EmptyCell = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color MutedColor = new Color(0.75f, 0.85f, 1f);
        static readonly Color ToastColor = new Color(0.75f, 0.95f, 1f);

        class Cell
        {
            public Image image;
            public TextMeshProUGUI text;
        }

        class MineralRow
        {
            public MineralDefinition mineral;
            public TextMeshProUGUI name;
            public Cell[] cells;
        }

        GameRoot _root;
        RectTransform _canvas;
        TextMeshProUGUI _summary;
        readonly List<MineralRow> _rows = new List<MineralRow>();

        GameSession Session => _root.Session;

        public void Init(GameRoot root, RectTransform hudCanvas, int slot)
        {
            _root = root;
            var button = UiFactory.Button(hudCanvas, "Codex", ButtonColor, Open, out var label, 30);
            label.text = "<b>CODEX</b>";
            RefineryPanel.HudSlot((RectTransform)button.transform, slot);
            Build();
            _canvas.gameObject.SetActive(false);

            Session.Codex.Discovered.Subscribe(OnDiscovered).AddTo(this);
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        void Open()
        {
            _canvas.gameObject.SetActive(true);
            Redraw();
        }

        void Close() => _canvas.gameObject.SetActive(false);

        void OnDiscovered((MineralDefinition mineral, int grade) entry)
        {
            if (entry.grade < ToastFromGrade) return;
            var bonus = Session.Codex.BonusFor(entry.grade);
            _root.Fx.Toast($"New in the Codex: {Session.Purity[entry.grade].name} {entry.mineral.displayName}\n" +
                           $"<size=38>+{(bonus * 100).ToString("0", CultureInfo.InvariantCulture)}% {entry.mineral.displayName} value</size>", ToastColor);
            if (_canvas.gameObject.activeSelf) Redraw();
        }

        void Redraw()
        {
            var codex = Session.Codex;
            var purity = Session.Purity;
            var inv = CultureInfo.InvariantCulture;
            int total = _rows.Count * purity.Count;
            _summary.text = $"Entries {codex.Count}/{total}. Each one raises that mineral's value for good.";

            foreach (var row in _rows)
            {
                bool any = false;
                for (int g = 0; g < row.cells.Length; g++)
                {
                    var cell = row.cells[g];
                    bool has = codex.Has(row.mineral, g);
                    any |= has;
                    var tint = purity[g].tint;
                    cell.image.color = has ? new Color(tint.r, tint.g, tint.b, 0.55f) : EmptyCell;
                    cell.text.text = has ? "+" + (codex.BonusFor(g) * 100).ToString("0", inv) + "%" : "—";
                    cell.text.color = has ? Color.white : new Color(1, 1, 1, 0.3f);
                }
                double mult = codex.ValueMultiplier(row.mineral);
                string bonus = mult > 1 ? $"  <size=24>+{((mult - 1) * 100).ToString("0", inv)}%</size>" : "";
                row.name.text = any
                    ? $"<color=#{UiFactory.Hex(row.mineral.color)}><b>{row.mineral.displayName}</b></color>{bonus}"
                    : $"<color=#808080>{row.mineral.displayName}</color>";
            }
        }

        void Build()
        {
            var panel = UiFactory.Modal("Codex", 91, PanelColor, new Vector2(30, 60), out _canvas);

            var title = UiFactory.Text(panel, "Title", "MINERAL CODEX", 60, TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold;
            UiFactory.Strip(title.rectTransform, true, 110);

            _summary = UiFactory.Text(panel, "Summary", "", 28, TextAlignmentOptions.Center, MutedColor);
            var srt = _summary.rectTransform;
            UiFactory.Strip(srt, true, 50);
            srt.offsetMin += new Vector2(0, -110);
            srt.offsetMax += new Vector2(0, -110);

            var close = UiFactory.Button(panel, "Close", ButtonColor, Close, out var closeLabel, 44);
            closeLabel.text = "CLOSE";
            UiFactory.PlaceFromBottom((RectTransform)close.transform, 0.25f, 0.75f, 30, 110, 0);

            var content = UiFactory.ScrollList(panel, new Vector2(24, 160), new Vector2(-24, -180), 8);

            // Header row: grade names.
            var purity = Session.Purity;
            var header = UiFactory.ListRow(content, "Grades", 56, new Color(0, 0, 0, 0));
            for (int g = 0; g < purity.Count; g++)
            {
                var t = UiFactory.Text(header, purity[g].name, purity[g].name, 24, TextAlignmentOptions.Center, MutedColor);
                PlaceColumn(t.rectTransform, g, purity.Count);
            }

            var minerals = Session.Database.minerals.Where(m => m != null).OrderBy(m => m.tier).ThenBy(m => m.baseValue);
            foreach (var m in minerals) _rows.Add(BuildRow(content, m, purity.Count));
        }

        static MineralRow BuildRow(RectTransform content, MineralDefinition mineral, int grades)
        {
            var rt = UiFactory.ListRow(content, mineral.id, RowHeight, RowColor);
            var row = new MineralRow { mineral = mineral, cells = new Cell[grades] };
            row.name = UiFactory.Text(rt, "Name", mineral.displayName, 32, TextAlignmentOptions.Left, Color.white);
            row.name.rectTransform.anchorMin = Vector2.zero;
            row.name.rectTransform.anchorMax = new Vector2(0.3f, 1);
            row.name.rectTransform.offsetMin = new Vector2(16, 0);
            row.name.rectTransform.offsetMax = Vector2.zero;
            for (int g = 0; g < grades; g++)
            {
                var img = UiFactory.Panel("Grade " + g, rt, EmptyCell);
                PlaceColumn(img.rectTransform, g, grades);
                img.rectTransform.offsetMin += new Vector2(0, 8);
                img.rectTransform.offsetMax += new Vector2(0, -8);
                var text = UiFactory.Text(img.rectTransform, "Text", "—", 26, TextAlignmentOptions.Center, Color.white);
                UiFactory.Stretch(text.rectTransform);
                row.cells[g] = new Cell { image = img, text = text };
            }
            return row;
        }

        /// <summary>Grade columns share the right 70% of a row.</summary>
        static void PlaceColumn(RectTransform rt, int index, int count)
        {
            float w = 0.7f / count;
            rt.anchorMin = new Vector2(0.3f + w * index, 0);
            rt.anchorMax = new Vector2(0.3f + w * (index + 1), 1);
            rt.offsetMin = new Vector2(4, 0);
            rt.offsetMax = new Vector2(-4, 0);
        }
    }
}
