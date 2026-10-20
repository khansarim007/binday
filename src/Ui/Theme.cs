using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BinDay.App
{
    /// <summary>One place for every colour the app uses.</summary>
    internal static class Theme
    {
        public static readonly Color Header = ColorTranslator.FromHtml("#111827");
        public static readonly Color HeaderText = Color.White;
        public static readonly Color HeaderMuted = ColorTranslator.FromHtml("#9CA3AF");
        public static readonly Color Accent = ColorTranslator.FromHtml("#34D399");
        public static readonly Color AccentDark = ColorTranslator.FromHtml("#0F766E");

        public static readonly Color Background = ColorTranslator.FromHtml("#F3F4F6");
        public static readonly Color Surface = Color.White;
        public static readonly Color Sidebar = ColorTranslator.FromHtml("#F9FAFB");

        public static readonly Color Text = ColorTranslator.FromHtml("#111827");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6B7280");
        public static readonly Color Border = ColorTranslator.FromHtml("#E5E7EB");

        public static readonly Color Safe = ColorTranslator.FromHtml("#047857");
        public static readonly Color Caution = ColorTranslator.FromHtml("#B45309");
        public static readonly Color Review = ColorTranslator.FromHtml("#9F1239");

        private static Font _body;
        private static Font _small;
        private static Font _smallBold;
        private static Font _medium;
        private static Font _large;
        private static Font _huge;
        private static Font _brand;

        /// <summary>
        /// Segoe UI is on every Windows box, but not everywhere this app can be
        /// started (Wine, trimmed down installs), so fall back to whatever the
        /// system offers instead of throwing.
        /// </summary>
        private static Font Make(float size, FontStyle style)
        {
            string[] candidates = { "Segoe UI", "Microsoft Sans Serif", "Tahoma", "Arial", "Verdana" };

            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    FontFamily family = new FontFamily(candidates[i]);
                    return new Font(family, size, style);
                }
                catch (Exception) { }
            }

            try
            {
                Font fallback = SystemFonts.DefaultFont;
                if (fallback != null) return new Font(fallback.FontFamily, size, style);
            }
            catch (Exception) { }

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
            catch (Exception) { }

            return SystemFonts.DefaultFont;
        }

        public static Font Body { get { return _body ?? (_body = Make(9.75f, FontStyle.Regular)); } }
        public static Font Small { get { return _small ?? (_small = Make(8.75f, FontStyle.Regular)); } }
        public static Font SmallBold { get { return _smallBold ?? (_smallBold = Make(8.75f, FontStyle.Bold)); } }
        public static Font Medium { get { return _medium ?? (_medium = Make(9.75f, FontStyle.Bold)); } }
        public static Font Large { get { return _large ?? (_large = Make(12f, FontStyle.Bold)); } }
        public static Font Huge { get { return _huge ?? (_huge = Make(26f, FontStyle.Bold)); } }
        public static Font Brand { get { return _brand ?? (_brand = Make(19f, FontStyle.Bold)); } }
    }

    internal static class Ui
    {
        public static Button FlatButton(string text, Color back, Color fore, bool solid)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(80, 32);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = solid ? 0 : 1;
            button.FlatAppearance.BorderColor = solid ? back : Theme.Border;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Font = Theme.Body;
            button.Padding = new Padding(12, 0, 12, 0);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        public static Label Label(string text, Font font, Color colour, bool autoSize)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = colour;
            label.AutoSize = autoSize;
            label.BackColor = Color.Transparent;
            return label;
        }

        public static void ConfigureListView(ListView list)
        {
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = true;
            list.HideSelection = false;
            list.CheckBoxes = true;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.Surface;
            list.Font = Theme.Body;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.GridLines = false;
        }

        public static void ConfigureTextBox(TextBox box)
        {
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = Theme.Body;
        }
    }
}
