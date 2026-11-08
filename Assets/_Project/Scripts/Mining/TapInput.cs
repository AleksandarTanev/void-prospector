using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VoidProspector.Core;

namespace VoidProspector.Mining
{
    /// <summary>Each new touch (or mouse click in the editor) damages the meteor under it. Taps on UI are ignored.</summary>
    public class TapInput : MonoBehaviour
    {
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
        GameRoot _root;

        public void Init(GameRoot root) => _root = root;

        void Update()
        {
            bool anyTouch = false;
            var touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (var t in touch.touches)
                {
                    if (!t.press.wasPressedThisFrame) continue;
                    anyTouch = true;
                    Tap(t.position.ReadValue());
                }
            }

            var mouse = Mouse.current;
            if (!anyTouch && mouse != null && mouse.leftButton.wasPressedThisFrame)
                Tap(mouse.position.ReadValue());
        }

        void Tap(Vector2 screen)
        {
            if (OverUI(screen)) return;

            var cam = _root.Cam;
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var meteor = _root.Meteors.AtPoint(world, _root.Session.Mining.tapPadding);
            if (meteor != null) meteor.Hit(_root.Session.DrillDamage, true);
            else _root.Fx.Burst(world, new Color(0.6f, 0.8f, 1f, 0.6f), 3, 1.2f, 0.08f);
        }

        static bool OverUI(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            UiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screen }, UiHits);
            return UiHits.Count > 0;
        }
    }
}
