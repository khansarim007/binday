using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>The category list on the left. Owner-drawn so it can carry counts and sizes.</summary>
    internal sealed class CategoryRail : ScrollableControl
    {
        public sealed class Item
        {
            public string Title;
            public Category? Category;
            public int Count;
            public long Bytes;
        }

        private readonly List<Item> _items = new List<Item>();
        private int _selected;
        private int _hover = -1;
        private int _scroll;

        public CategoryRail()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.Background;
            AutoScroll = false;
            TabStop = true;
        }

        public event EventHandler SelectionChanged;

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int index = Math.Max(0, Math.Min(_items.Count - 1, value));
                if (index == _selected) return;
                _selected = index;
                Invalidate();
                EventHandler handler = SelectionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public Item SelectedItem
        {
            get { return _selected >= 0 && _selected < _items.Count ? _items[_selected] : null; }
        }

        public void SetItems(List<Item> items, int keepSelected)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _selected = Math.Max(0, Math.Min(_items.Count - 1, keepSelected));
            Invalidate();
            UpdateScroll();
        }

        private int ScaleValue(int value)
        {
            return (int)Math.Round(value * (DeviceDpi / 96f));
        }

        private int RowHeight { get { return ScaleValue(48); } }
        private int RowGap { get { return ScaleValue(8); } // Increased spacing
        private int PaddingLeft { get { return ScaleValue(16); } // Increased padding
        private int PaddingRight { get { return ScaleValue(12); } // Increased padding
        private int TopOffset { get { return ScaleValue(42); } // Increased top offset
        private int ContentHeight
        {
            get { return TopOffset + _items.Count * (RowHeight + RowGap) + ScaleValue(16); }
        }

        private int ScrollMax
        {
            get { return Math.Max(0, ContentHeight - ClientSize.Height); }
        }

        private void UpdateScroll()
        {
            if (_scroll > ScrollMax) _scroll = ScrollMax;
            if (_scroll < 0) _scroll = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int scroll = _scroll;
            int left = PaddingLeft;
            int width = ClientSize.Width - PaddingLeft - PaddingRight;

            // Draw header
            int headerY = scroll + ScaleValue(18);
            Rectangle headerRect = new Rectangle(left, headerY, width, ScaleValue(24));
            using (SolidBrush brush = new SolidBrush(Theme.TextDim))
            {
                g.FillRectangle(brush, headerRect);
            }
            using (Font font = Theme.SmallBold)
            {
                Rectangle textRect = new Rectangle(left + ScaleValue(16), headerY + ScaleValue(2), width - ScaleValue(32), ScaleValue(20));
                TextRenderer.DrawText(g, "COLLECTIONS", font, textRect, Theme.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                int top = scroll + TopOffset + i * (RowHeight + RowGap);
                Rectangle row = new Rectangle(left, top, width, RowHeight);

                // Skip items that are outside the visible area
                if (row.Bottom < 0 || row.Top > ClientSize.Height) continue;

                bool selected = i == _selected;
                bool hover = i == _hover;

                // Draw selected item
                if (selected)
                {
                    using (SolidBrush selectionBrush = new SolidBrush(Theme.AccentDark))
                    {
                        g.FillRectangle(selectionBrush, row);
                    }

                    // Add accent bar
                    Rectangle accentBar = new Rectangle(row.Left, row.Top + ScaleValue(8), ScaleValue(4), row.Height - ScaleValue(10));
                    using (SolidBrush accentBrush = new SolidBrush(Theme.Accent))
                    {
                        g.FillRectangle(accentBrush, accentBar);
                    }
                }
                // Draw hover item
                else if (hover)
                {
                    using (SolidBrush hoverBrush = new SolidBrush(Theme.Hover))
                    {
                        g.FillRectangle(hoverBrush, row);
                    }
                }

                Item item = _items[i];
                Color textColor = selected ? Theme.Accent : Theme.TextDim;

                // Draw icon
                Rectangle iconRect = new Rectangle(row.Left + ScaleValue(12), row.Top + ScaleValue(10), ScaleValue(32), ScaleValue(32));
                if (item.Category.HasValue)
                {
                    Glyphs.Tile(g, CategoryInfo.Shape(item.Category.Value), iconRect, true);
                }
                else
                {
                    // Draw default icon
                    Rectangle gridTile = new Rectangle(iconRect.Left, iconRect.Top, ScaleValue(24), ScaleValue(24));
                    DrawGrid(g, gridTile, textColor);
                }

                // Draw title
                Rectangle titleRect = new Rectangle(iconRect.Right + ScaleValue(10), row.Top + ScaleValue(6), width - ScaleValue(56), ScaleValue(24));
                using (Font titleFont = Theme.MediumBold)
                {
                    g.DrawString(item.Title, titleFont, new SolidBrush(textColor), titleRect,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                // Draw subtitle
                string subtitle;
                if (item.Count == 0)
                {
                    subtitle = "nothing found";
                }
                else
                {
                    subtitle = $"{item.Count} item{(item.Count == 1 ? "" : "s")} · {Format.Bytes(item.Bytes)}";
                }

                Rectangle subtitleRect = new Rectangle(iconRect.Right + ScaleValue(10), row.Top + ScaleValue(4), width - ScaleValue(56), ScaleValue(18));
                g.DrawString(subtitle, Theme.Small, new SolidBrush(selected ? Theme.Accent : Theme.TextDim),
                    subtitleRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // Draw count badge
                if (item.Count > 0)
                {
                    string countText = $"{item.Count}";
                    Rectangle countRect = new Rectangle(row.Right - ScaleValue(48), row.Top + ScaleValue(6), ScaleValue(32), ScaleValue(18));
                    g.DrawString(countText, Theme.Tiny, selected ? Theme.Accent : Theme.TextDim,
                        countRect, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }

            // Draw scrollbar
            Ink.Scrollbar(g, ClientRectangle, _scroll, ContentHeight, ClientSize.Height);
        }

        private int HitTest(int y)
        {
            int local = y - _scroll - TopOffset;
            if (local < 0) return -1;

            int index = local / (RowHeight + RowGap);
            if (index < 0 || index >= _items.Count) return -1;

            int within = local % (RowHeight + RowGap);
            return within <= RowHeight ? index : -1;
        }

        private int HitTest(int y)
        {
            int local = y - _scroll - TopOffset;
            if (local < 0) return -1;

            int index = local / (RowHeight + RowGap);
            if (index < 0 || index >= _items.Count) return -1;

            int within = local % (RowHeight + RowGap);
            return within <= RowHeight ? index : -1;
        }

        private void OnMouseMove(MouseEventArgs e)
        {
            int index = HitTest(e.Y);
            if (index != _hover)
            {
                _hover = index;
                Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1)
            {
                _hover = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int index = HitTest(e.Y);
            if (index >= 0) SelectedIndex = index;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down) { SelectedIndex = _selected + 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { SelectedIndex = _selected - 1; e.Handled = true; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ScrollMax <= 0) return;

            int next = _scroll - (e.Delta / 120) * RowHeight;
            _scroll = Math.Max(0, Math.Min(ScrollMax, next));
            Invalidate();
        }

        private int HitTest(int y)
        {
            int local = y - _scroll - TopOffset;
            if (local < 0) return -1;

            int index = local / (RowHeight + RowGap);
            if (index < 0 || index >= _items.Count) return -1;

            int within = local % (RowHeight + RowGap);
            return within <= RowHeight ? index : -1;
        }

        private void UpdateScroll()
        {
            if (_scroll > ScrollMax) _scroll = ScrollMax;
            if (_scroll < 0) _scroll = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        private int ContentHeight
        {
            get { return TopOffset + _items.Count * (RowHeight + RowGap) + ScaleValue(16); }
        }

        private int ScrollMax
        {
            get { return Math.Max(0, ContentHeight - ClientSize.Height); }
        }
    }
}