using System.Collections.Generic;
using System.Globalization;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Drones;
using VoidProspector.Economy;

namespace VoidProspector.UI
{
    /// <summary>
    /// The Upgrades dialog (opened by the Upgrades tab): credits, a ×1 / ×10 / MAX buy-mode toggle and a scrolling
    /// list of upgrade tracks. Tracks tied to a later sector (drones) appear once that sector is unlocked.
    /// <see cref="AnyAffordable"/> tells the HUD when something can be bought, so the tab can light up while closed.
    /// </summary>
    public class UpgradePanel : MonoBehaviour, ITabPage
    {
        const float HeaderHeight = 72f;
        const float RowHeight = 104f;
        const float RowSpacing = 8f;

        class Row
        {
            public UpgradeTrack track;
            public GameObject root;
            public TextMeshProUGUI info;
            public UiButton buy;
            public TextMeshProUGUI buyLabel;
        }

        GameRoot _root;
        GameSession Session => _root.Session;
        readonly List<Row> _rows = new List<Row>();
        readonly ReactiveProperty<BuyMode> _mode = new ReactiveProperty<BuyMode>(BuyMode.One);
        readonly ReactiveProperty<bool> _open = new ReactiveProperty<bool>(false);
        TextMeshProUGUI _modeLabel, _credits;
        RectTransform _canvas;

        public ReadOnlyReactiveProperty<bool> IsOpenRx => _open;

        /// <summary>True while any available, non-maxed upgrade can buy at least one level. Emits on subscribe, then on changes.</summary>
        public Observable<bool> AnyAffordable { get; private set; }

