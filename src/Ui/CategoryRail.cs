using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            BackColor = Theme.Rail;
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

        private int RowHeight { get { return ScaleValue(42); } }
        private int RowGap { get { return ScaleValue(2); } }
        private int PaddingLeft { get { return ScaleValue(12); } }
        private int PaddingRight { get { return ScaleValue(10); } }
        private int TopOffset { get { return ScaleValue(34); } }

        private int ContentHeight
        {
            get { return TopOffset + _items.Count * (RowHeight + RowGap) + ScaleValue(12); }
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
            g.Clear(Theme.Rail);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int scroll = _scroll;
            int left = PaddingLeft;
            int width = ClientSize.Width - PaddingLeft - PaddingRight;

            Ink.Text(g, "COLLECTIONS", Theme.Tiny, Theme.TextDim,
                new Rectangle(left + ScaleValue(4), scroll + ScaleValue(14), width, ScaleValue(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            for (int i = 0; i < _items.Count; i++)
            {
                int top = scroll + TopOffset + i * (RowHeight + RowGap);
                Rectangle row = new Rectangle(left, top, width, RowHeight);

                if (row.Bottom < 0 || row.Top > ClientSize.Height) continue;

                bool selected = i == _selected;
                bool hover = i == _hover;

                if (selected)
                {
                    Ink.FillRounded(g, row, ScaleValue(9), Theme.Selection);
                    Rectangle bar = new Rectangle(row.Left, row.Top + ScaleValue(11), ScaleValue(3), row.Height - ScaleValue(22));
                    Ink.FillRounded(g, bar, ScaleValue(2), Theme.Accent);
                }
                else if (hover)
                {
                    Ink.FillRounded(g, row, ScaleValue(9), Theme.Hover);
                }

                Item item = _items[i];
                Color colour = selected ? Theme.Accent : Theme.Text;

                Rectangle tile = new Rectangle(row.Left + ScaleValue(10), row.Top + (row.Height - ScaleValue(30)) / 2,
                    ScaleValue(30), ScaleValue(30));

                if (item.Category.HasValue)
                {
                    Glyphs.Tile(g, CategoryInfo.Shape(item.Category.Value), tile, true);
                }
                else
                {
                    DrawGrid(g, tile, selected ? Theme.Accent : Theme.TextMuted);
                }

                Rectangle titleRect = new Rectangle(tile.Right + ScaleValue(10), row.Top + ScaleValue(8), width - ScaleValue(56), ScaleValue(19));
                Ink.Text(g, item.Title, Theme.SmallBold, colour, titleRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                string subtitle;
                if (item.Count == 0)
                {
                    subtitle = "nothing found";
                }
                else
                {
                    subtitle = item.Count + (item.Count == 1 ? " item" : " items") + "  ·  " + Format.Bytes(item.Bytes);
                }

                Rectangle subRect = new Rectangle(tile.Right + ScaleValue(10), row.Top + ScaleValue(26), width - ScaleValue(56), ScaleValue(18));
                Ink.Text(g, subtitle, Theme.Tiny, selected ? Theme.Mix(Theme.Accent, Theme.TextMuted, 0.5) : Theme.TextDim, subRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (item.Count > 0)
                {
                    Ink.Text(g, item.Count.ToString(), Theme.Tiny, selected ? Theme.Accent : Theme.TextDim,
                        new Rectangle(row.Right - ScaleValue(40), row.Top + ScaleValue(8), ScaleValue(30), ScaleValue(18)),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }

            Ink.Scrollbar(g, ClientRectangle, _scroll, ContentHeight, ClientSize.Height);
        }

        private static void DrawGrid(Graphics g, Rectangle tile, Color colour)
        {
            Ink.FillRounded(g, tile, Math.Max(6, tile.Width / 5), Theme.Mix(colour, Theme.Rail, 0.12));

            int cell = Math.Max(3, tile.Width / 6);
            int gap = Math.Max(2, tile.Width / 10);
            int startX = tile.Left + (tile.Width - (cell * 2 + gap)) / 2;
            int startY = tile.Top + (tile.Height - (cell * 2 + gap)) / 2;

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    Rectangle square = new Rectangle(startX + col * (cell + gap), startY + row * (cell + gap), cell, cell);
                    Ink.FillRounded(g, square, Math.Max(1, cell / 2), colour);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
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
    }
}
