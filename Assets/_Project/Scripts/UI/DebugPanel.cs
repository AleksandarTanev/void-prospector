using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VoidProspector.Core;

namespace VoidProspector.UI
{
    /// <summary>
    /// Development builds and the editor only: add credits, speed up time, unlock depths and the next sector, restart (keeps the save),
    /// test offline earnings by pretending time passed, and wipe the save.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        GameRoot _root;
        RectTransform _panel;
        TextMeshProUGUI _speedLabel;

        public void Init(GameRoot root, RectTransform canvas)
        {
            _root = root;

            var toggle = UiFactory.Button(canvas, "DEV", ButtonStyle.Secondary, () => _panel.gameObject.SetActive(!_panel.gameObject.activeSelf), out _, 28);
            var trt = (RectTransform)toggle.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0, 1);
            trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = new Vector2(16, -Hud.TopBarHeight - 16);
            trt.sizeDelta = new Vector2(110, 64);

            _panel = UiFactory.Panel("Debug Panel", canvas, new Color(0.1f, 0.05f, 0.05f, 0.92f)).rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0, 1);
            _panel.pivot = new Vector2(0, 1);
            _panel.anchoredPosition = new Vector2(16, -Hud.TopBarHeight - 90);
            _panel.sizeDelta = new Vector2(300, 10 * 84 + 16);

            int i = 0;
            AddButton("+$ 1K", () => root.Session.Wallet.Add(1_000), ref i);
            AddButton("+$ 1M", () => root.Session.Wallet.Add(1_000_000), ref i);
            _speedLabel = AddButton("Speed ×1", ToggleSpeed, ref i);
            AddButton("Unlock depths", () => root.Session.Depths.Unlock(int.MaxValue, root.Session.Depth), ref i);
            AddButton("Unlock sector", () => root.Session.DebugUnlockNextSector(), ref i);
            AddButton("Restart", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex), ref i);
            AddButton("Away 1h", () => root.Saves.PretendAwayAndRestart(System.TimeSpan.FromHours(1)), ref i);
            AddButton("Away 10h", () => root.Saves.PretendAwayAndRestart(System.TimeSpan.FromHours(10)), ref i);
            AddButton("Wipe save", () => root.Saves.WipeAndRestart(), ref i);
            AddButton("Analytics", ShowAnalytics, ref i);

            _panel.gameObject.SetActive(false);
        }

        TextMeshProUGUI AddButton(string label, UnityEngine.Events.UnityAction action, ref int index)
        {
            var b = UiFactory.Button(_panel, label, ButtonStyle.Secondary, action, out var text, 28);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(8, -8 - (index + 1) * 84 + 8);
            rt.offsetMax = new Vector2(-8, -8 - index * 84);
            index++;
            return text;
        }

        /// <summary>Flushes the event log and says where it is (also printed to the console).</summary>
        void ShowAnalytics()
        {
            var a = _root.Analytics;
            a.Game.Flush();
            Debug.Log($"Analytics: {a.Sink.Written} events written to {a.Sink.FilePath}" + (a.Sink.Error != null ? $" (last error: {a.Sink.Error})" : ""));
            _root.Fx.Toast($"{a.Sink.Written} events logged (path in the console)", new Color(0.75f, 0.85f, 1f));
        }

        void ToggleSpeed()
        {
            var fx = _root.Fx;
            fx.BaseTimeScale = fx.BaseTimeScale > 1f ? 1f : 10f;
            _speedLabel.text = fx.BaseTimeScale > 1f ? "Speed ×10" : "Speed ×1";
        }
    }
}
