using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;

namespace VoidProspector.UI
{
    /// <summary>Greybox HUD: credits and sector on top; cargo bar and Sell button at the bottom.</summary>
    public class Hud : MonoBehaviour
    {
        /// <summary>Panel heights in reference pixels (1080×1920). The world layout keeps the ship above the bottom panel.</summary>
        public const float TopBarHeight = 170f;
        public const float BottomPanelHeight = 320f;

        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color SellColor = new Color(0.18f, 0.5f, 0.3f);
        static readonly Color CreditsColor = new Color(1f, 0.9f, 0.5f);
        static readonly Color CargoColor = new Color(0.5f, 0.85f, 1f);
        static readonly Color CargoFullColor = new Color(1f, 0.45f, 0.4f);

        public RectTransform PopupLayer { get; private set; }
        public Text Toast { get; private set; }

        GameRoot _root;
        GameSession Session => _root.Session;

        Text _credits, _sector, _cargoText, _sellLabel;
        Button _sell;
        RectTransform _cargoFill;
        Image _cargoFillImage;

        public void Init(GameRoot root)
        {
            _root = root;
            UiFactory.EnsureEventSystem();
            Build();
            Session.Wallet.Changed += Refresh;
            Session.Cargo.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_root == null || Session == null) return;
            Session.Wallet.Changed -= Refresh;
            Session.Cargo.Changed -= Refresh;
        }

        void Build()
        {
            var canvas = UiFactory.Canvas("HUD");

            // Popups sit behind the panels and never block taps.
            PopupLayer = UiFactory.Rect("Popups", canvas);
            UiFactory.Stretch(PopupLayer);

            // Top bar
            var top = UiFactory.Panel("Top Bar", canvas, PanelColor).rectTransform;
            UiFactory.Strip(top, true, TopBarHeight);

            _credits = UiFactory.Text(top, "Credits", "$ 0", 68, TextAnchor.MiddleLeft, CreditsColor);
            _credits.fontStyle = FontStyle.Bold;
            UiFactory.PlaceFromBottom(_credits.rectTransform, 0f, 0.6f, 0, TopBarHeight, 40);

            _sector = UiFactory.Text(top, "Sector", "", 36, TextAnchor.MiddleRight, new Color(0.75f, 0.85f, 1f));
            UiFactory.PlaceFromBottom(_sector.rectTransform, 0.6f, 1f, 0, TopBarHeight, 40);

            // Toast under the top bar
            Toast = UiFactory.Text(canvas, "Toast", "", 56, TextAnchor.MiddleCenter, Color.white);
            Toast.fontStyle = FontStyle.Bold;
            Toast.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            var trt = Toast.rectTransform;
            UiFactory.Strip(trt, true, 90);
            trt.offsetMin += new Vector2(0, -TopBarHeight - 20);
            trt.offsetMax += new Vector2(0, -TopBarHeight - 20);

            // Bottom panel: cargo bar, then the Sell button
            var bottom = UiFactory.Panel("Bottom Panel", canvas, PanelColor).rectTransform;
            UiFactory.Strip(bottom, false, BottomPanelHeight);

            _cargoText = UiFactory.Text(bottom, "Cargo", "", 36, TextAnchor.MiddleLeft, Color.white);
            UiFactory.PlaceFromBottom(_cargoText.rectTransform, 0f, 0.35f, 230, 60, 40);

            var cargoBg = UiFactory.Panel("Cargo Bar", bottom, new Color(1, 1, 1, 0.12f)).rectTransform;
            UiFactory.PlaceFromBottom(cargoBg, 0.35f, 1f, 243, 34, 40);
            _cargoFill = UiFactory.Rect("Fill", cargoBg);
            _cargoFill.anchorMin = Vector2.zero;
            _cargoFill.anchorMax = new Vector2(0, 1);
            _cargoFill.offsetMin = _cargoFill.offsetMax = Vector2.zero;
            _cargoFillImage = _cargoFill.gameObject.AddComponent<Image>();
            _cargoFillImage.color = CargoColor;

            _sell = UiFactory.Button(bottom, "Sell", SellColor, OnSell, out _sellLabel, 56);
            _sellLabel.fontStyle = FontStyle.Bold;
            UiFactory.PlaceFromBottom((RectTransform)_sell.transform, 0f, 1f, 40, 170, 40);
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

        void Refresh()
        {
            var cargo = Session.Cargo;
            _credits.text = "$ " + NumberFormatter.Format(Session.Wallet.Credits);
            _sector.text = $"{Session.Sector.displayName}\n<size=28>Sector {Session.Sector.index}</size>";

            _cargoText.text = $"Cargo {cargo.Count}/{cargo.Capacity}";
            _cargoFill.anchorMax = new Vector2(cargo.Fill, 1);
            _cargoFillImage.color = cargo.IsFull ? CargoFullColor : CargoColor;

            _sellLabel.text = cargo.IsEmpty ? "SELL" : $"SELL   $ {NumberFormatter.Format(cargo.Value)}";
            _sell.interactable = !cargo.IsEmpty;
        }
    }
}
