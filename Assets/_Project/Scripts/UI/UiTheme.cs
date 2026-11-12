using System.Collections.Generic;
using UnityEngine;

namespace VoidProspector.UI
{
    /// <summary>
    /// The UI's look: palette and sprites. Sprites live in <c>Resources/UI/</c> (Frames, Icons, Minerals), cut from the
    /// generated sheets by <c>tools/ui/slice_ui.py</c>. A missing sprite returns null and the factory falls back to
    /// a flat colour, so the game still runs.
    /// </summary>
    public static class UiTheme
    {
        // Palette (matches the frame art).
        public static readonly Color Text = new Color(0.92f, 0.96f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.72f, 0.86f);
        public static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);
        public static readonly Color Accent = new Color(0.31f, 0.85f, 1f);       // #4FD8FF, frame edges
        public static readonly Color Credits = new Color(1f, 0.85f, 0.4f);
        public static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        public static readonly Color Good = new Color(0.55f, 0.95f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.45f);
        public static readonly Color Refine = new Color(0.85f, 0.75f, 1f);
        public static readonly Color Dim = new Color(0.01f, 0.02f, 0.05f, 0.78f);
        public static readonly Color PanelFallback = new Color(0.07f, 0.1f, 0.17f, 0.97f);

        // Frame sprite names (Resources/UI/Frames).
        public const string Panel = "panel";
        public const string Row = "row";
        public const string Header = "header";
        public const string Card = "card";
        public const string Strip = "strip";
        public const string TabBar = "tabbar";
        public const string TabSelected = "tab_selected";
        public const string BarTrack = "bar_track";
        public const string BarFill = "bar_fill_white";   // white copy of the fill art, tinted per bar
        public const string IconButton = "icon_btn";
        public const string IconButtonSelected = "icon_btn_selected";

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Frame(string name) => Load("UI/Frames/" + name);
        public static Sprite Icon(string name) => Load("UI/Icons/" + name);

        /// <summary>The chunk icon for a mineral id; minerals without art get the plain gem.</summary>
        public static Sprite Mineral(string id) => Load("UI/Minerals/" + id) ?? Load("UI/Minerals/gem");

        /// <summary>Icon per upgrade id (UpgradeDefinition.id).</summary>
        public static Sprite UpgradeIcon(string upgradeId)
        {
            switch (upgradeId)
            {
                case "drill_damage": return Icon("drill");
                case "auto_fire": return Icon("autofire");
                case "crit_chance": return Icon("crit");
                case "collector_radius": return Icon("magnet");
                case "cargo_capacity": return Icon("cargo_plus");
                case "drone_count": return Icon("drone_plus");
                case "drone_damage": return Icon("drone_damage");
                case "drone_targeting": return Icon("drone_target");
                case "scanner": return Icon("scanner");
                case "refinery_batch": return Icon("ingots");
                case "refinery_speed": return Icon("refine_speed");
                case "refinery_stabiliser": return Icon("stabiliser");
                case "refine_success": return Icon("refine_success");
                default: return Icon("upgrades");
            }
        }

        public static string ButtonSprite(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Confirm: return "btn_confirm";
                case ButtonStyle.Gold: return "btn_gold";
                case ButtonStyle.Secondary: return "btn_secondary";
                default: return "btn_primary";
            }
        }

        /// <summary>Flat colour used when a button's sprite is missing.</summary>
        public static Color ButtonFallback(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Confirm: return new Color(0.18f, 0.5f, 0.3f);
                case ButtonStyle.Gold: return new Color(0.65f, 0.5f, 0.15f);
                case ButtonStyle.Secondary: return new Color(0.22f, 0.24f, 0.32f);
                default: return new Color(0.16f, 0.36f, 0.56f);
            }
        }

        static Sprite Load(string path)
        {
            if (Cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            Cache[path] = s;
            return s;
        }
    }

    public enum ButtonStyle { Primary, Confirm, Gold, Secondary }
}
