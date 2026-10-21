using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    internal enum GridView
    {
        Large = 0,
        Medium = 1,
        List = 2,
        Details = 3
    }

    internal enum SortField
    {
        Size = 0,
        Name = 1,
        Changed = 2,
        Safety = 3,
        Files = 4,
        Category = 5
    }

    /// <summary>
    /// The item surface: big cards, medium cards, a compact list or a details table.
    /// Owner-drawn so ticking, selection, thumbnails and safety colours all match.
    /// </summary>
    internal sealed class FindingGrid : ScrollableControl
    {
        private readonly List<Finding> _items = new List<Finding>();
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action _invalidate;

        private GridView _view = GridView.Large;
        private SortField _sort = SortField.Size;
        private bool _descending = true;

        private int _hover = -1;
        private int _focus = -1;
        private int _scroll;
        private int _anchor = -1;
        private bool _dragging;

        private const int CheckNone = 0;
        private const int CheckOn = 1;
        private const int CheckOff = 2;

        public FindingGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.Background;
            AutoScroll = false;
            TabStop = true;
            _invalidate = new Action(Invalidate);
        }

        public event EventHandler SelectionChanged;
        public event EventHandler CheckChanged;
        public event EventHandler Activated;
        public event EventHandler<SortField> SortRequested;

        public HashSet<string> Checked = new HashSet<string>(StringComparer.Ordinal);

        public GridView ViewMode
        {
            get { return _view; }
            set
            {
                if (_view == value) return;
                _view = value;
                UpdateScroll();
                Invalidate();
            }
        }

        public SortField Sort
        {
            get { return _sort; }
            set { _sort = value; Invalidate(); }
        }

        public bool SortDescending
        {
            get { return _descending; }
            set { _descending = value; Invalidate(); }
        }

        public int Count { get { return _items.Count; } }

        public Finding Primary
        {
            get
            {
                if (_focus < 0 || _focus >= _items.Count) return null;
                return _items[_focus];
            }
        }

        public List<Finding> SelectedFindings
        {
            get
            {
                List<Finding> list = new List<Finding>();
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_selected.Contains(_items[i].Id)) list.Add(_items[i]);
                }
                return list;
            }
        }

        public void SetItems(List<Finding> items, bool keepSelection)
        {
            string previous = Primary != null ? Primary.Id : null;

            _items.Clear();
            if (items != null) _items.AddRange(items);

            if (!keepSelection)
            {
                _selected.Clear();
                _focus = -1;
                _anchor = -1;
            }
            else
            {
                HashSet<string> live = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < _items.Count; i++) live.Add(_items[i].Id);

                _selected.RemoveWhere(delegate (string id) { return !live.Contains(id); });

                _focus = -1;
                if (previous != null)
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].Id == previous) { _focus = i; break; }
                    }
                }
            }

            _hover = -1;
            UpdateScroll();
            Invalidate();
        }

        public void SelectOnly(Finding finding)
        {
            _selected.Clear();
            if (finding != null) _selected.Add(finding.Id);
            _focus = IndexOf(finding);
            _anchor = _focus;
            Invalidate();
            RaiseSelection();
        }

        public void ClearSelection()
        {
            _selected.Clear();
            _focus = -1;
            _anchor = -1;
            Invalidate();
            RaiseSelection();
        }

        public void ToggleChecked(Finding finding)
        {
            if (finding == null) return;
            if (Checked.Contains(finding.Id)) Checked.Remove(finding.Id);
            else Checked.Add(finding.Id);
            Invalidate();
            RaiseCheck();
        }

        public void SetChecked(IEnumerable<Finding> findings, bool value)
        {
            if (findings == null) return;
            foreach (Finding finding in findings)
            {
                if (value) Checked.Add(finding.Id);
                else Checked.Remove(finding.Id);
            }
            Invalidate();
            RaiseCheck();
        }

        public void ClearChecks()
        {
            Checked.Clear();
            Invalidate();
            RaiseCheck();
        }

        public void FocusFirst()
        {
            if (_items.Count == 0) return;
            _focus = 0;
            _selected.Clear();
            _selected.Add(_items[0].Id);
            _anchor = 0;
            Invalidate();
            RaiseSelection();
        }

        // ------------------------------------------------------------- layout

        private int Px(int value)
        {
            return (int)Math.Round(value * (DeviceDpi / 96f));
        }

        private int OuterPad { get { return Px(16); } }
        private int CardGap { get { return _view == GridView.Large ? Px(12) : Px(10); } }
        private int CardWidth { get { return _view == GridView.Large ? Px(212) : Px(160); } }
        private int CardHeight { get { return _view == GridView.Large ? Px(176) : Px(146); } }
        private int RowHeight { get { return _view == GridView.List ? Px(54) : Px(32); } }
        private int HeaderHeight { get { return Px(34); } }

        private int ScrollY
        {
            get { return _scroll; }
        }

        private int ScrollMax
        {
            get { return Math.Max(0, ContentHeight - ClientSize.Height); }
        }

        private int ColumnCount
        {
            get
            {
                int width = Math.Max(1, ClientSize.Width - OuterPad * 2);
                return Math.Max(1, (width + CardGap) / (CardWidth + CardGap));
            }
        }

        private int ContentHeight
        {
            get
            {
                if (_view == GridView.Details) return HeaderHeight + _items.Count * RowHeight + OuterPad;
                if (_view == GridView.List) return _items.Count * RowHeight + OuterPad;

                int columns = ColumnCount;
                int rows = (_items.Count + columns - 1) / columns;
                return OuterPad * 2 + rows * (CardHeight + CardGap);
            }
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

        private Rectangle CardBounds(int index)
        {
            int columns = ColumnCount;
            int row = index / columns;
            int col = index % columns;

            return new Rectangle(
                OuterPad + col * (CardWidth + CardGap),
                OuterPad + row * (CardHeight + CardGap) + ScrollY,
                CardWidth,
                CardHeight);
        }

        private Rectangle RowBounds(int index, int height)
        {
            return new Rectangle(0, HeaderHeight + index * height + ScrollY, ClientSize.Width, height);
        }

        private int IndexAt(Point point)
        {
            if (_items.Count == 0) return -1;

            int y = point.Y - ScrollY;

            if (_view == GridView.Details)
            {
                if (y < HeaderHeight) return -1;
                int index = (y - HeaderHeight) / RowHeight;
                return index >= 0 && index < _items.Count ? index : -1;
            }

            if (_view == GridView.List)
            {
                int index = y / RowHeight;
                return index >= 0 && index < _items.Count ? index : -1;
            }

            int columns = ColumnCount;
            int localY = y - OuterPad;
            if (localY < 0) return -1;

            int row = localY / (CardHeight + CardGap);
            int withinY = localY % (CardHeight + CardGap);
            if (withinY > CardHeight) return -1;

            int localX = point.X - OuterPad;
            if (localX < 0) return -1;

            int col = localX / (CardWidth + CardGap);
            int withinX = localX % (CardWidth + CardGap);
            if (withinX > CardWidth) return -1;

            int index2 = row * columns + col;
            return index2 >= 0 && index2 < _items.Count ? index2 : -1;
        }

        private Rectangle CheckBounds(int index)
        {
            int size = Px(19);

            if (_view == GridView.Details || _view == GridView.List)
            {
                Rectangle row = RowBounds(index, RowHeight);
                return new Rectangle(row.Left + Px(14), row.Top + (row.Height - size) / 2, size, size);
            }

            Rectangle card = CardBounds(index);
            return new Rectangle(card.Right - size - Px(10), card.Top + Px(10), size, size);
        }

        private int HitCheck(Point point)
        {
            int index = IndexAt(point);
            if (index < 0) return -1;
            return CheckBounds(index).Contains(point) ? index : -1;
        }

        private int IndexOf(Finding finding)
        {
            if (finding == null) return -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id == finding.Id) return i;
            }
            return -1;
        }

        // -------------------------------------------------------------- paint

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_items.Count == 0) return;

            if (_view == GridView.Details)
            {
                PaintDetails(g);
                Ink.Scrollbar(g, ClientRectangle, _scroll, ContentHeight, ClientSize.Height);
                return;
            }

            int first;
            int last;
            VisibleRange(out first, out last);

            for (int i = first; i <= last; i++)
            {
                if (_view == GridView.List) PaintListRow(g, i);
                else PaintCard(g, i);
            }

            Ink.Scrollbar(g, ClientRectangle, _scroll, ContentHeight, ClientSize.Height);
        }

        private void VisibleRange(out int first, out int last)
        {
            if (_view == GridView.Details || _view == GridView.List)
            {
                int start = Math.Max(0, (-ScrollY + HeaderHeight) / RowHeight - 1);
                int end = Math.Min(_items.Count - 1, (-ScrollY + ClientSize.Height) / RowHeight + 1);
                first = start;
                last = end;
                return;
            }

            int columns = ColumnCount;
            int perRow = CardHeight + CardGap;
            int top = Math.Max(0, (-ScrollY - OuterPad) / perRow - 1);
            int bottom = Math.Min((_items.Count + columns - 1) / columns - 1, (-ScrollY + ClientSize.Height) / perRow + 1);

            first = Math.Max(0, top * columns);
            last = Math.Min(_items.Count - 1, (bottom + 1) * columns - 1);
        }

        private void PaintCard(Graphics g, int index)
        {
            Finding item = _items[index];
            Rectangle card = CardBounds(index);
            if (card.Bottom < 0 || card.Top > ClientSize.Height) return;

            bool selected = _selected.Contains(item.Id);
            bool hover = index == _hover;

            Ink.FillRounded(g, card, Px(12), selected ? Theme.Selection : (hover ? Theme.CardHover : Theme.Card));
            Ink.OutlineRounded(g, card, Px(12), selected ? Theme.AccentDark : Theme.LineSoft, selected ? 1.6f : 1f);

            int pad = Px(10);
            int checkSize = Px(19);
            Rectangle thumb = new Rectangle(card.Left + pad, card.Top + pad, card.Width - pad * 2, card.Height - Px(78));

            PaintPreview(g, item, thumb, Px(9), checkSize, index);

            int textLeft = card.Left + pad + Px(2);
            int textWidth = card.Width - pad * 2 - Px(4);

            Rectangle titleRect = new Rectangle(textLeft, thumb.Bottom + Px(9), textWidth, Px(19));
            Ink.Text(g, item.Title, Theme.SmallBold, Theme.Text, titleRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            Rectangle pathRect = new Rectangle(textLeft, titleRect.Bottom + Px(1), textWidth, Px(16));
            Ink.Text(g, ShortLocation(item), Theme.Tiny, Theme.TextDim, pathRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            Rectangle metaRect = new Rectangle(textLeft, pathRect.Bottom + Px(4), textWidth, Px(17));
            PaintMeta(g, item, metaRect);
        }

        private void PaintPreview(Graphics g, Finding item, Rectangle bounds, int radius, int checkSize, int index)
        {
            Ink.FillRounded(g, bounds, radius, Theme.Mix(Glyphs.Colour(item.Shape), Theme.Card, 0.08));

            Image preview = LoadPreview(item, Math.Max(bounds.Width, bounds.Height));
            bool drewImage = false;

            using (GraphicsPath clip = Ink.Rounded(bounds, radius))
            {
                Region previous = g.Clip;
                g.SetClip(clip);
                if (preview != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(preview, CoverRect(preview, bounds));
                    drewImage = true;
                }
                else
                {
                    int glyphSize = Math.Min(bounds.Width, bounds.Height) / 2;
                    Rectangle glyph = new Rectangle(bounds.Left + (bounds.Width - glyphSize) / 2,
                        bounds.Top + (bounds.Height - glyphSize) / 2, glyphSize, glyphSize);
                    Glyphs.Draw(g, item.Shape, glyph, Glyphs.Colour(item.Shape));
                }
                g.Clip = previous;
                previous.Dispose();
            }

            if (drewImage)
            {
                // A soft scrim so the tick stays readable over bright photos.
                using (LinearGradientBrush scrim = new LinearGradientBrush(
                    new Rectangle(bounds.Left, bounds.Top, bounds.Width, Math.Max(1, bounds.Height / 3)),
                    Color.FromArgb(120, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Vertical))
                {
                    using (GraphicsPath clip = Ink.Rounded(bounds, radius))
                    {
                        Region previous = g.Clip;
                        g.SetClip(clip);
                        g.FillRectangle(scrim, new Rectangle(bounds.Left, bounds.Top, bounds.Width, Math.Max(1, bounds.Height / 3)));
                        g.Clip = previous;
                        previous.Dispose();
                    }
                }
            }

            PaintCheck(g, index, new Rectangle(bounds.Right - checkSize - Px(7), bounds.Top + Px(7), checkSize, checkSize));
        }

        private void PaintCheck(Graphics g, int index, Rectangle bounds)
        {
            Finding item = _items[index];
            bool ticked = Checked.Contains(item.Id);
            bool hover = index == _hover;
            int radius = bounds.Width / 2;
            bool round = _view != GridView.Details;

            Color fill;
            Color border;

            if (ticked)
            {
                fill = Theme.Accent;
                border = Theme.Accent;
            }
            else
            {
                fill = Color.FromArgb(170, Theme.Background);
                border = hover ? Theme.Accent : Theme.Mix(Theme.TextDim, Theme.Card, 0.65);
            }

            if (round)
            {
                using (GraphicsPath path = Ink.Rounded(bounds, radius))
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                using (GraphicsPath path = Ink.Rounded(bounds, radius))
                using (Pen pen = new Pen(border, 1.4f))
                {
                    g.DrawPath(pen, path);
                }
            }
            else
            {
                Ink.FillRounded(g, bounds, Px(4), fill);
                Ink.OutlineRounded(g, bounds, Px(4), border, 1.3f);
            }

            if (ticked)
            {
                Rectangle tick = new Rectangle(bounds.Left + bounds.Width / 4, bounds.Top + bounds.Height / 4,
                    bounds.Width / 2, bounds.Height / 2);
                Glyphs.Tick(g, tick, Theme.AccentInk, Math.Max(1.6f, bounds.Width / 10f));
            }
        }

        private void PaintMeta(Graphics g, Finding item, Rectangle bounds)
        {
            string size = Format.Bytes(item.Bytes);
            Size sizeText = Ink.Measure(g, size, Theme.Tiny, bounds.Width, TextFormatFlags.SingleLine);
            Ink.Text(g, size, Theme.Tiny, Theme.TextMuted,
                new Rectangle(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            Color safety = Theme.ForSafety(item.Level);
            string label = item.SafetyLabel;

            int pillWidth = Ink.PillWidth(g, label, Theme.Tiny, DeviceDpi / 96f);
            int x = bounds.Right - pillWidth;

            Ink.Pill(g, label, Theme.Tiny, new Point(x, bounds.Top + (bounds.Height - Px(19)) / 2),
                Theme.Mix(safety, Theme.Card, 0.16), safety, DeviceDpi / 96f);
        }

        private void PaintListRow(Graphics g, int index)
        {
            Finding item = _items[index];
            Rectangle row = RowBounds(index, RowHeight);

            bool selected = _selected.Contains(item.Id);
            bool hover = index == _hover;

            Rectangle pill = new Rectangle(Px(8), row.Top + Px(4), row.Width - Px(16), row.Height - Px(8));
            if (selected) Ink.FillRounded(g, pill, Px(8), Theme.Selection);
            else if (hover) Ink.FillRounded(g, pill, Px(8), Theme.Hover);

            PaintCheck(g, index, CheckBounds(index));

            int tileSize = Px(32);
            Rectangle tile = new Rectangle(Px(46), row.Top + (row.Height - tileSize) / 2, tileSize, tileSize);
            Glyphs.Tile(g, item.Shape, tile, false);

            int right = row.Width - Px(16);
            int safetyWidth = Ink.PillWidth(g, item.SafetyLabel, Theme.Tiny, DeviceDpi / 96f);
            int sizeWidth = Px(78);

            Rectangle sizeRect = new Rectangle(right - sizeWidth, row.Top, sizeWidth, row.Height);
            Ink.Text(g, Format.Bytes(item.Bytes), Theme.Small, Theme.TextMuted, sizeRect,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            int pillX = right - sizeWidth - safetyWidth - Px(8);
            Ink.Pill(g, item.SafetyLabel, Theme.Tiny,
                new Point(pillX, row.Top + (row.Height - Px(19)) / 2),
                Theme.Mix(Theme.ForSafety(item.Level), Theme.Background, 0.16), Theme.ForSafety(item.Level), DeviceDpi / 96f);

            int textLeft = tile.Right + Px(12);
            int textWidth = Math.Max(Px(80), pillX - textLeft - Px(12));

            Rectangle titleRect = new Rectangle(textLeft, row.Top + Px(9), textWidth, Px(19));
            Ink.Text(g, item.Title, Theme.SmallBold, Theme.Text, titleRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            Rectangle pathRect = new Rectangle(textLeft, row.Top + Px(27), textWidth, Px(17));
            Ink.Text(g, ShortLocation(item), Theme.Tiny, Theme.TextDim, pathRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void PaintDetails(Graphics g)
        {
            int scroll = ScrollY;
            int y = scroll + HeaderHeight;

            // Sticky header.
            Rectangle header = new Rectangle(0, 0, ClientSize.Width, HeaderHeight);
            using (SolidBrush brush = new SolidBrush(Theme.Surface))
            {
                g.FillRectangle(brush, header);
            }

            int[] widths = DetailColumns();
            string[] titles = { "Name", "Collection", "Size", "Changed", "Safety" };

            int x = Px(40);
            for (int i = 0; i < titles.Length; i++)
            {
                Rectangle cell = new Rectangle(x, 0, widths[i], HeaderHeight);
                SortField field = DetailField(i);
                bool active = field == _sort;

                Ink.Text(g, titles[i], Theme.Tiny, active ? Theme.Accent : Theme.TextDim, cell,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (active)
                {
                    Rectangle chevron = new Rectangle(cell.Left + Ink.Measure(g, titles[i], Theme.Tiny, 1000, TextFormatFlags.SingleLine).Width + Px(5),
                        0, Px(12), HeaderHeight);
                    Glyphs.Chevron(g, chevron, Theme.Accent, 1.4f, _descending);
                }

                x += widths[i];
            }

            using (Pen pen = new Pen(Theme.Line, 1f))
            {
                g.DrawLine(pen, 0, HeaderHeight - 1, ClientSize.Width, HeaderHeight - 1);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                Rectangle row = new Rectangle(0, y + i * RowHeight, ClientSize.Width, RowHeight);
                if (row.Bottom < HeaderHeight || row.Top > ClientSize.Height) continue;

                Finding item = _items[i];
                bool selected = _selected.Contains(item.Id);
                bool hover = i == _hover;

                if (selected) Ink.FillRounded(g, new Rectangle(Px(6), row.Top + 1, row.Width - Px(12), row.Height - 2), Px(6), Theme.Selection);
                else if (hover) Ink.FillRounded(g, new Rectangle(Px(6), row.Top + 1, row.Width - Px(12), row.Height - 2), Px(6), Theme.Hover);

                PaintCheck(g, i, CheckBounds(i));

                int cellX = Px(40);
                for (int c = 0; c < widths.Length; c++)
                {
                    Rectangle cell = new Rectangle(cellX, row.Top, widths[c], row.Height);

                    if (c == 0)
                    {
                        Rectangle glyph = new Rectangle(cell.Left, row.Top + (row.Height - Px(18)) / 2, Px(18), Px(18));
                        Glyphs.Draw(g, item.Shape, glyph, Glyphs.Colour(item.Shape));

                        Rectangle name = new Rectangle(glyph.Right + Px(9), row.Top, cell.Width - Px(30), row.Height);
                        Ink.Text(g, item.Title, Theme.Small, Theme.Text, name,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    }
                    else if (c == 1)
                    {
                        Ink.Text(g, item.CategoryLabel, Theme.Tiny, Theme.TextMuted, cell,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    else if (c == 2)
                    {
                        Rectangle size = new Rectangle(cell.Left, cell.Top, cell.Width - Px(12), cell.Height);
                        Ink.Text(g, Format.Bytes(item.Bytes), Theme.Small, Theme.TextMuted, size,
                            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                    }
                    else if (c == 3)
                    {
                        Rectangle changed = new Rectangle(cell.Left + Px(10), cell.Top, cell.Width - Px(10), cell.Height);
                        Ink.Text(g, Format.Date(item.Modified), Theme.Tiny, Theme.TextDim, changed,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    }
                    else
                    {
                        Ink.Text(g, item.SafetyLabel, Theme.Tiny, Theme.ForSafety(item.Level), cell,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    }

                    cellX += widths[c];
                }
            }
        }

        private int[] DetailColumns()
        {
            int width = ClientSize.Width;
            int name = Math.Max(Px(140), width - Px(40) - Px(120) - Px(82) - Px(96) - Px(70) - Px(16));
            return new[] { name, Px(120), Px(82), Px(96), Px(70) };
        }

        private static SortField DetailField(int column)
        {
            switch (column)
            {
                case 0: return SortField.Name;
                case 1: return SortField.Category;
                case 2: return SortField.Size;
                case 3: return SortField.Changed;
                default: return SortField.Safety;
            }
        }

        private static Rectangle CoverRect(Image image, Rectangle bounds)
        {
            float scale = Math.Max((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
            int width = Math.Max(1, (int)Math.Ceiling(image.Width * scale));
            int height = Math.Max(1, (int)Math.Ceiling(image.Height * scale));

            return new Rectangle(bounds.Left + (bounds.Width - width) / 2,
                bounds.Top + (bounds.Height - height) / 2, width, height);
        }

        private Image LoadPreview(Finding item, int size)
        {
            switch (item.Category)
            {
                case Category.Image:
                case Category.Video:
                case Category.Installer:
                case Category.AppPackage:
                    return Thumbnails.Get(item.Path, Math.Min(256, Math.Max(64, size)), OnThumbReady);
                default:
                    return null;
            }
        }

        private void OnThumbReady()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(_invalidate); }
            catch (Exception) { }
        }

        private static string ShortLocation(Finding item)
        {
            string path = item.IsDuplicateGroup ? item.KeeperPath : item.Path;
            if (string.IsNullOrEmpty(path)) return string.Empty;

            try
            {
                string directory = System.IO.Directory.Exists(path)
                    ? path
                    : System.IO.Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(directory)) return path;

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home) && directory.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                {
                    directory = "~" + directory.Substring(home.Length);
                }

                return directory;
            }
            catch (Exception) { return path; }
        }

        // --------------------------------------------------------------- input

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_view == GridView.Details && e.Y < HeaderHeight)
            {
                HeaderClick(e.Location);
                return;
            }

            int index = IndexAt(e.Location);
            if (index < 0)
            {
                if (!ModifierKeys.HasFlag(Keys.Control))
                {
                    ClearSelection();
                }
                return;
            }

            int check = HitCheck(e.Location);
            if (check >= 0)
            {
                ToggleChecked(_items[check]);
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                if (!_selected.Contains(_items[index].Id)) SelectIndex(index, false, false);
                return;
            }

            bool control = (ModifierKeys & Keys.Control) == Keys.Control;
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (shift && _anchor >= 0)
            {
                SelectRange(_anchor, index, control);
            }
            else
            {
                SelectIndex(index, control, shift);
                _anchor = index;
                _dragging = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int index = IndexAt(e.Location);
            if (index != _hover)
            {
                _hover = index;
                Invalidate();
            }

            int check = HitCheck(e.Location);
            Cursor = check >= 0 ? Cursors.Hand : Cursors.Default;

            if (_dragging && index >= 0)
            {
                SelectIndex(index, true, false);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
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

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int index = IndexAt(e.Location);
            if (index < 0 || HitCheck(e.Location) >= 0) return;

            SelectIndex(index, false, false);
            EventHandler handler = Activated;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void SelectIndex(int index, bool add, bool range)
        {
            Finding item = _items[index];

            if (!add) _selected.Clear();
            _selected.Add(item.Id);
            _focus = index;

            Invalidate();
            RaiseSelection();
        }

        private void SelectRange(int from, int to, bool add)
        {
            if (!add) _selected.Clear();

            int start = Math.Min(from, to);
            int end = Math.Max(from, to);

            for (int i = start; i <= end && i < _items.Count; i++) _selected.Add(_items[i].Id);

            _focus = to;
            Invalidate();
            RaiseSelection();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Space:
                case Keys.Enter:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_items.Count == 0) return;

            if (e.KeyCode == Keys.A && e.Control)
            {
                _selected.Clear();
                for (int i = 0; i < _items.Count; i++) _selected.Add(_items[i].Id);
                Invalidate();
                RaiseSelection();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Space && _focus >= 0)
            {
                ToggleChecked(_items[_focus]);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter && _focus >= 0)
            {
                EventHandler handler = Activated;
                if (handler != null) handler(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            int move = 0;
            if (e.KeyCode == Keys.Left) move = -1;
            else if (e.KeyCode == Keys.Right) move = 1;
            else if (e.KeyCode == Keys.Up) move = _view == GridView.Large || _view == GridView.Medium ? -ColumnCount : -1;
            else if (e.KeyCode == Keys.Down) move = _view == GridView.Large || _view == GridView.Medium ? ColumnCount : 1;
            else if (e.KeyCode == Keys.Home) move = -_items.Count;
            else if (e.KeyCode == Keys.End) move = _items.Count;
            else if (e.KeyCode == Keys.PageDown) move = 10;
            else if (e.KeyCode == Keys.PageUp) move = -10;

            if (move != 0)
            {
                int target = Math.Max(0, Math.Min(_items.Count - 1, (_focus < 0 ? 0 : _focus) + move));
                SelectIndex(target, e.Control, false);
                _anchor = target;
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ScrollMax <= 0) return;

            int step = _view == GridView.Details || _view == GridView.List ? RowHeight : CardHeight + CardGap;
            int next = _scroll - (e.Delta / 120) * step;
            _scroll = Math.Max(0, Math.Min(ScrollMax, next));
            Invalidate();
        }

        private void RaiseSelection()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseCheck()
        {
            EventHandler handler = CheckChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>Header click in the details view.</summary>
        public void HeaderClick(Point point)
        {
            if (_view != GridView.Details || point.Y >= HeaderHeight) return;

            int[] widths = DetailColumns();
            int x = Px(40);

            for (int i = 0; i < widths.Length; i++)
            {
                if (point.X >= x && point.X < x + widths[i])
                {
                    EventHandler<SortField> handler = SortRequested;
                    if (handler != null) handler(this, DetailField(i));
                    return;
                }
                x += widths[i];
            }
        }
    }
}
