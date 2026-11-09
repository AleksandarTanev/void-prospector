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
        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.18f, 0.97f);
        static readonly Color CollectColor = new Color(0.18f, 0.5f, 0.3f);
        static readonly Color CreditsColor = new Color(1f, 0.9f, 0.5f);
        static readonly Color MutedColor = new Color(0.75f, 0.85f, 1f);

        GameRoot _root;
        RectTransform _canvas;
        TextMeshProUGUI _away, _credits, _note;

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
        public void Show(OfflineResult result)
        {
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

            var dim = UiFactory.Panel("Dim", _canvas, DimColor);   // also swallows taps meant for meteors
            UiFactory.Stretch(dim.rectTransform);

            var panel = UiFactory.Panel("Panel", _canvas, PanelColor).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.55f);
            panel.sizeDelta = new Vector2(860, 620);

            var title = UiFactory.Text(panel, "Title", "WELCOME BACK", 64, TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold;
            Row(title.rectTransform, 520, 80);

            _away = UiFactory.Text(panel, "Away", "", 38, TextAlignmentOptions.Center, MutedColor);
            Row(_away.rectTransform, 440, 50);

            _credits = UiFactory.Text(panel, "Credits", "", 96, TextAlignmentOptions.Center, CreditsColor);
            _credits.fontStyle = FontStyles.Bold;
            Row(_credits.rectTransform, 300, 120);

            _note = UiFactory.Text(panel, "Note", "", 30, TextAlignmentOptions.Center, MutedColor);
            Row(_note.rectTransform, 230, 40);

            var collect = UiFactory.Button(panel, "Collect", CollectColor, Close, out var label, 52);
            label.fontStyle = FontStyles.Bold;
            label.text = "COLLECT";
            UiFactory.PlaceFromBottom((RectTransform)collect.transform, 0.2f, 0.8f, 50, 130, 0);
        }

        /// <summary>A full-width line in the panel, <paramref name="bottom"/> pixels from its bottom edge.</summary>
        static void Row(RectTransform rt, float bottom, float height) => UiFactory.PlaceFromBottom(rt, 0f, 1f, bottom, height, 30);
    }
}
