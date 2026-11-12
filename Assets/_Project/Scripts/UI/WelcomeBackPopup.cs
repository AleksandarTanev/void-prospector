using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoidProspector.Core;
using VoidProspector.Idle;

namespace VoidProspector.UI
{
    /// <summary>"Welcome back" dialog after offline earnings. Dims and blocks the game until closed.</summary>
    public class WelcomeBackPopup : MonoBehaviour
    {
        GameRoot _root;
        RectTransform _canvas;
        TextMeshProUGUI _away, _credits, _note, _refined;

        public void Init(GameRoot root)
        {
            _root = root;
            Build();
            _canvas.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        /// <summary>Shows what was earned. The credits are already in the wallet; this only tells the player.</summary>
        public void Show(OfflineResult result, string refinedNote = null)
        {
            _refined.text = refinedNote ?? "";
            _away.text = $"You were away for {TimeFormatter.Format(result.awaySeconds)}";
            _credits.text = "+$ " + NumberFormatter.Format(result.credits);
            _note.text = result.capped
                ? $"Your ship kept mining for the first {TimeFormatter.Format(result.paidSeconds)} (the limit)"
                : "Your auto-fire and drones kept mining";
            _canvas.gameObject.SetActive(true);
        }

        void Close()
        {
            _canvas.gameObject.SetActive(false);
            _root.Audio.Sell();
            _root.Fx.Burst(_root.Ship.position, new Color(1f, 0.85f, 0.3f), 40, 7f, 0.22f);
        }

        void Build()
        {
            _canvas = UiFactory.Canvas("Welcome Back");
            _canvas.GetComponent<Canvas>().sortingOrder = 100;   // above the HUD

            var dim = UiFactory.Panel("Dim", _canvas, UiTheme.Dim);   // also swallows taps meant for meteors
            UiFactory.Stretch(dim.rectTransform);

            var panel = UiFactory.Frame("Panel", _canvas, UiTheme.Panel, UiTheme.PanelFallback).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.55f);
            panel.sizeDelta = new Vector2(900, 740);

            var plate = UiFactory.Frame("Title Plate", panel, UiTheme.Header, UiTheme.PanelFallback).rectTransform;
            UiFactory.PlaceFromBottom(plate, 0f, 1f, 610, 104, 36);
            var title = UiFactory.Text(plate, "Title", "WELCOME BACK", 56, TextAlignmentOptions.Center, UiTheme.Text);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6;
            UiFactory.Stretch(title.rectTransform);

            _away = UiFactory.Text(panel, "Away", "", 36, TextAlignmentOptions.Center, UiTheme.Muted);
            Row(_away.rectTransform, 530, 50);

            var coin = UiFactory.Icon(panel, "Coin", UiTheme.Icon("credits"), UiTheme.Credits);
            UiFactory.Square(coin.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 470), 72);

            _credits = UiFactory.Text(panel, "Credits", "", 96, TextAlignmentOptions.Center, UiTheme.Credits);
            _credits.fontStyle = FontStyles.Bold;
            Row(_credits.rectTransform, 300, 120);

            _note = UiFactory.Text(panel, "Note", "", 28, TextAlignmentOptions.Center, UiTheme.Muted);
            Row(_note.rectTransform, 250, 40);

            _refined = UiFactory.Text(panel, "Refined", "", 28, TextAlignmentOptions.Center, UiTheme.Refine);
            Row(_refined.rectTransform, 200, 40);

            var collect = UiFactory.Button(panel, "Collect", ButtonStyle.Confirm, Close, out var label, 52);
            label.text = "COLLECT";
            UiFactory.PlaceFromBottom((RectTransform)collect.transform, 0.2f, 0.8f, 50, 124, 0);
        }

        /// <summary>A full-width line in the panel, <paramref name="bottom"/> pixels from its bottom edge.</summary>
        static void Row(RectTransform rt, float bottom, float height) => UiFactory.PlaceFromBottom(rt, 0f, 1f, bottom, height, 30);
    }
}
