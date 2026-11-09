using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Idle;

namespace VoidProspector.UI
{
    /// <summary>
    /// Greybox HUD. Top bar: credits, idle income per second, the sector (opens the Star Map) and depth controls.
    /// Bottom panel: cargo bar, Sell, auto-sell threshold and the upgrades list.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        /// <summary>Panel heights in reference pixels (1080×1920). The world layout keeps the ship above the bottom panel.</summary>
        public const float TopBarHeight = 200f;
        public const float BottomPanelHeight = 640f;

        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color SellColor = new Color(0.18f, 0.5f, 0.3f);
        static readonly Color ToggleColor = new Color(0.22f, 0.24f, 0.32f);
        static readonly Color CreditsColor = new Color(1f, 0.9f, 0.5f);
        static readonly Color CargoColor = new Color(0.5f, 0.85f, 1f);
        static readonly Color CargoFullColor = new Color(1f, 0.45f, 0.4f);
        static readonly Color DepthColor = new Color(0.6f, 0.9f, 1f);
        static readonly Color IdleColor = new Color(0.75f, 0.85f, 1f);
        static readonly Color MapColor = new Color(0.22f, 0.24f, 0.32f);
        static readonly Color MapReadyColor = new Color(0.65f, 0.5f, 0.15f);
        static readonly Color JumpColor = new Color(1f, 0.8f, 0.4f);

        public RectTransform PopupLayer { get; private set; }
        public TextMeshProUGUI Toast { get; private set; }

        GameRoot _root;
        GameSession Session => _root.Session;

        TextMeshProUGUI _credits, _idle, _sector, _depth, _cargoText, _sellLabel, _autoSellLabel;
        Button _sell, _shallower, _deeper, _map;
        Image _mapImage;
        StarMapPanel _starMap;
        JumpState _jumpState;
        RectTransform _cargoFill, _depthFill;
        Image _cargoFillImage;
        UpgradePanel _upgrades;

        public void Init(GameRoot root)
        {
            _root = root;
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

            Toast = UiFactory.Text(canvas, "Toast", "", 56, TextAlignmentOptions.Center, Color.white);
            Toast.fontStyle = FontStyles.Bold;
            UiFactory.AddOutline(Toast);
            var trt = Toast.rectTransform;
            UiFactory.Strip(trt, true, 90);
            trt.offsetMin += new Vector2(0, -TopBarHeight - 20);
            trt.offsetMax += new Vector2(0, -TopBarHeight - 20);

            BuildBottomPanel(canvas);

            // Right edge, under the top bar: Codex, then Refinery (appears with Sector 3).
            gameObject.AddComponent<CodexPanel>().Init(_root, canvas, 0);
            gameObject.AddComponent<RefineryPanel>().Init(_root, canvas, 1);
            gameObject.AddComponent<ScannerView>().Init(_root, PopupLayer);

            if (Debug.isDebugBuild) gameObject.AddComponent<DebugPanel>().Init(_root, canvas);
        }

