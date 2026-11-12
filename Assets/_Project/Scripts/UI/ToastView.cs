using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoidProspector.UI
{
    /// <summary>
    /// One toast at a time under the top bar: a framed pill that fits its text, slides down, holds, then fades.
    /// A new toast replaces the current one. Unscaled time, so hit-stop doesn't freeze it.
    /// </summary>
    public class ToastView : MonoBehaviour
    {
        const float Hold = 1.4f;
        const float SlideFrom = 40f;

        RectTransform _rt;
        CanvasGroup _group;
        TextMeshProUGUI _text;
        Image _frame;
        float _y;
        Sequence _seq;

        public static ToastView Create(RectTransform canvas, float top)
        {
            var frame = UiFactory.Frame("Toast", canvas, UiTheme.Row, UiTheme.PanelFallback, 0.8f);
            frame.raycastTarget = false;
            var rt = frame.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -top);

            var layout = frame.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 22, 26);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = frame.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var view = frame.gameObject.AddComponent<ToastView>();
            view._rt = rt;
            view._frame = frame;
            view._y = -top;
            view._group = frame.gameObject.AddComponent<CanvasGroup>();
            view._group.alpha = 0;
            view._group.blocksRaycasts = false;
            view._text = UiFactory.Text(rt, "Text", "", 44, TextAlignmentOptions.Center, Color.white);
            view._text.fontStyle = FontStyles.Bold;
            return view;
        }

        public void Show(string message, Color color)
        {
            _text.text = message;
            _text.color = color;
            // The frame edge picks up a hint of the message colour.
            if (_frame.sprite != null) _frame.color = Color.Lerp(Color.white, color, 0.35f);

            _seq?.Kill();
            _rt.anchoredPosition = new Vector2(0, _y + SlideFrom);
            _group.alpha = 0;
            _seq = DOTween.Sequence()
                .Append(_rt.DOAnchorPosY(_y, 0.22f).SetEase(Ease.OutBack))
                .Join(_group.DOFade(1f, 0.15f))
                .AppendInterval(Hold)
                .Append(_group.DOFade(0f, 0.35f))
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
