using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BinDay.App
{
    /// <summary>
    /// One place for every colour, font and small drawing helper.
    /// </summary>
    internal static class Theme
    {
        // Lighter, more vibrant color scheme to match heyclicky.com
        public static readonly Color Background = ColorTranslator.FromHtml("#F8FAFC");  // Very light gray
        public static readonly Color Header = ColorTranslator.FromHtml("#1E293B");      // Darker blue-gray for header
        public static readonly Color Surface = ColorTranslator.FromHtml("#FFFFFF");       // Pure white
        public static readonly Color SurfaceAlt = ColorTranslator.FromHtml("#F1F5F9");   // Very light blue-gray
        public static readonly Color Card = ColorTranslator.FromHtml("#FFFFFF");       // White cards
        public static readonly Color CardHover = ColorTranslator.FromHtml("#E2E8F0");   // Light gray hover
        public static readonly Color Rail = ColorTranslator.FromHtml("#CBD5E0");       // Light blue-gray
        public static readonly Color Line = ColorTranslator.FromHtml("#94A3B8");       // Muted blue-gray
        public static readonly Color LineSoft = ColorTranslator.FromHtml("#A0AEC0");    // Softer muted color
        
        // Text colors
        public static readonly Color Text = ColorTranslator.FromHtml("#0F172A");       // Dark gray (almost black)
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#64748B");   // Medium gray
        public static readonly Color TextDim = ColorTranslator.FromHtml("#475569");     // Darker gray
        
        // Accent colors - more vibrant and varied
        public static readonly Color Accent = ColorTranslator.FromHtml("#10B981");     // Emerald green
        public static readonly Color AccentDark = ColorTranslator.FromHtml("#059669");   // Darker emerald
        public static readonly Color AccentInk = ColorTranslator.FromHtml("#0284C7");   // Blue
        public static readonly Color AccentSoft = ColorTranslator.FromHtml("#0EA5E9");   // Bright blue
        
        // Status colors
        public static readonly Color Safe = ColorTranslator.FromHtml("#10B981");      // Emerald green
        public static readonly Color Caution = ColorTranslator.FromHtml("#F59E0B");    // Amber
        public static readonly Color Review = ColorTranslator.FromHtml("#EF4444");    // Red
        public static readonly Color Danger = ColorTranslator.FromHtml("#DC2626");    // Red
        
        // Selection and interaction colors
        public static readonly Color Selection = ColorTranslator.FromHtml("#059669");   // Darker emerald
        public static readonly Color Hover = ColorTranslator.FromHtml("#64748B");     // Muted blue-gray
        
        // Fonts
        private static Font _body;
        private static Font _small;
        private static Font _smallBold;
        private static Font _tiny;
        private static Font _medium;
        private static Font _mediumBold;
        private static Font _large;
        private static Font _huge;
        private static Font _brand;
        private static Font _mono;

        /// <summary>
        /// Segoe UI is on every Windows box, but not everywhere this app can be
        /// started (Wine, trimmed down installs), so fall back to whatever the
        /// system offers instead of throwing.
        /// </summary>
        private static Font Make(float size, FontStyle style)
        {
            return Make(new[] { "Segoe UI", "Microsoft Sans Serif", "Tahoma", "Arial", "Verdana" }, size, style);
        }

        private static Font Make(string[] candidates, float size, FontStyle style)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    FontFamily family = new FontFamily(candidates[i]);
                    return new Font(family, size, style);
                }
                catch (Exception)
                {
                    continue;
                }
            }
            
            try
            {
                Font fallback = SystemFonts.DefaultFont;
                if (fallback != null) return new Font(fallback.FontFamily, size, style);
            }
            catch (Exception)
            {
            }
            
            try
            {
                using (InstalledFontCollection installed = new InstalledFontCollection())
                {
                    if (installed.Families.Length > 0)
                    {
                        return new Font(installed.Families[0], size, style);
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return SystemFonts.DefaultFont;
        }

        public static Font Body { get { return _body ?? (_body = Make(10f, FontStyle.Regular)); } }
        public static Font Small { get { return _small ?? (_small = Make(9f, FontStyle.Regular)); } }
        public static Font SmallBold { get { return _smallBold ?? (_smallBold = Make(10f, FontStyle.Bold)); } }
        public static Font Tiny { get { return _tiny ?? (_tiny = Make(8f, FontStyle.Regular)); } }
        public static Font Medium { get { return _medium ?? (_medium = Make(11f, FontStyle.Bold)); } }
        public static Font MediumBold { get { return _mediumBold ?? (_mediumBold = Make(11f, FontStyle.Bold)); } }
        public static Font Large { get { return _large ?? (_large = Make(14f, FontStyle.Bold)); } }
        public static Font Huge { get { return _huge ?? (_huge = Make(24f, FontStyle.Bold)); } }
        public static Font Brand { get { return _brand ?? (_brand = Make(16f, FontStyle.Bold)); } }
        public static Font Mono { get { return _mono ?? (_mono = Make(9f, FontStyle.Regular)); } }
    }
}