        void BuildTopBar(RectTransform canvas)
        {
            var top = UiFactory.Panel("Top Bar", canvas, PanelColor).rectTransform;
            UiFactory.Strip(top, true, TopBarHeight);

            _credits = UiFactory.Text(top, "Credits", "$ 0", 72, TextAlignmentOptions.Left, CreditsColor);
            _credits.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom(_credits.rectTransform, 0f, 0.5f, 50, 120, 40);

            // Idle income: what auto-fire and drones earn per second with no input (offline earnings use the same number).
            _idle = UiFactory.Text(top, "Idle Income", "", 30, TextAlignmentOptions.Left, IdleColor);
            UiFactory.PlaceFromBottom(_idle.rectTransform, 0f, 0.5f, 18, 44, 40);

            _starMap = gameObject.AddComponent<StarMapPanel>();
            _starMap.Init(_root);
            _map = UiFactory.Button(top, "Map", MapColor, _starMap.Open, out _sector, 30);
            _mapImage = (Image)_map.targetGraphic;
            UiFactory.PlaceFromBottom((RectTransform)_map.transform, 0.5f, 1f, 136, 56, 24);

            _shallower = UiFactory.Button(top, "Shallower", ToggleColor, () => Session.Depths.TrySetDepth(Session.Depth - 1), out var lessLabel, 44);
            lessLabel.text = "<";
            UiFactory.PlaceFromBottom((RectTransform)_shallower.transform, 0.5f, 0.6f, 40, 90, 8);

            _depth = UiFactory.Text(top, "Depth", "", 40, TextAlignmentOptions.Center, DepthColor);
            _depth.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom(_depth.rectTransform, 0.6f, 0.9f, 52, 80, 0);

            _deeper = UiFactory.Button(top, "Deeper", ToggleColor, () => Session.Depths.TrySetDepth(Session.Depth + 1), out var moreLabel, 44);
            moreLabel.text = ">";
            UiFactory.PlaceFromBottom((RectTransform)_deeper.transform, 0.9f, 1f, 40, 90, 8);

            // Progress toward unlocking the next depth.
            var bg = UiFactory.Panel("Depth Progress", top, new Color(1, 1, 1, 0.12f)).rectTransform;
            UiFactory.PlaceFromBottom(bg, 0.6f, 0.9f, 30, 12, 10);
            _depthFill = UiFactory.Rect("Fill", bg);
            _depthFill.anchorMin = Vector2.zero;
            _depthFill.anchorMax = new Vector2(0, 1);
            _depthFill.offsetMin = _depthFill.offsetMax = Vector2.zero;
            _depthFill.gameObject.AddComponent<Image>().color = DepthColor;
        }

        void BuildBottomPanel(RectTransform canvas)
        {
            var bottom = UiFactory.Panel("Bottom Panel", canvas, PanelColor).rectTransform;
            UiFactory.Strip(bottom, false, BottomPanelHeight);

            _cargoText = UiFactory.Text(bottom, "Cargo", "", 36, TextAlignmentOptions.Left, Color.white);
            UiFactory.PlaceFromBottom(_cargoText.rectTransform, 0f, 0.35f, 570, 56, 40);

            var cargoBg = UiFactory.Panel("Cargo Bar", bottom, new Color(1, 1, 1, 0.12f)).rectTransform;
            UiFactory.PlaceFromBottom(cargoBg, 0.35f, 1f, 581, 34, 40);
            _cargoFill = UiFactory.Rect("Fill", cargoBg);
            _cargoFill.anchorMin = Vector2.zero;
            _cargoFill.anchorMax = new Vector2(0, 1);
            _cargoFill.offsetMin = _cargoFill.offsetMax = Vector2.zero;
            _cargoFillImage = _cargoFill.gameObject.AddComponent<Image>();
            _cargoFillImage.color = CargoColor;

            _sell = UiFactory.Button(bottom, "Sell", SellColor, OnSell, out _sellLabel, 52);
            _sellLabel.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom((RectTransform)_sell.transform, 0f, 0.7f, 440, 115, 30);
            ((RectTransform)_sell.transform).offsetMin = new Vector2(40, 440);

            var autoSell = UiFactory.Button(bottom, "Auto-sell", ToggleColor, OnCycleAutoSell, out _autoSellLabel, 30);
            UiFactory.PlaceFromBottom((RectTransform)autoSell.transform, 0.7f, 1f, 440, 115, 0);
            ((RectTransform)autoSell.transform).offsetMax = new Vector2(-40, 555);

            var list = UiFactory.Rect("Upgrades", bottom);
            UiFactory.PlaceFromBottom(list, 0f, 1f, 16, 410, 40);
            _upgrades = gameObject.AddComponent<UpgradePanel>();
            _upgrades.Init(_root, list);
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
                _root.Fx.Toast("Keeping these for the jump drive", JumpColor);
                return;
            }
            _root.Audio.Sell();
            _root.Fx.Toast($"+$ {NumberFormatter.Format(value)}", CreditsColor);
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

