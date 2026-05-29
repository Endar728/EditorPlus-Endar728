using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus.AtomicBuilder
{
    /// <summary>
    /// Nuclear Option mission editor palette: charcoal panels, bright green accents.
    /// </summary>
    public static class MissionEditorStyling
    {
        // #2c3338 — main panel / dialog background
        public static readonly Color PanelBackground = Hex("#2c3338");

        // #1e2428 — scroll areas, inactive tabs
        public static readonly Color PanelDark = Hex("#1e2428");

        public static readonly Color PanelBorder = Hex("#3d464f");

        // #e5e7eb — primary text
        public static readonly Color LabelPrimary = Hex("#e5e7eb");

        // #9ca3af — secondary / path / status
        public static readonly Color LabelSecondary = Hex("#9ca3af");

        // #4ade80 — titles, primary buttons, active highlights (exit dialog green)
        public static readonly Color AccentGreen = Hex("#4ade80");

        // #5b6b3e — muted olive (Center button, tab fill)
        public static readonly Color AccentGreenMuted = Hex("#5b6b3e");

        // Dark text on bright green buttons
        public static readonly Color TextOnGreen = Hex("#1a1f23");

        public static readonly Color TabActive = Hex("#4a5d23");

        public static readonly Color TabInactive = Hex("#1e2428");

        // #3a4249 — neutral buttons / inputs
        public static readonly Color InputFieldBackground = Hex("#3a4249");

        public static readonly Color InputFieldText = LabelPrimary;

        public static readonly Color ButtonNormal = Hex("#3a4249");

        public static readonly Color ButtonHighlighted = Hex("#4a545c");

        public static readonly Color ButtonPressed = Hex("#2a3138");

        public static readonly Color ScrollBackground = PanelDark;

        public static readonly Color RowBackground = Hex("#343c44");

        public static readonly Color RowSelected = Hex("#3d5230");

        public static Color LabelMuted => LabelSecondary;

        public static void ApplyPanelBackground(Image image)
        {
            if (image != null) image.color = PanelBackground;
        }

        public static void ApplyTabButton(Button button, bool active)
        {
            if (button == null) return;
            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = active ? TabActive : TabInactive;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.2f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            button.colors = colors;
        }

        public static Color TabLabelColor(bool active) =>
            active ? AccentGreen : LabelPrimary;

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return Color.white;
        }
    }
}
