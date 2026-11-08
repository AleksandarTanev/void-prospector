using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Economy;

namespace VoidProspector.UI
{
    /// <summary>Scrolling list of upgrade tracks with a ×1 / ×10 / MAX buy-mode toggle.</summary>
    public class UpgradePanel : MonoBehaviour
    {
        const float HeaderHeight = 64f;
        const float RowHeight = 108f;
        const float RowSpacing = 10f;

        static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color BuyColor = new Color(0.16f, 0.36f, 0.56f);
        static readonly Color ToggleColor = new Color(0.22f, 0.24f, 0.32f);

        class Row
        {
            public UpgradeTrack track;
            public TextMeshProUGUI info;
            public Button buy;
            public TextMeshProUGUI buyLabel;
        }

        GameRoot _root;
        GameSession Session => _root.Session;
        readonly List<Row> _rows = new List<Row>();
        BuyMode _mode = BuyMode.One;
        TextMeshProUGUI _modeLabel;

        public void Init(GameRoot root, RectTransform area)
        {
            _root = root;
            Build(area);
            Session.Wallet.Changed += Refresh;
            Session.Upgrades.Bought += OnBought;
            Refresh();
        }

        void OnDestroy()
        {
            if (_root == null || Session == null) return;
            Session.Wallet.Changed -= Refresh;
            Session.Upgrades.Bought -= OnBought;
        }

        void OnBought(UpgradeTrack track, int levels) => Refresh();

        void Build(RectTransform area)
        {
            var title = UiFactory.Text(area, "Title", "UPGRADES", 34, TextAlignmentOptions.Left, new Color(0.75f, 0.85f, 1f));
            title.fontStyle = FontStyles.Bold;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(0.6f, 1);
            trt.pivot = new Vector2(0, 1);
            trt.offsetMin = new Vector2(0, -HeaderHeight);
            trt.offsetMax = Vector2.zero;

            var mode = UiFactory.Button(area, "Buy Mode", ToggleColor, OnCycleMode, out _modeLabel, 32);
            var mrt = (RectTransform)mode.transform;
            mrt.anchorMin = new Vector2(0.72f, 1);
            mrt.anchorMax = new Vector2(1, 1);
            mrt.pivot = new Vector2(1, 1);
            mrt.offsetMin = new Vector2(0, -HeaderHeight + 6);
            mrt.offsetMax = new Vector2(0, -2);

            // Scroll view
            var view = UiFactory.Rect("Scroll View", area);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = Vector2.zero;
            view.offsetMax = new Vector2(0, -HeaderHeight - 8);
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);   // receives drags
            view.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.Rect("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
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

            foreach (var track in Session.Upgrades.All) _rows.Add(BuildRow(content, track));
        }

        Row BuildRow(RectTransform content, UpgradeTrack track)
        {
            var bg = UiFactory.Panel(track.Definition.id, content, RowColor);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var rt = bg.rectTransform;

            var info = UiFactory.Text(rt, "Info", "", 34, TextAlignmentOptions.Left, Color.white);
            info.rectTransform.anchorMin = Vector2.zero;
            info.rectTransform.anchorMax = new Vector2(0.62f, 1);
            info.rectTransform.offsetMin = new Vector2(24, 0);
            info.rectTransform.offsetMax = Vector2.zero;

            var row = new Row { track = track, info = info };
            row.buy = UiFactory.Button(rt, "Buy", BuyColor, () => OnBuy(row), out row.buyLabel, 34);
            var brt = (RectTransform)row.buy.transform;
            brt.anchorMin = new Vector2(0.62f, 0);
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(10, 10);
            brt.offsetMax = new Vector2(-10, -10);
            return row;
        }

        void OnCycleMode()
        {
            _mode = _mode == BuyMode.One ? BuyMode.Ten : _mode == BuyMode.Ten ? BuyMode.Max : BuyMode.One;
            Refresh();
        }

        void OnBuy(Row row)
        {
            int bought = Session.Upgrades.TryBuy(row.track, _mode);
            if (bought == 0)
            {
                _root.Audio.Deny();
                return;
            }
            _root.Audio.Pickup(4);
            _root.Fx.Burst(_root.Ship.position, new Color(0.5f, 0.9f, 1f), 15, 4f, 0.15f);
        }

        void Refresh()
        {
            _modeLabel.text = _mode == BuyMode.One ? "Buy ×1" : _mode == BuyMode.Ten ? "Buy ×10" : "Buy MAX";

            foreach (var row in _rows)
            {
                var t = row.track;
                string next = t.IsMaxed ? "" : $" → {Describe(t.Effect, t.NextValue)}";
                row.info.text = $"<b>{t.Definition.displayName}</b>  <size=26>Lv {t.Level}</size>\n<size=26>{Describe(t.Effect, t.Value)}{next}</size>";

                if (t.IsMaxed)
                {
                    row.buyLabel.text = "MAX";
                    row.buy.interactable = false;
                    continue;
                }
                var quote = Session.Upgrades.Quote(t, _mode);
                string count = quote.count > 1 ? $"<size=26>×{quote.count}</size>  " : "";
                row.buyLabel.text = $"{count}$ {NumberFormatter.Format(quote.cost)}";
                row.buy.interactable = quote.affordable;
            }
        }

        static string Describe(UpgradeEffect effect, double v)
        {
            var c = CultureInfo.InvariantCulture;
            switch (effect)
            {
                case UpgradeEffect.DrillDamage: return NumberFormatter.Format(v) + " dmg";
                case UpgradeEffect.AutoFireInterval: return v <= 0 ? "off" : v.ToString("0.00", c) + "s";
                case UpgradeEffect.CritChance: return (v * 100).ToString("0", c) + "%";
                case UpgradeEffect.CollectorRadius: return v.ToString("0.0", c) + "m";
                case UpgradeEffect.CargoCapacity: return NumberFormatter.Format(v);
                default: return v.ToString("0.##", c);
            }
        }
    }
}
