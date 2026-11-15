using System;
using System.Collections.Generic;
using System.Text;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.UI
{
    /// <summary>
    /// What's in the cargo hold: one row per mineral with its count, value and count per purity grade, the minerals
    /// kept for the jump drive, and a Sell button. Opened by tapping the cargo row in the HUD (it has no tab).
    /// Polls the session a few times a second while open, like the other dialogs.
    /// </summary>
    public class CargoPanel : MonoBehaviour, ITabPage
    {
        const float RedrawSeconds = 0.25f;
        const float RowHeight = 116f;

        class Row
        {
            public RectTransform root;
            public Image icon, lockIcon;
            public TextMeshProUGUI text, value;
            public MineralDefinition mineral;
        }

        GameRoot _root;
        UnityAction _onSell;
        Func<string> _sellLabel;
        RectTransform _canvas, _list;
        TextMeshProUGUI _summary, _empty, _sellText;
        UiButton _sell;
        List<MineralDefinition> _minerals;
        readonly List<Row> _rows = new List<Row>();
        readonly StringBuilder _sb = new StringBuilder();
        readonly ReactiveProperty<bool> _open = new ReactiveProperty<bool>(false);
        float _redraw;

        GameSession Session => _root.Session;
        public ReadOnlyReactiveProperty<bool> IsOpenRx => _open;

        /// <param name="onSell">The HUD's Sell action (toast, sound, coins).</param>
        /// <param name="sellLabel">The HUD's Sell label text, so both buttons read the same.</param>
        public void Init(GameRoot root, UnityAction onSell, Func<string> sellLabel)
        {
            _root = root;
            _onSell = onSell;
            _sellLabel = sellLabel;
            _minerals = UiTheme.SortedMinerals(Session.Database);
            _open.AddTo(this);
            Build();
            _canvas.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        void Update()
        {
            if (!_open.Value) return;
            _redraw -= Time.unscaledDeltaTime;
            if (_redraw <= 0) Redraw();
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

        void OnSell()
        {
            _onSell?.Invoke();
            Redraw();
        }

        // ---------- Drawing ----------

        void Redraw()
        {
            _redraw = RedrawSeconds;
            var cargo = Session.Cargo;
            var purity = Session.Purity;
            var reserve = Session.JumpReserve;

            string summary = $"{cargo.Count}/{cargo.Capacity} chunks  ·  $ {NumberFormatter.Format(cargo.Value)}";
            if (reserve.Count > 0)
            {
                _sb.Clear();
                for (int i = 0; i < reserve.Count; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    _sb.Append(reserve[i].count).Append(' ').Append(reserve[i].mineral.displayName);
                }
                summary += $"\n<color=#{UiFactory.Hex(UiTheme.Gold)}>Sell keeps {_sb} for the jump drive</color>";
            }
            _summary.text = summary;

            int shown = 0;
            foreach (var mineral in _minerals)
            {
                int count = cargo.CountOf(mineral);
                if (count <= 0) continue;
                if (shown >= _rows.Count) _rows.Add(BuildRow(_list));
                var row = _rows[shown++];
                if (row.mineral != mineral)
                {
                    row.mineral = mineral;
                    row.icon.sprite = UiTheme.Mineral(mineral.id);
                    row.icon.enabled = row.icon.sprite != null;
                }
                row.root.gameObject.SetActive(true);

                // Count per grade, each grade name in its tint; the stack values add up to the row's value.
                _sb.Clear();
                var value = GameNumber.Zero;
                bool first = true;
                for (int g = 0; g < purity.Count; g++)
                {
                    int n = cargo.CountOf(mineral, g);
                    if (n <= 0) continue;
                    value += cargo.ValueOf(mineral, g);
                    if (!first) _sb.Append("  ·  ");
                    first = false;
                    _sb.Append("<color=#").Append(UiFactory.Hex(purity[g].tint)).Append('>').Append(purity[g].name)
                       .Append("</color> ").Append(n);
                }

                int kept = KeptFor(reserve, mineral, count);
                string keptText = kept > 0 ? $"  <color=#{UiFactory.Hex(UiTheme.Gold)}>kept: {kept} for jump</color>" : "";
                row.text.text = $"<b><color=#{UiFactory.Hex(mineral.color)}>{mineral.displayName}</color></b> ×{count}{keptText}\n" +
                                $"<size=26>{_sb}</size>";
                row.value.text = $"$ {NumberFormatter.Format(value)}";
                row.lockIcon.enabled = kept > 0 && row.lockIcon.sprite != null;
            }
            for (int i = shown; i < _rows.Count; i++) _rows[i].root.gameObject.SetActive(false);

            _empty.gameObject.SetActive(shown == 0);
            _sellText.text = _sellLabel != null ? _sellLabel() : "SELL";
            _sell.interactable = !cargo.IsEmpty;
        }

        static int KeptFor(IReadOnlyList<(MineralDefinition mineral, int count)> reserve, MineralDefinition mineral, int have)
        {
            foreach (var (m, n) in reserve)
                if (m == mineral) return Mathf.Min(n, have);
            return 0;
        }

        // ---------- Building ----------

        void Build()
        {
            var panel = UiFactory.Modal("Cargo", "CARGO", 88, true, Close, out _canvas);

            _summary = UiFactory.Text(panel, "Summary", "", 30, TextAlignmentOptions.Center, UiTheme.Muted);
            UiFactory.Strip(_summary.rectTransform, true, 90);

            _list = UiFactory.ScrollList(panel, new Vector2(0, 150), new Vector2(0, -100), 8);

            _empty = UiFactory.Text(panel, "Empty", "The hold is empty — break meteors to fill it", 32,
                TextAlignmentOptions.Center, UiTheme.Faint);
            UiFactory.Stretch(_empty.rectTransform);
            _empty.rectTransform.offsetMin = new Vector2(0, 150);
            _empty.rectTransform.offsetMax = new Vector2(0, -100);

            _sell = UiFactory.Button(panel, "Sell", ButtonStyle.Confirm, OnSell, out _sellText, 44);
            UiFactory.PlaceFromBottom((RectTransform)_sell.transform, 0.1f, 0.9f, 10, 124, 0);
        }

        static Row BuildRow(RectTransform content)
        {
            var rt = UiFactory.ListRow(content, "Mineral", RowHeight);
            var row = new Row { root = rt };

            row.icon = UiFactory.Icon(rt, "Icon", null, Color.white);
            UiFactory.Square(row.icon.rectTransform, new Vector2(0, 0.5f), new Vector2(58, 0), 72);

            row.text = UiFactory.Text(rt, "Text", "", 32, TextAlignmentOptions.Left, UiTheme.Text);
            row.text.rectTransform.anchorMin = Vector2.zero;
            row.text.rectTransform.anchorMax = new Vector2(0.74f, 1);
            row.text.rectTransform.offsetMin = new Vector2(110, 0);
            row.text.rectTransform.offsetMax = Vector2.zero;

            row.value = UiFactory.Text(rt, "Value", "", 32, TextAlignmentOptions.Right, UiTheme.Credits);
            row.value.fontStyle = FontStyles.Bold;
            row.value.rectTransform.anchorMin = new Vector2(0.74f, 0);
            row.value.rectTransform.anchorMax = Vector2.one;
            row.value.rectTransform.offsetMin = Vector2.zero;
            row.value.rectTransform.offsetMax = new Vector2(-24, 0);

            row.lockIcon = UiFactory.Icon(rt, "Kept", UiTheme.Icon("lock"), UiTheme.Gold);
            UiFactory.Square(row.lockIcon.rectTransform, new Vector2(1, 1), new Vector2(-30, -26), 32);
            row.lockIcon.enabled = false;
            return row;
        }
    }
}
