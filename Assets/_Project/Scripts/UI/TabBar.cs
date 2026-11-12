using System.Collections.Generic;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VoidProspector.UI
{
    /// <summary>A dialog the tab bar opens: Star Map, Refinery, Codex.</summary>
    public interface ITabPage
    {
        ReadOnlyReactiveProperty<bool> IsOpenRx { get; }
        void Open();
        void Close();
    }

    /// <summary>
    /// The row of tabs along the bottom of the HUD. The first tab is "home" (no dialog open: the upgrades list);
    /// the others open their page as a dialog above the bar. Only one page is open at a time, and the highlighted
    /// tab follows whichever page is open, however it was closed.
    /// </summary>
    public class TabBar : MonoBehaviour
    {
        public class Tab
        {
            public UiButton button;
            public Image selected, icon;
            public TextMeshProUGUI label;
            public ITabPage page;

            /// <summary>While locked, tapping runs <see cref="onLocked"/> (e.g. a toast) instead of opening the page.</summary>
            public bool locked;
            public UnityAction onLocked;

            /// <summary>Gold icon and label: something is ready there (jump drive, finished batch).</summary>
            public void SetAlert(bool alert)
            {
                var c = alert ? UiTheme.Gold : UiTheme.Text;
                button.SetIconColor(c);
                button.SetLabelColor(alert ? UiTheme.Gold : UiTheme.Muted);
            }

            public void SetIcon(Sprite sprite)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        RectTransform _bar;
        readonly List<Tab> _tabs = new List<Tab>();
        int _selected = -1;

        public IReadOnlyList<Tab> Tabs => _tabs;

        public void Init(RectTransform canvas, float height)
        {
            _bar = UiFactory.Frame("Tab Bar", canvas, UiTheme.TabBar, new Color(0.05f, 0.07f, 0.12f, 0.97f)).rectTransform;
            UiFactory.Strip(_bar, false, height);
            _bar.offsetMin = new Vector2(-8, -8);   // hide the frame's bottom edge and sides off screen
            _bar.offsetMax = new Vector2(8, height);
        }

        /// <summary>Adds a tab; <paramref name="page"/> null makes it the home tab (closes any open page).</summary>
        public Tab Add(string label, string icon, ITabPage page)
        {
            var tab = new Tab { page = page };
            int index = _tabs.Count;
            _tabs.Add(tab);

            var rt = UiFactory.Rect(label, _bar);
            tab.selected = UiFactory.Frame("Selected", rt, UiTheme.TabSelected, new Color(0.31f, 0.85f, 1f, 0.2f), 0.8f);
            UiFactory.Stretch(tab.selected.rectTransform);
            tab.selected.rectTransform.offsetMin = new Vector2(6, 12);
            tab.selected.rectTransform.offsetMax = new Vector2(-6, -2);
            tab.selected.gameObject.SetActive(false);

            var hit = UiFactory.Panel("Hit", rt, new Color(0, 0, 0, 0));   // whole tab is tappable
            UiFactory.Stretch(hit.rectTransform);
            tab.button = hit.gameObject.AddComponent<UiButton>();
            tab.button.targetGraphic = hit;
            tab.button.transition = Selectable.Transition.None;
            tab.button.onClick.AddListener(() => OnClick(index));

            // Icon and label sit inside the tappable rect so they squash with it.
            tab.icon = UiFactory.Icon(hit.transform, "Icon", UiTheme.Icon(icon), UiTheme.Text);
            UiFactory.Square(tab.icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -58), 72);

            tab.label = UiFactory.Text(hit.transform, "Label", label, 26, TextAlignmentOptions.Center, UiTheme.Muted);
            tab.label.fontStyle = FontStyles.Bold;
            UiFactory.PlaceFromBottom(tab.label.rectTransform, 0f, 1f, 18, 36, 0);
            tab.button.SetupFixedFrame(tab.icon, tab.label);

            Layout();
            if (page != null)
                page.IsOpenRx.Subscribe(_ => RefreshSelection()).AddTo(this);
            RefreshSelection();
            return tab;
        }

        /// <summary>Closes any open page (back to the home tab).</summary>
        public void CloseAll()
        {
            foreach (var t in _tabs)
                if (t.page != null && t.page.IsOpenRx.CurrentValue) t.page.Close();
        }

        void OnClick(int index)
        {
            var tab = _tabs[index];
            if (tab.locked)
            {
                tab.onLocked?.Invoke();
                return;
            }
            var page = tab.page;
            bool wasOpen = page != null && page.IsOpenRx.CurrentValue;
            CloseAll();
            if (page != null && !wasOpen) page.Open();
        }

        void Layout()
        {
            float w = 1f / _tabs.Count;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var rt = (RectTransform)_tabs[i].selected.transform.parent;
                rt.anchorMin = new Vector2(w * i, 0);
                rt.anchorMax = new Vector2(w * (i + 1), 1);
                rt.offsetMin = new Vector2(0, 8);   // the bar sticks out 8 px below the screen
                rt.offsetMax = new Vector2(0, -8);
            }
        }

        void RefreshSelection()
        {
            int selected = 0;
            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i].page != null && _tabs[i].page.IsOpenRx.CurrentValue) { selected = i; break; }
            if (selected == _selected) return;
            _selected = selected;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                bool on = i == selected;
                t.selected.gameObject.SetActive(on);
                t.icon.rectTransform.DOKill();
                t.icon.rectTransform.DOScale(on ? 1.15f : 1f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(t.icon.gameObject);
            }
        }
    }
}
