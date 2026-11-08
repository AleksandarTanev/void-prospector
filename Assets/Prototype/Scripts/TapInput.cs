using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace VoidProspector.Prototype
{
    /// <summary>Touch or click damages the meteor under the finger. Taps on UI are ignored.</summary>
    public class TapInput : MonoBehaviour
    {
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

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

        static void Tap(Vector2 screen)
        {
            if (OverUI(screen)) return;

            var cam = ProtoBootstrap.I.Cam;
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var meteor = MeteorSpawner.I.AtPoint(world, ProtoBootstrap.I.config.tapPadding);
            if (meteor != null) meteor.Hit(ProtoGame.I.DrillDamage, true);
            else ProtoFx.I.Burst(world, new Color(0.6f, 0.8f, 1f, 0.6f), 3, 1.2f, 0.08f);
        }

        static bool OverUI(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screen };
            UiHits.Clear();
            es.RaycastAll(ped, UiHits);
            return UiHits.Count > 0;
        }
    }
}
