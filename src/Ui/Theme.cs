using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using BinDay.Core;

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

        /// <summary>Mix two colours by ratio (0 = a, 1 = b).</summary>
        public static Color Mix(Color a, Color b, double ratio)
        {
            int r = (int)Math.Round(a.R * (1 - ratio) + b.R * ratio);
            int g = (int)Math.Round(a.G * (1 - ratio) + b.G * ratio);
            int bl = (int)Math.Round(a.B * (1 - ratio) + b.B * ratio);
            return Color.FromArgb(r, g, bl);
        }

        /// <summary>Returns the colour used for a safety level.</summary>
        public static Color ForSafety(Safety level)
        {
            switch (level)
            {
                case Safety.Safe: return Safe;
                case Safety.Caution: return Caution;
                default: return Review;
            }
        }
    }

    /// <summary>Drawing primitives that keep the UI looking consistent.</summary>
    internal static class Ink
    {
        public static void FillRounded(Graphics g, Rectangle r, int radius, Color color)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath path = Rounded(r, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void OutlineRounded(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath path = Rounded(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Text(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text)) return;
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }

        public static Size Measure(Graphics g, string text, Font font, int width, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text)) return Size.Empty;
            return TextRenderer.MeasureText(g, text, font, new Size(Math.Max(1, width), int.MaxValue),
                flags | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        public static string ShortPath(string path, int max)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= max) return path;

            int keep = Math.Max(4, (max - 3) / 2);
            string head = path.Substring(0, keep);
            string tail = path.Substring(path.Length - keep);
            return head + "..." + tail;
        }

        public static void Pill(Graphics g, string text, Font font, Point location, Color back, Color fore, float scale)
        {
            int padX = (int)Math.Round(8 * scale);
            int height = (int)Math.Round(19 * scale);
            Size size = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, height),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            Rectangle bounds = new Rectangle(location.X, location.Y, size.Width + padX * 2, height);
            FillRounded(g, bounds, height / 2, back);
            Text(g, text, font, fore, bounds, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        /// <summary>A slim scrollbar that matches the app instead of the Windows one.</summary>
        public static void Scrollbar(Graphics g, Rectangle area, int offset, int content, int visible)
        {
            if (content <= visible || visible <= 0) return;

            Rectangle track = new Rectangle(area.Right - 5, area.Top + 2, 3, area.Height - 4);
            FillRounded(g, track, 2, Theme.LineSoft);

            float ratio = (float)visible / content;
            int thumbHeight = Math.Max(28, (int)(track.Height * ratio));
            int travel = track.Height - thumbHeight;
            int position = (int)((float)travel * (offset / (float)Math.Max(1, content - visible)));

            Rectangle thumb = new Rectangle(track.Left, track.Top + position, track.Width, thumbHeight);
            FillRounded(g, thumb, 2, Theme.Mix(Theme.TextDim, Theme.Surface, 0.5));
        }

        public static int PillWidth(Graphics g, string text, Font font, float scale)
        {
            Size size = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, 100),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return size.Width + (int)Math.Round(16 * scale);
        }
    }

    /// <summary>A rounded, hover-aware button. The stock WinForms button cannot be themed.</summary>
    internal sealed class PillButton : Control
    {
        public enum Kind
        {
            Solid,
            Outline,
            Ghost,
            Danger
        }

        private bool _hover;
        private bool _down;
        private Kind _kind = Kind.Outline;
        private bool _toggled;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            Height = 34;
            TabStop = true;
        }

        public Kind Style
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        /// <summary>For segmented controls: shows the button as currently chosen.</summary>
        public bool Toggled
        {
            get { return _toggled; }
            set { if (_toggled != value) { _toggled = value; Invalidate(); } }
        }

        public int Radius { get; set; }

        public bool Compact
        {
            get { return _compact; }
            set { _compact = value; Invalidate(); }
        }

        private bool _compact;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _down = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                Focus();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _down = false;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);

            float scale = DeviceDpi / 96f;
            int radius = Radius > 0 ? Radius : (int)Math.Round(8 * scale);
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            Color back;
            Color fore;
            Color border = Color.Transparent;

            switch (_kind)
            {
                case Kind.Solid:
                    back = Enabled ? Theme.Accent : Theme.Mix(Theme.Accent, Theme.Background, 0.25);
                    fore = Theme.AccentInk;
                    if (_hover) back = Theme.Mix(Color.White, back, 0.12);
                    if (_down) back = Theme.Mix(Color.Black, back, 0.12);
                    break;

                case Kind.Danger:
                    back = _hover ? Theme.Mix(Theme.Danger, Theme.Background, 0.18) : Theme.SurfaceAlt;
                    fore = Theme.Danger;
                    border = _hover ? Theme.Mix(Theme.Danger, Theme.Background, 0.6) : Theme.Line;
                    break;

                case Kind.Ghost:
                    back = _hover ? Theme.Hover : Color.Transparent;
                    fore = _hover ? Theme.Text : Theme.TextMuted;
                    if (_toggled) { back = Theme.AccentSoft; fore = Theme.Accent; }
                    break;

                default:
                    back = _toggled ? Theme.AccentSoft : (_hover ? Theme.Hover : Theme.SurfaceAlt);
                    fore = _toggled ? Theme.Accent : Theme.Text;
                    border = _toggled ? Theme.AccentDark : Theme.Line;
                    break;
            }

            if (!Enabled)
            {
                fore = Theme.TextDim;
                if (back != Color.Transparent) back = Theme.Mix(back, Theme.Background, 0.45);
            }

            if (back.A > 0) Ink.FillRounded(g, bounds, radius, back);
            if (border.A > 0) Ink.OutlineRounded(g, bounds, radius, border, 1f);

            Rectangle textBounds = ClientRectangle;
            if (_compact) textBounds = new Rectangle(textBounds.X, textBounds.Y, textBounds.Width, textBounds.Height);

            Ink.Text(g, Text, Font, fore, textBounds,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                Rectangle ring = new Rectangle(2, 2, Width - 5, Height - 5);
                Ink.OutlineRounded(g, ring, Math.Max(2, radius - 2), Theme.Mix(Theme.Accent, back, 0.7), 1f);
            }
        }
    }

    /// <summary>
    /// A dark, owner-drawn list. The stock ListView header cannot be themed, so the
    /// header and every row are painted by hand.
    /// </summary>
    internal static class DarkList
    {
        public static void Attach(ListView list)
        {
            list.OwnerDraw = true;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.BorderStyle = BorderStyle.None;
            list.GridLines = false;
            list.BackColor = Theme.Surface;
            list.ForeColor = Theme.Text;
            list.Font = Theme.Small;

            ImageList spacer = new ImageList();
            spacer.ImageSize = new Size(1, 24);
            list.SmallImageList = spacer;

            list.DrawColumnHeader += delegate (object sender, DrawListViewColumnHeaderEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(Theme.SurfaceAlt))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(0, 0, list.ClientSize.Width, e.Bounds.Height));
                }

                Rectangle bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.Tiny, bounds, Theme.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };

            list.DrawItem += delegate (object sender, DrawListViewItemEventArgs e)
            {
                e.DrawDefault = false;

                bool selected = (e.State & ListViewItemStates.Selected) == ListViewItemStates.Selected;
                if (selected)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.Selection))
                    {
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    }
                }

                Rectangle bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Item.Text, Theme.Small, bounds, e.Item.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };

            list.DrawSubItem += delegate (object sender, DrawListViewSubItemEventArgs e)
            {
                e.DrawDefault = false;

                bool selected = e.Item != null && e.Item.Selected;
                if (selected)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.Selection))
                    {
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    }
                }

                Rectangle bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.Small, bounds, e.SubItem.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
        }
    }

    /// <summary>Makes context menus dark so they do not flash white.</summary>
    internal sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.SurfaceAlt; } }
        public override Color ImageMarginGradientBegin { get { return Theme.SurfaceAlt; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.SurfaceAlt; } }
        public override Color ImageMarginGradientEnd { get { return Theme.SurfaceAlt; } }
        public override Color MenuBorder { get { return Theme.Line; } }
        public override Color MenuItemBorder { get { return Theme.AccentDark; } }
        public override Color MenuItemSelected { get { return Theme.Selection; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Selection; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Selection; } }
        public override Color SeparatorDark { get { return Theme.Line; } }
        public override Color SeparatorLight { get { return Theme.Line; } }
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
            button.FlatAppearance.BorderColor = solid ? back : Theme.Line;
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
            list.ForeColor = Theme.Text;
            list.Font = Theme.Body;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.GridLines = false;
        }

        public static void ConfigureTextBox(TextBox box)
        {
            box.BorderStyle = BorderStyle.None;
            box.Font = Theme.Body;
            box.BackColor = Theme.SurfaceAlt;
            box.ForeColor = Theme.Text;
        }

        public static ContextMenuStrip DarkMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Theme.SurfaceAlt;
            menu.ForeColor = Theme.Text;
            menu.Font = Theme.Body;
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            menu.ShowImageMargin = false;
            return menu;
        }

        public static ToolTip Tip()
        {
            ToolTip tip = new ToolTip();
            tip.BackColor = Theme.SurfaceAlt;
            tip.ForeColor = Theme.Text;
            tip.InitialDelay = 450;
            tip.ReshowDelay = 100;
            tip.AutoPopDelay = 8000;
            return tip;
        }

        public static void OpenLocation(string path, bool selectFile)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;

                if (selectFile && System.IO.File.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                    return;
                }

                string folder = System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception) { }
        }
    }
}