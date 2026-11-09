using System.Collections.Generic;
using System.Text;
using R3;
using TMPro;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Mining;

namespace VoidProspector.UI
{
    /// <summary>
    /// The scanner (once bought): meteors within range of the ship show what's still inside them. Rare meteors are
    /// marked anywhere on screen and announced when they appear.
    /// </summary>
    public class ScannerView : MonoBehaviour
    {
        const int MaxShown = 3;
        const float AlertCooldown = 8f;
        static readonly Color RareColor = new Color(1f, 0.82f, 0.3f);

        GameRoot _root;
        RectTransform _layer;
        readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
        readonly StringBuilder _sb = new StringBuilder();
        float _lastAlert = -999f;

        GameSession Session => _root.Session;

        public void Init(GameRoot root, RectTransform popupLayer)
        {
            _root = root;
            _layer = popupLayer;
            root.Meteors.Spawned.Subscribe(OnSpawned).AddTo(this);
        }

        void OnSpawned(Meteor m)
        {
            if (Session.ScannerRange <= 0 || !MeteorScan.IsRare(m.Spawn, Session.Database.scanner)) return;
            if (Time.unscaledTime - _lastAlert < AlertCooldown) return;
            _lastAlert = Time.unscaledTime;
            var best = MeteorScan.Contents(m.Spawn, 0);
            string what = best.Count > 0 ? $" ({best[0].count} {best[0].mineral.displayName})" : "";
            _root.Fx.Toast($"Scanner: rare meteor{what}!", RareColor);
            _root.Audio.Rare(1);
        }

        void LateUpdate()
        {
            float range = Session.ScannerRange;
            int used = 0;
            if (range > 0)
            {
                Vector2 ship = _root.Ship.position;
                var settings = Session.Database.scanner;
                foreach (var m in _root.Meteors.Active)
                {
                    if (!m.Active) continue;
                    Vector2 p = m.transform.position;
                    bool inRange = Vector2.Distance(p, ship) - m.Radius <= range;
                    bool rare = MeteorScan.IsRare(m.Spawn, settings);
                    if (!inRange && !rare) continue;

                    var label = Label(used++);
                    label.text = Describe(m, inRange, rare);
                    Vector2 screen = _root.Cam.WorldToScreenPoint(p + Vector2.down * (m.Radius + 0.25f));
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var local);
                    label.rectTransform.anchoredPosition = local;
                }
            }
            for (int i = used; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);
        }

        string Describe(Meteor m, bool inRange, bool rare)
        {
            _sb.Clear();
            if (rare) _sb.Append("<color=#").Append(UiFactory.Hex(RareColor)).Append("><b>RARE</b></color>");
            if (inRange)
            {
                var contents = MeteorScan.Contents(m.Spawn, m.SpilledChunks);
                for (int i = 0; i < contents.Count && i < MaxShown; i++)
                {
                    var (mineral, count) = contents[i];
                    if (_sb.Length > 0) _sb.Append(i == 0 && rare ? "\n" : "  ");
                    _sb.Append("<color=#").Append(UiFactory.Hex(mineral.color)).Append('>')
                       .Append(count).Append(' ').Append(mineral.displayName).Append("</color>");
                }
            }
            return _sb.ToString();
        }

        TextMeshProUGUI Label(int index)
        {
            while (_labels.Count <= index)
            {
                var t = UiFactory.Text(_layer, "Scan", "", 26, TextAlignmentOptions.Top, Color.white);
                UiFactory.AddOutline(t);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(400, 60);
                _labels.Add(t);
            }
            var label = _labels[index];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            return label;
        }
    }
}
