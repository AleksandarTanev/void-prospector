using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VoidProspector.Prototype
{
    /// <summary>All prototype UI, built in code: top bar, toast, sell, upgrades, refine and debug buttons.</summary>
    public class ProtoUI : MonoBehaviour
    {
        public static ProtoUI I { get; private set; }
        public static Font UiFont { get; private set; }

        /// <summary>Height of the bottom panel in reference pixels (1080x1920 canvas).</summary>
        public const float BottomPanelHeight = 600f;
        const float TopBarHeight = 170f;

        public RectTransform PopupLayer { get; private set; }
        public Text ToastText { get; private set; }

        class UpgradeButton
        {
            public UpgradeId id;
            public Button button;
            public Text label;
        }

        Text _credits, _cargoText, _sellLabel, _mineralLabel, _refineLabel, _speedLabel;
        Button _sell, _refine;
        RectTransform _cargoFill;
        Image _cargoFillImage;
        readonly List<UpgradeButton> _upgrades = new List<UpgradeButton>();

        int _refineMineral = 1;
        float _refineReadyAt;
        float _refreshTimer;

        static ProtoConfig C => ProtoBootstrap.I.config;
        static ProtoGame Game => ProtoGame.I;

        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color ButtonColor = new Color(0.16f, 0.24f, 0.36f);
        static readonly Color SellColor = new Color(0.18f, 0.5f, 0.3f);
        static readonly Color DebugColor = new Color(0.25f, 0.25f, 0.28f);

        void Awake()
        {
            I = this;
            UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            Build();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Start()
        {
            Game.Changed += Refresh;
            Refresh();
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- Building ----------

        void Build()
        {
            var canvasGo = new GameObject("Prototype UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // Popups sit behind the panels and never block taps.
            PopupLayer = MakeRect("Popups", root);
            Stretch(PopupLayer);

            BuildTopBar(root);
            BuildBottomPanel(root);
        }

        void BuildTopBar(RectTransform root)
        {
            var bar = MakeRect("Top Bar", root);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.offsetMin = new Vector2(0, -TopBarHeight);
            bar.offsetMax = Vector2.zero;
            bar.gameObject.AddComponent<Image>().color = PanelColor;

            _credits = MakeText(bar, "Credits", "$ 0", 68, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.5f));
            _credits.fontStyle = FontStyle.Bold;
            Place(_credits.rectTransform, 0f, 0.5f, 0, TopBarHeight, 40);

            _cargoText = MakeText(bar, "Cargo", "Cargo 0/50", 38, TextAnchor.MiddleRight, Color.white);
            Place(_cargoText.rectTransform, 0.5f, 1f, 75, 70, 40);

            var cargoBg = MakeRect("Cargo Bar", bar);
            Place(cargoBg, 0.5f, 1f, 35, 32, 40);
            cargoBg.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);
            _cargoFill = MakeRect("Fill", cargoBg);
            _cargoFill.anchorMin = Vector2.zero;
            _cargoFill.anchorMax = new Vector2(0, 1);
            _cargoFill.offsetMin = _cargoFill.offsetMax = Vector2.zero;
            _cargoFillImage = _cargoFill.gameObject.AddComponent<Image>();
            _cargoFillImage.color = new Color(0.5f, 0.85f, 1f);

            ToastText = MakeText(root, "Toast", "", 56, TextAnchor.MiddleCenter, Color.white);
            ToastText.fontStyle = FontStyle.Bold;
            ToastText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            var trt = ToastText.rectTransform;
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.offsetMin = new Vector2(0, -TopBarHeight - 110);
            trt.offsetMax = new Vector2(0, -TopBarHeight - 20);
        }

        void BuildBottomPanel(RectTransform root)
        {
            var panel = MakeRect("Bottom Panel", root);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(1, 0);
            panel.pivot = new Vector2(0.5f, 0);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = new Vector2(0, BottomPanelHeight);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            // Sell (top row)
            _sell = MakeButton(panel, "Sell", SellColor, OnSell, out _sellLabel, 52);
            _sellLabel.fontStyle = FontStyle.Bold;
            Place((RectTransform)_sell.transform, 0f, 1f, 440, 140, 24);

            // Upgrades (2x2)
            AddUpgrade(panel, UpgradeId.Drill, 0f, 0.5f, 290);
            AddUpgrade(panel, UpgradeId.AutoFire, 0.5f, 1f, 290);
            AddUpgrade(panel, UpgradeId.Collector, 0f, 0.5f, 145);
            AddUpgrade(panel, UpgradeId.Drone, 0.5f, 1f, 145);

            // Refine + debug (bottom row)
            var mineral = MakeButton(panel, "Refine Mineral", ButtonColor, OnCycleMineral, out _mineralLabel, 32);
            Place((RectTransform)mineral.transform, 0f, 0.3f, 20, 105, 12);
            _refine = MakeButton(panel, "Refine", new Color(0.4f, 0.25f, 0.5f), OnRefine, out _refineLabel, 34);
            Place((RectTransform)_refine.transform, 0.3f, 0.6f, 20, 105, 12);
            var speed = MakeButton(panel, "Speed", DebugColor, OnSpeed, out _speedLabel, 32);
            Place((RectTransform)speed.transform, 0.6f, 0.8f, 20, 105, 12);
            MakeButton(panel, "Add Credits", DebugColor, () => Game.AddCredits(1000), out var addLabel, 32);
            addLabel.text = "+$1000";
            Place((RectTransform)panel.Find("Add Credits"), 0.8f, 1f, 20, 105, 12);
        }

        void AddUpgrade(RectTransform panel, UpgradeId id, float x0, float x1, float y)
        {
            var b = MakeButton(panel, id.ToString(), ButtonColor, () => OnBuy(id), out var label, 34);
            Place((RectTransform)b.transform, x0, x1, y, 130, 24);
            _upgrades.Add(new UpgradeButton { id = id, button = b, label = label });
        }

        // ---------- Actions ----------

        void OnSell()
        {
            if (Game.CargoCount == 0) return;
            double v = Game.Sell();
            ProtoAudio.I.Sell();
            ProtoFx.I.Toast($"+$ {ProtoGame.Fmt(v)}", new Color(1f, 0.9f, 0.5f));
            ProtoFx.I.Burst(ProtoBootstrap.I.Ship.position, new Color(1f, 0.85f, 0.3f), 30, 6f, 0.2f);
        }

        void OnBuy(UpgradeId id)
        {
            if (Game.TryBuy(id))
            {
                ProtoAudio.I.Pickup(4);
                ProtoFx.I.Burst(ProtoBootstrap.I.Ship.position, new Color(0.5f, 0.9f, 1f), 15, 4f, 0.15f);
            }
            else ProtoAudio.I.Deny();
        }

        void OnCycleMineral()
        {
            _refineMineral = (_refineMineral + 1) % C.minerals.Length;
            Refresh();
        }

        void OnRefine()
        {
            if (Time.time < _refineReadyAt || Game.RefinableCount(_refineMineral) == 0) { ProtoAudio.I.Deny(); return; }
            var (up, lost) = Game.Refine(_refineMineral);
            _refineReadyAt = Time.time + C.refineCooldown;
            ProtoAudio.I.Rare(0);
            ProtoFx.I.Toast($"Refined {C.minerals[_refineMineral].name}: {up} up, {lost} lost", new Color(0.85f, 0.7f, 1f));
        }

        void OnSpeed()
        {
            var fx = ProtoFx.I;
            fx.BaseTimeScale = fx.BaseTimeScale < 2f ? 5f : 1f;
            Refresh();
        }

        // ---------- Refresh ----------

        void Update()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0)
            {
                _refreshTimer = 0.2f;
                Refresh();
            }
        }

        void Refresh()
        {
            if (Game == null) return;

            _credits.text = "$ " + ProtoGame.Fmt(Game.Credits);

            float fill = Mathf.Clamp01((float)Game.CargoCount / C.cargoCapacity);
            _cargoText.text = $"Cargo {Game.CargoCount}/{C.cargoCapacity}";
            _cargoFill.anchorMax = new Vector2(fill, 1);
            _cargoFillImage.color = Game.CargoFull ? new Color(1f, 0.45f, 0.4f) : new Color(0.5f, 0.85f, 1f);

            _sellLabel.text = Game.CargoCount > 0 ? $"SELL   $ {ProtoGame.Fmt(Game.CargoValue)}" : "SELL";
            _sell.interactable = Game.CargoCount > 0;

            foreach (var u in _upgrades)
            {
                var def = C.Upgrade(u.id);
                int lvl = Game.Level(u.id);
                string cost = Game.IsMaxed(u.id) ? "MAX" : "$ " + ProtoGame.Fmt(Game.Cost(u.id));
                u.label.text = $"{def.name}  Lv {lvl}\n<size=26>{Effect(u.id)}</size>\n{cost}";
                u.button.interactable = Game.CanBuy(u.id);
            }

            var mineral = C.minerals[_refineMineral];
            _mineralLabel.text = $"{mineral.name}\n<size=24>{Game.CargoOfMineral(_refineMineral)} in cargo</size>";
            _mineralLabel.color = Color.Lerp(mineral.color, Color.white, 0.4f);
            float wait = _refineReadyAt - Time.time;
            _refineLabel.text = wait > 0 ? $"Refine\n<size=24>{wait:0.0}s</size>" : "Refine";
            _refine.interactable = wait <= 0 && Game.RefinableCount(_refineMineral) > 0;

            _speedLabel.text = ProtoFx.I != null && ProtoFx.I.BaseTimeScale > 1f ? "Speed x5" : "Speed x1";
        }

        static string Effect(UpgradeId id)
        {
            var g = Game;
            switch (id)
            {
                case UpgradeId.Drill: return $"{ProtoGame.Fmt(g.DrillDamage)} damage per hit";
                case UpgradeId.AutoFire: return g.AutoFireInterval > 0 ? $"fires every {g.AutoFireInterval:0.00}s" : "auto laser: off";
                case UpgradeId.Collector: return $"pickup radius {g.CollectorRadius:0.0}";
                default: return g.DroneCount == 1 ? "1 drone" : $"{g.DroneCount} drones";
            }
        }

        // ---------- Helpers ----------

        public static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = MakeRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Button MakeButton(RectTransform parent, string name, Color color, UnityAction onClick, out Text label, int fontSize)
        {
            var rt = MakeRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            b.colors = colors;
            b.onClick.AddListener(onClick);

            label = MakeText(rt, "Label", name, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform);
            return b;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Anchors horizontally between x0..x1 (0–1) and places at a pixel height from the parent's bottom.</summary>
        static void Place(RectTransform rt, float x0, float x1, float bottom, float height, float padX)
        {
            rt.anchorMin = new Vector2(x0, 0);
            rt.anchorMax = new Vector2(x1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(padX, bottom);
            rt.offsetMax = new Vector2(-padX, bottom + height);
        }
    }
}
