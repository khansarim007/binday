using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>The "nothing here yet" panel that replaces the grid when it is empty.</summary>
    internal sealed class EmptyState : Control
    {
        private string _title = "Nothing scanned yet";
        private string _body = "Pick a folder and press Scan. BinDay will look for build junk, caches, old installers, duplicates and oversized media.";
        private ItemKind _kind = ItemKind.Build;

        public EmptyState()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        public ItemKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        public void SetText(string title, string body)
        {
            _title = title;
            _body = body;
            Invalidate();
        }

        private int Px(int value)
        {
            return (int)Math.Round(value * (DeviceDpi / 96f));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.Clear(Theme.Background);

            int width = Math.Min(ClientSize.Width - Px(40), Px(420));
            if (width < Px(100)) return;

            int tile = Px(56);
            int left = (ClientSize.Width - width) / 2;
            int top = Math.Max(Px(40), ClientSize.Height / 2 - Px(120));

            Rectangle glyph = new Rectangle((ClientSize.Width - tile) / 2, top, tile, tile);
            Ink.FillRounded(g, glyph, Px(14), Theme.Mix(Glyphs.Colour(_kind), Theme.Background, 0.12));
            Ink.OutlineRounded(g, glyph, Px(14), Theme.Mix(Glyphs.Colour(_kind), Theme.Background, 0.25), 1f);

            int inner = tile / 2;
            Glyphs.Draw(g, _kind, new Rectangle(glyph.Left + (tile - inner) / 2, glyph.Top + (tile - inner) / 2, inner, inner),
                Glyphs.Colour(_kind));

            Size titleSize = Ink.Measure(g, _title, Theme.Large, width, TextFormatFlags.WordBreak);
            Ink.Text(g, _title, Theme.Large, Theme.Text,
                new Rectangle(left, glyph.Bottom + Px(16), width, titleSize.Height),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);

            Size bodySize = Ink.Measure(g, _body, Theme.Small, width, TextFormatFlags.WordBreak);
            Ink.Text(g, _body, Theme.Small, Theme.TextMuted,
                new Rectangle(left, glyph.Bottom + Px(16) + titleSize.Height + Px(6), width, bodySize.Height),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>A thin indeterminate progress line used while a scan runs.</summary>
    internal sealed class PulseBar : Control
    {
        private readonly Timer _timer;
        private int _phase;

        public PulseBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Surface;
            _timer = new Timer();
            _timer.Interval = 33;
            _timer.Tick += delegate
            {
                _phase = (_phase + 7) % Math.Max(1, Width + 80);
                Invalidate();
            };
        }

        public bool Running
        {
            get { return _timer.Enabled; }
            set
            {
                _timer.Enabled = value;
                Visible = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            Rectangle track = new Rectangle(0, Math.Max(0, Height / 2 - 2), Width, 4);
            Ink.FillRounded(g, track, 2, Theme.Line);

            if (!Visible) return;

            int segment = Math.Max(30, Width / 4);
            int x = (_phase % Math.Max(1, Width + segment)) - segment;
            Rectangle bar = new Rectangle(x, track.Top, Math.Min(segment, Width - x), track.Height);
            if (bar.Left < 0)
            {
                bar = new Rectangle(0, track.Top, Math.Min(segment + x, Width), track.Height);
            }

            if (bar.Width > 0) Ink.FillRounded(g, bar, 2, Theme.Accent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}

namespace BinDay.App
{
    /// <summary>A labelled on/off switch. CheckBox cannot be themed to match the app.</summary>
    internal sealed class ToggleRow : Control
    {
        private bool _checked;
        private bool _hover;

        public ToggleRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Height = 44;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public event EventHandler CheckedChanged;

        public string Detail { get; set; }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private int Px(int value)
        {
            return (int)Math.Round(value * (DeviceDpi / 96f));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(_hover ? Theme.Hover : Theme.Surface);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int switchWidth = Px(42);
            int switchHeight = Px(24);
            int right = Width - Px(4);
            Rectangle track = new Rectangle(right - switchWidth, (Height - switchHeight) / 2, switchWidth, switchHeight);

            Color trackColour = _checked ? Theme.AccentDark : Theme.Line;
            Ink.FillRounded(g, track, switchHeight / 2, trackColour);

            int knob = switchHeight - Px(6);
            int knobX = _checked ? track.Right - knob - Px(3) : track.Left + Px(3);
            Rectangle knobRect = new Rectangle(knobX, track.Top + Px(3), knob, knob);

            using (System.Drawing.Drawing2D.GraphicsPath path = Ink.Rounded(knobRect, knob / 2))
            using (SolidBrush brush = new SolidBrush(_checked ? Theme.Accent : Theme.TextMuted))
            {
                g.FillPath(brush, path);
            }

            int textWidth = Math.Max(Px(40), track.Left - Px(12));

            Ink.Text(g, Text, Theme.SmallBold, Theme.Text,
                new Rectangle(0, Px(6), textWidth, Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrEmpty(Detail))
            {
                Ink.Text(g, Detail, Theme.Tiny, Theme.TextDim,
                    new Rectangle(0, Px(23), textWidth, Px(16)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }
    }
}