        public void Init(GameRoot root)
        {
            _root = root;
            _mode.AddTo(this);
            _open.AddTo(this);
            Build();
            _canvas.gameObject.SetActive(false);
            Bind();
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        public void Open()
        {
            _canvas.gameObject.SetActive(true);
            _open.Value = true;
        }

        public void Close()
        {
            _canvas.gameObject.SetActive(false);
            _open.Value = false;
        }

        void Build()
        {
            var area = UiFactory.Modal("Upgrades", "UPGRADES", 89, true, Close, out _canvas);

            // Header: credits (the dialog covers the top bar), then the buy-mode toggle.
            var coin = UiFactory.Icon(area, "Credits Icon", UiTheme.Icon("credits"), UiTheme.Credits);
            UiFactory.Square(coin.rectTransform, new Vector2(0, 1), new Vector2(32, -HeaderHeight / 2), 52);
            _credits = UiFactory.Text(area, "Credits", "", 44, TextAlignmentOptions.Left, UiTheme.Credits);
            _credits.fontStyle = FontStyles.Bold;
            var crt = _credits.rectTransform;
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(0.7f, 1);
            crt.pivot = new Vector2(0, 1);
            crt.offsetMin = new Vector2(70, -HeaderHeight);
            crt.offsetMax = Vector2.zero;

            var mode = UiFactory.Button(area, "Buy Mode", ButtonStyle.Secondary, OnCycleMode, out _modeLabel, 28);
            var mrt = (RectTransform)mode.transform;
            mrt.anchorMin = new Vector2(0.72f, 1);
            mrt.anchorMax = new Vector2(1, 1);
            mrt.pivot = new Vector2(1, 1);
            mrt.offsetMin = new Vector2(0, -HeaderHeight + 4);
            mrt.offsetMax = new Vector2(0, -2);

            var content = UiFactory.ScrollList(area, Vector2.zero, new Vector2(0, -HeaderHeight - 8), RowSpacing);
            foreach (var track in Session.Upgrades.All) _rows.Add(BuildRow(content, track));
        }

        Row BuildRow(RectTransform content, UpgradeTrack track)
        {
            var rt = UiFactory.ListRow(content, track.Definition.id, RowHeight);

            var icon = UiFactory.Icon(rt, "Icon", UiTheme.UpgradeIcon(track.Definition.id), UiTheme.Accent);
            UiFactory.Square(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(58, 0), 64);

            var info = UiFactory.Text(rt, "Info", "", 32, TextAlignmentOptions.Left, UiTheme.Text);
            info.rectTransform.anchorMin = Vector2.zero;
            info.rectTransform.anchorMax = new Vector2(0.62f, 1);
            info.rectTransform.offsetMin = new Vector2(106, 0);
            info.rectTransform.offsetMax = Vector2.zero;

            var row = new Row { track = track, root = rt.gameObject, info = info };
            row.buy = UiFactory.Button(rt, "Buy", ButtonStyle.Primary, () => OnBuy(row), out row.buyLabel, 32);
            var brt = (RectTransform)row.buy.transform;
            brt.anchorMin = new Vector2(0.62f, 0);
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(8, 10);
            brt.offsetMax = new Vector2(-12, -10);
            return row;
        }

        void OnCycleMode()
        {
            var m = _mode.Value;
            _mode.Value = m == BuyMode.One ? BuyMode.Ten : m == BuyMode.Ten ? BuyMode.Max : BuyMode.One;
        }

        void OnBuy(Row row)
        {
            int bought = Session.TryBuy(row.track, _mode.Value);
            if (bought == 0)
            {
                _root.Audio.Deny();
                return;
            }
            _root.Audio.Pickup(4);
            _root.Fx.Burst(_root.Ship.position, new Color(0.5f, 0.9f, 1f), 15, 4f, 0.15f);
        }

        /// <summary>
        /// The mode label follows the buy mode. Each row's text follows its level; its button follows the quote
        /// (level + credits + mode) and only redraws when the quote actually changes, not on every credit.
        /// Subscriptions end when this component is destroyed.
        /// </summary>
        void Bind()
        {
            _mode.Subscribe(m => _modeLabel.text = m == BuyMode.One ? "BUY ×1" : m == BuyMode.Ten ? "BUY ×10" : "BUY MAX")
                .AddTo(this);

            var credits = Session.Wallet.CreditsRx;
            credits.Subscribe(c => _credits.text = "$ " + NumberFormatter.Format(c)).AddTo(this);

            var changes = new List<Observable<Unit>> { credits.AsUnitObservable(), Session.UnlockedSectorRx.AsUnitObservable() };
            foreach (var row in _rows) changes.Add(row.track.LevelRx.AsUnitObservable());
            AnyAffordable = Observable.Merge(changes.ToArray())
                .Select(_ => ComputeAnyAffordable())
                .DistinctUntilChanged();

            foreach (var row in _rows)
            {
                var r = row;
                var t = r.track;
                t.LevelRx.Subscribe(_ => DrawInfo(r)).AddTo(this);
                Session.UnlockedSectorRx.Subscribe(_ => r.root.SetActive(Session.IsAvailable(t))).AddTo(this);

                Observable.CombineLatest(t.LevelRx, credits, _mode, (_, c, m) => t.Quote(m, c))
                    .DistinctUntilChangedBy(q => (q.count, q.cost, q.affordable))
                    .Subscribe(q => DrawBuy(r, q))
                    .AddTo(this);
            }
        }

        bool ComputeAnyAffordable()
        {
            var credits = Session.Wallet.Credits;
            foreach (var row in _rows)
            {
                var t = row.track;
                if (!t.IsMaxed && Session.IsAvailable(t) && t.Quote(BuyMode.One, credits).affordable) return true;
            }
            return false;
        }

        static void DrawInfo(Row row)
        {
            var t = row.track;
            string next = t.IsMaxed ? "" : $" → {Describe(t.Effect, t.NextValue)}";
            row.info.text = $"<b>{t.Definition.displayName}</b>  <size=24><color=#4FD8FF>Lv {t.Level}</color></size>\n" +
                            $"<size=25><color=#9FB8DB>{Describe(t.Effect, t.Value)}{next}</color></size>";
        }

        static void DrawBuy(Row row, UpgradeQuote quote)
        {
            if (row.track.IsMaxed)
            {
                row.buyLabel.text = "MAX";
                row.buy.interactable = false;
                return;
            }
            string count = quote.count > 1 ? $"<size=26>×{quote.count}</size>  " : "";
            row.buyLabel.text = $"{count}$ {NumberFormatter.Format(quote.cost)}";
            row.buy.interactable = quote.affordable;
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
                case UpgradeEffect.DroneCount: return ((int)v).ToString(c) + ((int)v == 1 ? " drone" : " drones");
                case UpgradeEffect.DroneDamage: return (v * 100).ToString("0", c) + "% of drill";
                case UpgradeEffect.DroneTargeting: return DroneTargeting.Describe(DroneTargeting.RuleForLevel(v));
                case UpgradeEffect.RefinerySpeed: return "×" + v.ToString("0.00", c) + " speed";
                case UpgradeEffect.RefineSuccess: return "+" + (v * 100).ToString("0", c) + "% chance";
                case UpgradeEffect.RefineryBatch: return ((int)v).ToString(c) + " per batch";
                case UpgradeEffect.RefineryStabiliser: return (v * 100).ToString("0", c) + "% less loss";
                case UpgradeEffect.ScannerRange: return v <= 0 ? "off" : v.ToString("0.0", c) + "m";
                default: return v.ToString("0.##", c);
            }
        }
    }
}