            _sector.text = $"{Session.Sector.index}. {Session.Sector.displayName}  <size=24>MAP</size>";

            BindJumpDrive();
            BindIdleIncome();

            Session.Milestones.Completed
                .Subscribe(m =>
                {
                    _root.Fx.Toast($"Milestone: {m.Title}!\n<size=38>{m.RewardText}</size>", JumpColor);
                    _root.Audio.Rare(2);
                })
                .AddTo(this);

            Session.Wallet.CreditsRx
                .Subscribe(credits => _credits.text = "$ " + NumberFormatter.Format(credits))
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
                    ? "Auto-sell\n<size=26>off</size>"
                    : $"Auto-sell\n<size=26>below {Session.Purity[below].name}</size>")
                .AddTo(this);
        }

        /// <summary>
        /// The map button turns gold when the jump drive can be bought; toasts announce when it becomes available
        /// (minerals are kept from then on) and when it's affordable.
        /// </summary>
        void BindJumpDrive()
        {
            var cargo = Session.Cargo;
            Observable.Merge(Session.Wallet.CreditsRx.AsUnitObservable(), cargo.CountRx.AsUnitObservable(),
                    Session.Depths.DeepestRx.AsUnitObservable(), Session.UnlockedSectorRx.AsUnitObservable())
                .Select(_ => Session.JumpState)
                .DistinctUntilChanged()
                .Subscribe(state => _mapImage.color = state == JumpState.Affordable ? MapReadyColor : MapColor)
                .AddTo(this);

            // Skip the state at startup so loading a save doesn't toast.
            _jumpState = Session.JumpState;
            Observable.Merge(Session.Wallet.CreditsRx.AsUnitObservable(), cargo.CountRx.AsUnitObservable(),
                    Session.Depths.DeepestRx.AsUnitObservable(), Session.UnlockedSectorRx.AsUnitObservable())
                .Select(_ => Session.JumpState)
                .DistinctUntilChanged()
                .Skip(1)
                .Subscribe(OnJumpStateChanged)
                .AddTo(this);
        }

        void OnJumpStateChanged(JumpState state)
        {
            var previous = _jumpState;
            _jumpState = state;
            var next = Session.NextSector;
            if (next == null) return;
            if (state == JumpState.Affordable)
                _root.Fx.Toast("Jump drive ready! Open the MAP", JumpColor);
            else if (state == JumpState.Saving && previous == JumpState.NeedsDepth)
                _root.Fx.Toast($"Jump drive to {next.displayName} available: see the MAP", JumpColor);
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
            _idle.text = $"Idle  $ {NumberFormatter.Format(rate)}/s";
        }

        void DrawDepth()
        {
            var depths = Session.Depths;
            _depth.text = $"Depth {depths.Current + 1}<size=26>/{depths.MaxDepth}</size>";
            _shallower.interactable = depths.CanGoShallower;
            _deeper.interactable = depths.CanGoDeeper;
            _depthFill.anchorMax = new Vector2(depths.ProgressFraction, 1);
        }

        void DrawCargo()
        {
            var cargo = Session.Cargo;
            _cargoText.text = $"Cargo {cargo.Count}/{cargo.Capacity}";
            _cargoFill.anchorMax = new Vector2(cargo.Fill, 1);
            _cargoFillImage.color = cargo.IsFull ? CargoFullColor : CargoColor;
        }

        void DrawSell()
        {
            var cargo = Session.Cargo;
            var reserve = Session.JumpReserve;
            string keep = reserve.Count > 0 ? $"\n<size=24>keeps {reserve[0].count} {reserve[0].mineral.displayName} for the jump drive</size>" : "";
            _sellLabel.text = cargo.IsEmpty ? "SELL" : $"SELL  $ {NumberFormatter.Format(Session.SellableValue)}{keep}";
            _sell.interactable = !cargo.IsEmpty;
        }
    }
}
