using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;

namespace VoidProspector.UI
{
    /// <summary>
    /// Greybox HUD. Top bar: credits, sector and depth controls. Bottom panel: cargo bar, Sell, auto-sell
    /// threshold and the upgrades list.
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

        public RectTransform PopupLayer { get; private set; }
        public TextMeshProUGUI Toast { get; private set; }

        GameRoot _root;
        GameSession Session => _root.Session;

        TextMeshProUGUI _credits, _sector, _depth, _cargoText, _sellLabel, _autoSellLabel;
        Button _sell, _shallower, _deeper;
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

            if (Debug.isDebugBuild) gameObject.AddComponent<DebugPanel>().Init(_root, canvas);
        }

        void BuildTopBar(RectTransform canvas)
        {
            var top = UiFactory.Panel("Top Bar", canvas, PanelColor).rectTransform;
            UiFactory.Strip(top, true, TopBarHeight);

            _credits = UiFactory.Text(top, "Credits", "$ 0", 72, TextAlignmentOptions.Left, CreditsColor);
            _credits.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom(_credits.rectTransform, 0f, 0.5f, 0, TopBarHeight, 40);

            _sector = UiFactory.Text(top, "Sector", "", 34, TextAlignmentOptions.Center, new Color(0.75f, 0.85f, 1f));
            UiFactory.PlaceFromBottom(_sector.rectTransform, 0.5f, 1f, 140, 50, 30);

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

            _sector.text = $"{Session.Sector.index}. {Session.Sector.displayName}";

            Session.Wallet.CreditsRx
                .Subscribe(credits => _credits.text = "$ " + NumberFormatter.Format(credits))
                .AddTo(this);

            Observable.Merge(depths.CurrentRx, depths.DeepestRx, depths.ProgressRx)
                .Subscribe(_ => DrawDepth())
                .AddTo(this);

            Observable.Merge(cargo.CountRx, cargo.CapacityRx)
                .Subscribe(_ => DrawCargo())
                .AddTo(this);

            Observable.Merge(cargo.CountRx.AsUnitObservable(), cargo.ValueRx.AsUnitObservable())
                .Subscribe(_ => DrawSell())
                .AddTo(this);

            Session.AutoSellBelowRx
                .Subscribe(below => _autoSellLabel.text = below == 0
                    ? "Auto-sell\n<size=26>off</size>"
                    : $"Auto-sell\n<size=26>below {Session.Purity[below].name}</size>")
                .AddTo(this);
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
            _sellLabel.text = cargo.IsEmpty ? "SELL" : $"SELL  $ {NumberFormatter.Format(cargo.Value)}";
            _sell.interactable = !cargo.IsEmpty;
        }
    }
}
