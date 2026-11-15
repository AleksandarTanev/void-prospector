using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Idle;

namespace VoidProspector.UI
{
    /// <summary>
    /// The HUD. Top bar: credits, idle income per second, the sector and depth controls.
    /// Bottom: a short panel with the cargo bar, Sell and auto-sell, and under it the tab bar
    /// (Upgrades / Refinery / Codex / Map) that opens the dialogs.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        /// <summary>Heights in reference pixels (1080×1920). The world layout keeps the ship above the bottom panel.</summary>
        public const float TopBarHeight = 200f;
        public const float TabBarHeight = 150f;
        const float BodyHeight = 200f;
        public const float BottomPanelHeight = TabBarHeight + BodyHeight;

        static readonly Color CargoFullColor = new Color(1f, 0.45f, 0.4f);

        public RectTransform PopupLayer { get; private set; }
        public ToastView Toast { get; private set; }

        GameRoot _root;
        GameSession Session => _root.Session;

        TextMeshProUGUI _credits, _idle, _sector, _depth, _cargoText, _sellLabel, _autoSellLabel;
        UiButton _sell, _shallower, _deeper;
        TabBar _tabs;
        TabBar.Tab _mapTab;
        JumpState _jumpState;
        RectTransform _cargoFill, _depthFill;
        Image _cargoTrack;
        Color _cargoTrackColor;
        CargoPanel _cargo;
        List<MineralDefinition> _minerals;
        readonly List<Image> _segments = new List<Image>();

        public void Init(GameRoot root)
        {
            _root = root;
            _minerals = UiTheme.SortedMinerals(Session.Database);
            UiFactory.EnsureEventSystem();
            Build();
            Bind();
        }

        void Build()
        {
            var canvas = UiFactory.Canvas("HUD");

            // Popups sit behind the panels and never block taps.
            PopupLayer = UiFactory.Rect("Popups", canvas);
            UiFactory.Stretch(PopupLayer);

            BuildTopBar(canvas);
            BuildBottomPanel(canvas);
            BuildTabs(canvas);
            gameObject.AddComponent<ScannerView>().Init(_root, PopupLayer);
            gameObject.AddComponent<PickupFeed>().Init(Session, PopupLayer);

            Toast = ToastView.Create(canvas, TopBarHeight + 24);

            if (Debug.isDebugBuild) gameObject.AddComponent<DebugPanel>().Init(_root, canvas);
        }

