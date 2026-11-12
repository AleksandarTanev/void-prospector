using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoidProspector.UI
{
    /// <summary>
    /// A themed button: the sprite follows its <see cref="Style"/>, it swaps to the disabled frame when not
    /// interactable (and dims its label and icon), and squashes a little when pressed. Made by <see cref="UiFactory.Button"/>.
    /// </summary>
    public class UiButton : Button
    {
        const float PressedScale = 0.94f;
        static readonly Color DisabledContent = new Color(1f, 1f, 1f, 0.45f);

        ButtonStyle _style;
        bool _fixedFrame;   // icon buttons and tabs keep their own frame; only the content dims
        TMP_Text _label;
        Graphic _icon;
        Color _labelColor = Color.white, _iconColor = Color.white;
        bool _disabledDrawn;

        public TMP_Text Label => _label;

        public ButtonStyle Style
        {
            get => _style;
            set
            {
                _style = value;
                ApplySprite(!IsInteractable());
            }
        }

        internal void Setup(ButtonStyle style, TMP_Text label)
        {
            _style = style;
            _label = label;
            if (label != null) _labelColor = label.color;
            ApplySprite(!IsInteractable());
        }

        /// <summary>For buttons whose frame isn't a button style (icon buttons, tabs): only the icon and label dim.</summary>
        internal void SetupFixedFrame(Graphic icon, TMP_Text label = null)
        {
            _fixedFrame = true;
            _icon = icon;
            if (icon != null) _iconColor = icon.color;
            _label = label;
            if (label != null) _labelColor = label.color;
            ApplySprite(!IsInteractable());
        }

        /// <summary>Changes the icon's normal colour (it still dims while disabled).</summary>
        public void SetIconColor(Color c)
        {
            _iconColor = c;
            if (_icon != null && !_disabledDrawn) _icon.color = c;
        }

        /// <summary>Changes the label's normal colour (it still dims while disabled).</summary>
        public void SetLabelColor(Color c)
        {
            _labelColor = c;
            if (_label != null && !_disabledDrawn) _label.color = c;
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            ApplySprite(state == SelectionState.Disabled);
        }

        void ApplySprite(bool disabled)
        {
            _disabledDrawn = disabled;
            if (_label != null) _label.color = disabled ? _labelColor * DisabledContent : _labelColor;
            if (_icon != null) _icon.color = disabled ? _iconColor * DisabledContent : _iconColor;

            var img = image;
            if (img == null || _fixedFrame) return;
            var sprite = UiTheme.Frame(disabled ? "btn_disabled" : UiTheme.ButtonSprite(_style));
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = disabled ? UiTheme.ButtonFallback(ButtonStyle.Secondary) : UiTheme.ButtonFallback(_style);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left) return;
            transform.DOKill();
            transform.DOScale(PressedScale, 0.06f).SetUpdate(true).SetLink(gameObject);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            transform.DOKill();
            transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.DOKill();
            transform.localScale = Vector3.one;
        }
    }
}
