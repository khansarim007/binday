using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>
    /// The right-hand panel. This is where the app answers "what happens if I
    /// remove this?" before anything is ticked.
    /// </summary>
    internal sealed class Inspector : ScrollableControl
    {
        private Finding _item;
        private DeleteMode _mode = DeleteMode.Recycle;
        private bool _ticked;

        private Rectangle _tickRect;
        private Rectangle _openRect;
        private int _hot = -1;
        private int _contentHeight;
        private int _scroll;

        public Inspector()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Surface;
            AutoScroll = false;
        }

        public event EventHandler TickRequested;
        public event EventHandler OpenRequested;

        public Finding Item { get { return _item; } }

        public void Display(Finding item, DeleteMode mode, bool ticked)
        {
            _item = item;
            _mode = mode;
            _ticked = ticked;
            _scroll = 0;
            Reflow();
            Invalidate();
        }

        public void SetMode(DeleteMode mode)
        {
            _mode = mode;
            Reflow();
            Invalidate();
        }

        public void SetTicked(bool ticked)
        {
            _ticked = ticked;
            Invalidate();
        }

        private int Px(int value)
        {
            return (int)Math.Round(value * (DeviceDpi / 96f));
        }

        private void Reflow()
        {
            if (!IsHandleCreated) return;

            using (Graphics g = CreateGraphics())
            {
                _contentHeight = Measure(g);
            }

            int max = Math.Max(0, _contentHeight - ClientSize.Height);
            if (_scroll > max) _scroll = max;
            if (_scroll < 0) _scroll = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Reflow();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);

            using (Pen pen = new Pen(Theme.Line, 1f))
            {
                g.DrawLine(pen, 0, 0, 0, ClientSize.Height);
            }

            int left = Px(20);
            int width = ClientSize.Width - Px(40);
            if (width < Px(80)) return;

            int y = Px(20) + _scroll;

            if (_item == null)
            {
                PaintEmpty(g, left, width, ref y);
            }
            else
            {
                PaintItem(g, left, width, ref y);
            }

            Ink.Scrollbar(g, ClientRectangle, _scroll, _contentHeight, ClientSize.Height);
        }

        private void PaintEmpty(Graphics g, int left, int width, ref int y)
        {
            int glyphSize = Px(46);
            Rectangle glyph = new Rectangle(left, y + Px(10), glyphSize, glyphSize);
            Glyphs.Tile(g, ItemKind.Duplicate, glyph, true);

            Ink.Text(g, "Pick something to inspect", Theme.Large, Theme.Text,
                new Rectangle(left + glyphSize + Px(12), y + Px(10), width - glyphSize - Px(12), Px(24)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            Ink.Text(g, "Every item explains itself here.", Theme.Small, Theme.TextMuted,
                new Rectangle(left + glyphSize + Px(12), y + Px(35), width - glyphSize - Px(12), Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            y += glyphSize + Px(30);

            Paragraph(g, "Select any card and this panel tells you:", Theme.Small, Theme.TextMuted, left, width, ref y);
            y += Px(2);
            Bullet(g, "what the item actually is", left, width, ref y);
            Bullet(g, "what gets removed, and where", left, width, ref y);
            Bullet(g, "how to get it back afterwards", left, width, ref y);

            y += Px(22);
            Section(g, "HOW IT IS GRADED", left, width, ref y);

            Grade(g, Theme.Safe, "Safe", "A tool rebuilds it automatically. BinDay can delete it permanently.", left, width, ref y);
            Grade(g, Theme.Caution, "Check", "Rebuildable, but it may need a command or the internet.", left, width, ref y);
            Grade(g, Theme.Review, "Review", "Your own file or a setup you may still want. Recycle Bin only.", left, width, ref y);

            y += Px(18);

            using (Pen pen = new Pen(Theme.Line, 1f))
            {
                g.DrawLine(pen, left, y, left + width, y);
            }
            y += Px(14);

            Paragraph(g, "Nothing is deleted until you tick it and confirm. Every item goes to the Recycle Bin unless it is marked Safe and you choose permanent deletion.",
                Theme.Tiny, Theme.TextDim, left, width, ref y);
        }

        private void PaintItem(Graphics g, int left, int width, ref int y)
        {
            int previewSize = Px(64);
            Rectangle preview = new Rectangle(left, y, previewSize, previewSize);
            PaintPreview(g, preview);

            int textLeft = preview.Right + Px(12);
            int textWidth = width - previewSize - Px(12);

            Rectangle titleRect = new Rectangle(textLeft, y + Px(1), textWidth, Px(40));
            Ink.Text(g, _item.Title, Theme.Large, Theme.Text, titleRect,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

            string location = _item.IsDuplicateGroup ? _item.KeeperPath : _item.Path;
            Ink.Text(g, Ink.ShortPath(location, 74), Theme.Mono, Theme.TextDim,
                new Rectangle(textLeft, y + Px(42), textWidth, Px(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            y += previewSize + Px(14);

            // Pills: collection, size, safety.
            int pillX = left;
            int pillY = y;

            string collection = _item.CategoryLabel;
            Color collectionColour = Glyphs.Colour(_item.Category);
            Ink.Pill(g, collection, Theme.Tiny, new Point(pillX, pillY),
                Theme.Mix(collectionColour, Theme.Surface, 0.16), collectionColour, DeviceDpi / 96f);
            pillX += Ink.PillWidth(g, collection, Theme.Tiny, DeviceDpi / 96f) + Px(6);

            string size = Format.Bytes(_item.Bytes);
            Ink.Pill(g, size, Theme.Tiny, new Point(pillX, pillY),
                Theme.SurfaceAlt, Theme.TextMuted, DeviceDpi / 96f);
            pillX += Ink.PillWidth(g, size, Theme.Tiny, DeviceDpi / 96f) + Px(6);

            Color safety = Theme.ForSafety(_item.Level);
            Ink.Pill(g, _item.SafetyLabel, Theme.Tiny, new Point(pillX, pillY),
                Theme.Mix(safety, Theme.Surface, 0.16), safety, DeviceDpi / 96f);

            y += Px(32);

            Section(g, "WHY IT IS HERE", left, width, ref y);
            Paragraph(g, _item.Why, Theme.Small, Theme.Text, left, width, ref y);
            y += Px(10);

            if (_item.FileCount > 1)
            {
                Paragraph(g, Format.Count(_item.FileCount) + " files, last changed " + Format.Relative(_item.Modified) + ".",
                    Theme.Tiny, Theme.TextDim, left, width, ref y);
                y += Px(12);
            }

            Section(g, _mode == DeleteMode.Permanent ? "IF YOU DELETE IT PERMANENTLY" : "IF YOU REMOVE IT", left, width, ref y);
            DeleteMode mode = _mode;
            if (mode == DeleteMode.Permanent && _item.Level != Safety.Safe) mode = DeleteMode.Recycle;
            Paragraph(g, Narrative.IfYouRemove(_item, mode), Theme.Small,
                _mode == DeleteMode.Permanent ? Theme.Caution : Theme.Text, left, width, ref y);

            if (_mode == DeleteMode.Permanent && _item.Level != Safety.Safe)
            {
                y += Px(6);
                Paragraph(g, "This item is not marked Safe, so it will go to the Recycle Bin instead.",
                    Theme.Tiny, Theme.Caution, left, width, ref y);
            }

            y += Px(12);

            Section(g, "GETTING IT BACK", left, width, ref y);
            Paragraph(g, Narrative.GetItBack(_item), Theme.Small, Theme.TextMuted, left, width, ref y);

            if (!string.IsNullOrEmpty(_item.MarkerPath))
            {
                y += Px(12);
                Paragraph(g, "Project file found next to it: " + _item.MarkerPath, Theme.Tiny, Theme.TextDim, left, width, ref y);
            }

            y += Px(20);

            // Actions.
            int buttonHeight = Px(34);
            string tickText = _ticked ? "Remove from cleanup" : "Tick for cleanup";

            _tickRect = new Rectangle(left, y, Px(150), buttonHeight);
            _openRect = new Rectangle(_tickRect.Right + Px(8), y, Px(120), buttonHeight);

            Ink.FillRounded(g, _tickRect, Px(8), _ticked ? Theme.SurfaceAlt : Theme.Accent);
            Ink.OutlineRounded(g, _tickRect, Px(8), _ticked ? Theme.Line : Theme.Accent, 1f);
            Ink.Text(g, tickText, Theme.SmallBold, _ticked ? Theme.Text : Theme.AccentInk, _tickRect,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            Ink.OutlineRounded(g, _openRect, Px(8), _hot == 1 ? Theme.AccentDark : Theme.Line, 1f);
            Ink.Text(g, "Open location", Theme.Small, Theme.Text, _openRect,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            y += buttonHeight + Px(16);

            using (Pen pen = new Pen(Theme.Line, 1f))
            {
                g.DrawLine(pen, left, y, left + width, y);
            }
            y += Px(12);

            string modeText = _mode == DeleteMode.Permanent
                ? "Removal mode: Permanent delete (only Safe items)"
                : "Removal mode: Recycle Bin (restorable)";

            Ink.Text(g, modeText, Theme.Tiny, _mode == DeleteMode.Permanent ? Theme.Caution : Theme.TextDim,
                new Rectangle(left, y, width, Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void PaintPreview(Graphics g, Rectangle bounds)
        {
            Ink.FillRounded(g, bounds, Px(10), Theme.Mix(Glyphs.Colour(_item.Shape), Theme.Surface, 0.1));

            Image image = null;
            if (_item.Category == Category.Image || _item.Category == Category.Video ||
                _item.Category == Category.Installer || _item.Category == Category.AppPackage)
            {
                image = Thumbnails.Get(_item.Path, 128, delegate
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try { BeginInvoke(new Action(Invalidate)); } catch (Exception) { }
                });
            }

            using (GraphicsPath clip = Ink.Rounded(bounds, Px(10)))
            {
                Region previous = g.Clip;
                g.SetClip(clip);

                if (image != null)
                {
                    float scale = Math.Max((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
                    int w = (int)Math.Ceiling(image.Width * scale);
                    int h = (int)Math.Ceiling(image.Height * scale);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, new Rectangle(bounds.Left + (bounds.Width - w) / 2, bounds.Top + (bounds.Height - h) / 2, w, h));
                }
                else
                {
                    int size = bounds.Width / 2;
                    Glyphs.Draw(g, _item.Shape,
                        new Rectangle(bounds.Left + (bounds.Width - size) / 2, bounds.Top + (bounds.Height - size) / 2, size, size),
                        Glyphs.Colour(_item.Shape));
                }

                g.Clip = previous;
                previous.Dispose();
            }
        }

        // ------------------------------------------------------- text helpers

        private void Section(Graphics g, string caption, int left, int width, ref int y)
        {
            Size size = Ink.Measure(g, caption, Theme.Tiny, width, TextFormatFlags.SingleLine);
            Ink.Text(g, caption, Theme.Tiny, Theme.TextDim, new Rectangle(left, y, width, size.Height),
                TextFormatFlags.Left | TextFormatFlags.SingleLine);
            y += size.Height + Px(5);
        }

        private void Paragraph(Graphics g, string text, Font font, Color colour, int left, int width, ref int y)
        {
            if (string.IsNullOrEmpty(text)) return;

            Size size = Ink.Measure(g, text, font, width, TextFormatFlags.WordBreak);
            Rectangle bounds = new Rectangle(left, y, width, size.Height + Px(2));
            Ink.Text(g, text, font, colour, bounds, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            y += size.Height + Px(2);
        }

        private void Bullet(Graphics g, string text, int left, int width, ref int y)
        {
            int dot = Px(4);
            using (SolidBrush brush = new SolidBrush(Theme.Accent))
            {
                g.FillEllipse(brush, left + Px(2), y + Px(7), dot, dot);
            }

            Size size = Ink.Measure(g, text, Theme.Small, width - Px(14), TextFormatFlags.WordBreak);
            Ink.Text(g, text, Theme.Small, Theme.TextMuted,
                new Rectangle(left + Px(14), y, width - Px(14), size.Height), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            y += size.Height + Px(4);
        }

        private void Grade(Graphics g, Color colour, string name, string explanation, int left, int width, ref int y)
        {
            int dot = Px(7);
            using (SolidBrush brush = new SolidBrush(colour))
            {
                g.FillEllipse(brush, left + Px(2), y + Px(5), dot, dot);
            }

            Size nameSize = Ink.Measure(g, name, Theme.SmallBold, width, TextFormatFlags.SingleLine);
            Ink.Text(g, name, Theme.SmallBold, colour, new Rectangle(left + Px(16), y, Px(60), nameSize.Height),
                TextFormatFlags.Left | TextFormatFlags.Top);

            Size size = Ink.Measure(g, explanation, Theme.Tiny, width - Px(16), TextFormatFlags.WordBreak);
            Ink.Text(g, explanation, Theme.Tiny, Theme.TextMuted,
                new Rectangle(left + Px(16), y + nameSize.Height + Px(1), width - Px(16), size.Height),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);

            y += nameSize.Height + size.Height + Px(10);
        }

        private int Measure(Graphics g)
        {
            int width = ClientSize.Width - Px(40);
            if (width < Px(80)) return 0;

            int y = Px(20);

            if (_item == null) return Px(520);

            y += Px(64) + Px(14);   // preview + gap
            y += Px(32);            // pills

            y += Px(18);            // section label
            y += Ink.Measure(g, _item.Why, Theme.Small, width, TextFormatFlags.WordBreak).Height + Px(2);
            y += Px(10);

            if (_item.FileCount > 1)
            {
                y += Ink.Measure(g, Format.Count(_item.FileCount) + " files, last changed " + Format.Relative(_item.Modified) + ".",
                    Theme.Tiny, width, TextFormatFlags.WordBreak).Height + Px(2) + Px(12);
            }

            DeleteMode mode = _mode == DeleteMode.Permanent && _item.Level != Safety.Safe ? DeleteMode.Recycle : _mode;

            y += Px(18);
            y += Ink.Measure(g, Narrative.IfYouRemove(_item, mode), Theme.Small, width, TextFormatFlags.WordBreak).Height + Px(2);

            if (_mode == DeleteMode.Permanent && _item.Level != Safety.Safe)
            {
                y += Px(6) + Ink.Measure(g, "This item is not marked Safe, so it will go to the Recycle Bin instead.",
                    Theme.Tiny, width, TextFormatFlags.WordBreak).Height + Px(2);
            }

            y += Px(12) + Px(18);
            y += Ink.Measure(g, Narrative.GetItBack(_item), Theme.Small, width, TextFormatFlags.WordBreak).Height + Px(2);

            if (!string.IsNullOrEmpty(_item.MarkerPath))
            {
                y += Px(12) + Ink.Measure(g, "Project file found next to it: " + _item.MarkerPath, Theme.Tiny, width,
                    TextFormatFlags.WordBreak).Height + Px(2);
            }

            y += Px(20) + Px(34) + Px(16) + Px(12) + Px(18);
            return y + Px(24);
        }

        // --------------------------------------------------------------- input

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            int max = Math.Max(0, _contentHeight - ClientSize.Height);
            if (max <= 0) return;

            _scroll = Math.Max(0, Math.Min(max, _scroll - (e.Delta / 120) * 40));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Point point = new Point(e.X, e.Y - _scroll);
            int hot = -1;
            if (_tickRect.Contains(point)) hot = 0;
            else if (_openRect.Contains(point)) hot = 1;

            if (hot != _hot)
            {
                _hot = hot;
                Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != -1)
            {
                _hot = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            Point point = new Point(e.X, e.Y - _scroll);
            if (_item == null) return;

            if (_tickRect.Contains(point))
            {
                EventHandler handler = TickRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            else if (_openRect.Contains(point))
            {
                EventHandler handler = OpenRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }
    }
}