        void BuildTopBar(RectTransform canvas)
        {
            var top = UiFactory.Rect("Top Bar", canvas);
            UiFactory.Strip(top, true, TopBarHeight);

            // The strip art has its bright edge on top; flip it so the edge faces the game.
            var bg = UiFactory.Frame("Background", top, UiTheme.Strip, new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(-8, 0);
            bg.rectTransform.offsetMax = new Vector2(8, 8);
            bg.rectTransform.localScale = new Vector3(1, -1, 1);

            // Left: credits, and under them idle income (what auto-fire and drones earn per second with no input).
            var coin = UiFactory.Icon(top, "Credits Icon", UiTheme.Icon("credits"), UiTheme.Credits);
            UiFactory.Square(coin.rectTransform, Vector2.zero, new Vector2(78, 122), 76);
            _credits = UiFactory.Text(top, "Credits", "0", 70, TextAlignmentOptions.Left, UiTheme.Credits);
            _credits.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom(_credits.rectTransform, 0f, 0.55f, 76, 92, 0);
            _credits.rectTransform.offsetMin = new Vector2(130, 76);

            var clock = UiFactory.Icon(top, "Idle Icon", UiTheme.Icon("idle"), UiTheme.Muted);
            UiFactory.Square(clock.rectTransform, Vector2.zero, new Vector2(78, 46), 44);
            _idle = UiFactory.Text(top, "Idle Income", "", 30, TextAlignmentOptions.Left, UiTheme.Muted);
            UiFactory.PlaceFromBottom(_idle.rectTransform, 0f, 0.55f, 24, 44, 0);
            _idle.rectTransform.offsetMin = new Vector2(130, 24);

            // Right: sector name, then  [^] Depth n/m [v]  with progress toward the next depth.
            _sector = UiFactory.Text(top, "Sector", "", 28, TextAlignmentOptions.Center, UiTheme.Accent);
            _sector.fontStyle = FontStyles.Bold;
            _sector.characterSpacing = 3;
            UiFactory.PlaceFromBottom(_sector.rectTransform, 0.55f, 1f, 140, 44, 20);

            _shallower = UiFactory.IconButton(top, "Shallower", "up", () => Session.Depths.TrySetDepth(Session.Depth - 1));
            UiFactory.Square((RectTransform)_shallower.transform, new Vector2(0.55f, 0), new Vector2(70, 76), 96);

            _deeper = UiFactory.IconButton(top, "Deeper", "down", () => Session.Depths.TrySetDepth(Session.Depth + 1));
            UiFactory.Square((RectTransform)_deeper.transform, new Vector2(1f, 0), new Vector2(-70, 76), 96);

            _depth = UiFactory.Text(top, "Depth", "", 40, TextAlignmentOptions.Center, UiTheme.Text);
            _depth.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom(_depth.rectTransform, 0.55f, 1f, 70, 60, 130);

            // Progress toward unlocking the next depth.
            var bar = UiFactory.Rect("Depth Progress", top);
            UiFactory.PlaceFromBottom(bar, 0.55f, 1f, 34, 26, 130);
            _depthFill = UiFactory.Bar(bar, "Bar", UiTheme.Accent, out _);
            UiFactory.Stretch((RectTransform)_depthFill.parent);
        }

        void BuildBottomPanel(RectTransform canvas)
        {
            var bottom = UiFactory.Rect("Bottom Panel", canvas);
            UiFactory.Strip(bottom, false, BodyHeight);
            bottom.offsetMin = new Vector2(0, TabBarHeight);
            bottom.offsetMax = new Vector2(0, TabBarHeight + BodyHeight);

            var bg = UiFactory.Frame("Background", bottom, UiTheme.Panel, new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(8, -10);
            bg.rectTransform.offsetMax = new Vector2(-8, 0);

            // Cargo row: icon, count, a bar split by mineral, and an info icon. Tapping anywhere on it opens the
            // Cargo dialog; the widgets sit under the hit area so they squash with it.
            var cargoRow = UiFactory.HitArea(bottom, "Cargo Row", ToggleCargo);
            var row = (RectTransform)cargoRow.transform;
            UiFactory.PlaceFromBottom(row, 0f, 1f, 128, 72, 24);

            var crate = UiFactory.Icon(row, "Cargo Icon", UiTheme.Icon("cargo"), UiTheme.Text);
            UiFactory.Square(crate.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), 56);
            _cargoText = UiFactory.Text(row, "Cargo", "", 34, TextAlignmentOptions.Left, UiTheme.Text);
            _cargoText.fontStyle = FontStyles.Bold;
            var crt = _cargoText.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = new Vector2(0.4f, 1);
            crt.offsetMin = new Vector2(88, 0);
            crt.offsetMax = Vector2.zero;

            var cargoBar = UiFactory.Rect("Cargo Bar", row);
            cargoBar.anchorMin = new Vector2(0.4f, 0);
            cargoBar.anchorMax = new Vector2(1f, 0);
            cargoBar.offsetMin = new Vector2(16, 18);
            cargoBar.offsetMax = new Vector2(-64, 54);
            _cargoFill = UiFactory.Bar(cargoBar, "Bar", UiTheme.Accent, out var fillImage);
            UiFactory.Stretch((RectTransform)_cargoFill.parent);
            // The fill rect only holds the mineral segments; the gaps between them show the track.
            fillImage.enabled = false;
            _cargoTrack = _cargoFill.parent.GetComponent<Image>();
            _cargoTrackColor = _cargoTrack.color;

            var info = UiFactory.Icon(row, "Info", UiTheme.Icon("info"), UiTheme.Muted);
            UiFactory.Square(info.rectTransform, new Vector2(1, 0.5f), new Vector2(-28, 0), 40);

            _sell = UiFactory.Button(bottom, "Sell", ButtonStyle.Confirm, OnSell, out _sellLabel, 48);
            UiFactory.PlaceFromBottom((RectTransform)_sell.transform, 0f, 0.68f, 14, 112, 0);
            ((RectTransform)_sell.transform).offsetMin = new Vector2(32, 14);
            ((RectTransform)_sell.transform).offsetMax = new Vector2(-8, 126);

            var autoSell = UiFactory.Button(bottom, "Auto-sell", ButtonStyle.Secondary, OnCycleAutoSell, out _autoSellLabel, 30);
            UiFactory.PlaceFromBottom((RectTransform)autoSell.transform, 0.68f, 1f, 14, 112, 0);
            ((RectTransform)autoSell.transform).offsetMin = new Vector2(8, 14);
            ((RectTransform)autoSell.transform).offsetMax = new Vector2(-32, 126);
        }

        void BuildTabs(RectTransform canvas)
        {
            var starMap = gameObject.AddComponent<StarMapPanel>();
            starMap.Init(_root);
            var codex = gameObject.AddComponent<CodexPanel>();
            codex.Init(_root);
            var refinery = gameObject.AddComponent<RefineryPanel>();
            var upgrades = gameObject.AddComponent<UpgradePanel>();
            upgrades.Init(_root);

            _tabs = gameObject.AddComponent<TabBar>();
            _tabs.Init(canvas, TabBarHeight);
            var upgradesTab = _tabs.Add("Upgrades", "upgrades", upgrades);
            upgrades.AnyAffordable.Subscribe(upgradesTab.SetAlert).AddTo(this);
            var refineryTab = _tabs.Add("Refinery", "refinery", refinery);
            _tabs.Add("Codex", "codex", codex);
            _mapTab = _tabs.Add("Map", "map", starMap);
            refinery.Init(_root, refineryTab);

            // The Cargo dialog has no tab, but still only one dialog is open at a time.
            _cargo = gameObject.AddComponent<CargoPanel>();
            _cargo.Init(_root, OnSell, SellLabel);
            foreach (var tab in _tabs.Tabs)
                if (tab.page != null)
                    tab.page.IsOpenRx.Where(open => open).Subscribe(_ => _cargo.Close()).AddTo(this);
        }

        void ToggleCargo()
        {
            if (_cargo.IsOpenRx.CurrentValue) { _cargo.Close(); return; }
            _tabs.CloseAll();
            _cargo.Open();
        }

        void OnSell()
        {
            if (Session.Cargo.IsEmpty)
            {
                _root.Audio.Deny();
                return;
            }
            var value = Session.SellCargo();
            if (value <= GameNumber.Zero)
            {
                _root.Audio.Deny();
                _root.Fx.Toast("Keeping these for the jump drive", UiTheme.Gold);
                return;
            }
            _root.Audio.Sell();
            _root.Fx.Toast($"+$ {NumberFormatter.Format(value)}", UiTheme.Credits);
            _root.Fx.Burst(_root.Ship.position, new Color(1f, 0.85f, 0.3f), 30, 6f, 0.2f);
        }

        /// <summary>Off → below Standard → below Refined → below Pristine → Off.</summary>
        void OnCycleAutoSell()
        {
            int next = Session.AutoSellBelow + 1;
            if (next > Session.Purity.Count - 2) next = 0;
            Session.SetAutoSellBelow(next);
        }

        /// <summary>
        /// Each widget listens only to the values it shows. Reactive properties emit their current value on
        /// subscribe, so this also draws the initial state. Subscriptions end when this component is destroyed.
        /// </summary>
        void Bind()
        {
            var cargo = Session.Cargo;
            var depths = Session.Depths;

            _sector.text = $"{Session.Sector.index}. {Session.Sector.displayName.ToUpperInvariant()}";

            BindJumpDrive();
            BindIdleIncome();

            Session.Milestones.Completed
                .Subscribe(m =>
                {
                    _root.Fx.Toast($"Milestone: {m.Title}!\n<size=34>{m.RewardText}</size>", UiTheme.Gold);
                    _root.Audio.Rare(2);
                })
                .AddTo(this);

            Session.Wallet.CreditsRx
                .Subscribe(credits => _credits.text = NumberFormatter.Format(credits))
                .AddTo(this);

            Observable.Merge(depths.CurrentRx, depths.DeepestRx, depths.ProgressRx)
                .Subscribe(_ => DrawDepth())
                .AddTo(this);

            Observable.Merge(cargo.CountRx, cargo.CapacityRx)
                .Subscribe(_ => DrawCargo())
                .AddTo(this);

            Observable.Merge(cargo.CountRx.AsUnitObservable(), cargo.ValueRx.AsUnitObservable(), depths.DeepestRx.AsUnitObservable(),
                    Session.UnlockedSectorRx.AsUnitObservable())
                .Subscribe(_ => DrawSell())
                .AddTo(this);

            Session.AutoSellBelowRx
                .Subscribe(below => _autoSellLabel.text = below == 0
                    ? "AUTO-SELL\n<size=24><color=#9FB8DB>off</color></size>"
                    : $"AUTO-SELL\n<size=24>below {Session.Purity[below].name}</size>")
                .AddTo(this);
        }

        /// <summary>
        /// The Map tab turns gold when the jump drive can be bought; toasts announce when it becomes available
        /// (minerals are kept from then on) and when it's affordable.
        /// </summary>
        void BindJumpDrive()
        {
            var cargo = Session.Cargo;
            var jumpState = Observable.Merge(Session.Wallet.CreditsRx.AsUnitObservable(), cargo.CountRx.AsUnitObservable(),
                    Session.Depths.DeepestRx.AsUnitObservable(), Session.UnlockedSectorRx.AsUnitObservable())
                .Select(_ => Session.JumpState)
                .DistinctUntilChanged();

            jumpState.Subscribe(state => _mapTab.SetAlert(state == JumpState.Affordable)).AddTo(this);

            // Skip the state at startup so loading a save doesn't toast.
            _jumpState = Session.JumpState;
            jumpState.Skip(1).Subscribe(OnJumpStateChanged).AddTo(this);
        }

        void OnJumpStateChanged(JumpState state)
        {
            var previous = _jumpState;
            _jumpState = state;
            var next = Session.NextSector;
            if (next == null) return;
            if (state == JumpState.Affordable)
                _root.Fx.Toast("Jump drive ready! Open the MAP", UiTheme.Gold);
            else if (state == JumpState.Saving && previous == JumpState.NeedsDepth)
                _root.Fx.Toast($"Jump drive to {next.displayName} available: see the MAP", UiTheme.Gold);
        }

        /// <summary>
        /// Re-estimates idle income (a short simulation, the same one offline earnings use) shortly after anything that
        /// changes it: purchases, depth, milestones. Debounced so buying ×10 runs it once.
        /// </summary>
        void BindIdleIncome()
        {
            Observable.Merge(
                    Session.Upgrades.Bought.AsUnitObservable(),
                    Session.Depths.CurrentRx.AsUnitObservable(),
                    Session.Milestones.Completed.AsUnitObservable())
                .Debounce(TimeSpan.FromSeconds(0.3), UnityTimeProvider.UpdateIgnoreTimeScale)
                .Prepend(Unit.Default)
                .Subscribe(_ => DrawIdleIncome())
                .AddTo(this);
        }

        void DrawIdleIncome()
        {
            if (!Session.HasIdleMining)
            {
                _idle.text = "Idle: buy Auto-fire";
                return;
            }
            double rate = OfflineEarnings.EstimateIncomePerSecond(Session, Session.Database.offline.incomeSampleSeconds);
            _idle.text = $"{NumberFormatter.Format(rate)}/s idle";
        }

        void DrawDepth()
        {
            var depths = Session.Depths;
            _depth.text = $"DEPTH {depths.Current + 1}<size=26><color=#9FB8DB>/{depths.MaxDepth}</color></size>";
            _shallower.interactable = depths.CanGoShallower;
            _deeper.interactable = depths.CanGoDeeper;
            _depthFill.anchorMax = new Vector2(depths.ProgressFraction, 1);
        }

        /// <summary>Count, and the bar split into one segment per mineral (in the Codex order), sized by its share.</summary>
        void DrawCargo()
        {
            var cargo = Session.Cargo;
            _cargoText.text = $"{cargo.Count}<size=26><color=#9FB8DB>/{cargo.Capacity}</color></size>";
            _cargoText.color = cargo.IsFull ? CargoFullColor : UiTheme.Text;
            _cargoTrack.color = cargo.IsFull ? CargoFullColor : _cargoTrackColor;
            _cargoFill.anchorMax = new Vector2(cargo.Fill, 1);

            int total = cargo.Count, shown = 0, before = 0;
            if (total > 0)
            {
                foreach (var mineral in _minerals)
                {
                    int n = cargo.CountOf(mineral);
                    if (n <= 0) continue;
                    if (shown >= _segments.Count)
                    {
                        var seg = UiFactory.Panel("Segment", _cargoFill, Color.white);
                        seg.raycastTarget = false;
                        _segments.Add(seg);
                    }
                    var img = _segments[shown++];
                    img.color = mineral.color;
                    var rt = img.rectTransform;
                    rt.anchorMin = new Vector2((float)before / total, 0);
                    before += n;
                    rt.anchorMax = new Vector2((float)before / total, 1);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = new Vector2(before < total ? -2 : 0, 0);   // a thin gap between minerals
                    img.gameObject.SetActive(true);
                }
            }
            for (int i = shown; i < _segments.Count; i++) _segments[i].gameObject.SetActive(false);
        }

        void DrawSell()
        {
            _sellLabel.text = SellLabel();
            _sell.interactable = !Session.Cargo.IsEmpty;
        }

        /// <summary>The Sell button's text (HUD and Cargo dialog): the value it gets, and what it keeps for the jump drive.</summary>
        string SellLabel()
        {
            var reserve = Session.JumpReserve;
            string keep = reserve.Count > 0 ? $"\n<size=22>keeps {reserve[0].count} {reserve[0].mineral.displayName} for the jump drive</size>" : "";
            return Session.Cargo.IsEmpty ? "SELL" : $"SELL  $ {NumberFormatter.Format(Session.SellableValue)}{keep}";
        }
    }
}
