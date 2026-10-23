using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace BinDay.App
{
    public enum GridView
    {
        Large,
        Medium,
        List,
        Details
    }

    public class FindingGrid : UserControl
    {
        private List<Finding> _dataSource = new List<Finding>();
        private GridView _view = GridView.Large;
        private SortField _sort = SortField.Size;
        private bool _descending = true;

        private Point _scrollOffset = new Point(0, 0);
        private const int CardPadding = 12;
        private const int CardSpacing = 16;
        private const int CardHeight = 120;
        private const int CardWidthLarge = 240;
        private const int CardWidthMedium = 160;
        private const int CardWidthList = 100;
        private const int CardWidthDetails = 400;

        public void SetDataSource(List<Finding> data)
        {
            _dataSource = data;
            Refresh();
        }

        public void Refresh()
        {
            _scrollOffset = new Point(0, 0);
            Invalidate();
        }

        public void SetView(GridView view)
        {
            _view = view;
            Invalidate();
        }

        public void SetSort(SortField sort, bool descending)
        {
            _sort = sort;
            _descending = descending;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_dataSource.Count == 0)
            {
                if (_empty != null)
                {
                    _empty.Location = new Point((Width - _empty.Width) / 2, (Height - _empty.Height) / 2);
                    _empty.Visible = true;
                }
                return;
            }

            _empty.Visible = false;

            int cardWidth = GetCardWidth();
            int cardHeight = CardHeight;

            int startX = CardPadding;
            int startY = CardPadding;

            for (int i = 0; i < _dataSource.Count; i++)
            {
                Finding finding = _dataSource[i];
                Rectangle cardRect = CalculateCardRect(startX, startY, cardWidth, cardHeight, i);

                DrawCardBackground(e, cardRect, finding);
                DrawCardContent(e, cardRect, finding);
            }

            // Handle vertical scrolling
            if (_scrollOffset.Y > 0)
            {
                e.Graphics.TranslateTransform(_scrollOffset.X, _scrollOffset.Y);
            }
        }

        private Rectangle CalculateCardRect(int startX, int startY, int cardWidth, int cardHeight, int index)
        {
            int row = index / GetCardsPerRow(cardWidth);
            int column = index % GetCardsPerRow(cardWidth);

            int x = startX + column * (cardWidth + CardSpacing);
            int y = startY + row * (cardHeight + CardSpacing);

            return new Rectangle(x, y, cardWidth, cardHeight);
        }

        private int GetCardsPerRow(int cardWidth)
        {
            switch (_view)
            {
                case GridView.Large:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                case GridView.Medium:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                case GridView.List:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                case GridView.Details:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                default:
                    return 1;
            }
        }

        private int GetCardWidth()
        {
            switch (_view)
            {
                case GridView.Large:
                    return CardWidthLarge;
                case GridView.Medium:
                    return CardWidthMedium;
                case GridView.List:
                    return CardWidthList;
                case GridView.Details:
                    return CardWidthDetails;
                default:
                    return CardWidthLarge;
            }
        }

        private void DrawCardBackground(Graphics g, Rectangle rect, Finding finding)
        {
            // Draw card background
            using (SolidBrush brush = new SolidBrush(Theme.Card))
            {
                g.FillRectangle(brush, rect);
            }

            // Draw hover effect
            if (IsMouseOverCard(finding))
            {
                using (SolidBrush hoverBrush = new SolidBrush(Theme.CardHover))
                {
                    g.FillRectangle(hoverBrush, rect);
                }
            }

            // Draw border
            using (Pen pen = new Pen(Theme.Line, 1f))
            {
                Rectangle borderRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                g.DrawRectangle(pen, borderRect);
            }

            // Draw selection highlight
            if (finding.IsSelected)
            {
                using (SolidBrush selectionBrush = new SolidBrush(Theme.Selection))
                {
                    g.FillRectangle(selectionBrush, rect);
                }
            }
        }

        private void DrawCardContent(Graphics g, Rectangle rect, Finding finding)
        {
            // Draw title
            Rectangle titleRect = new Rectangle(rect.X + CardPadding, rect.Y + CardPadding, rect.Width - (CardPadding * 2), 24);
            using (Font font = Theme.Large)
            {
                TextRenderer.DrawText(g, finding.Title, font, titleRect, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }

            // Draw subtitle/description
            Rectangle descRect = new Rectangle(rect.X + CardPadding, rect.Bottom - 36, rect.Width - (CardPadding * 2), 36);
            using (Font font = Theme.Small)
            {
                TextRenderer.DrawText(g, finding.Description, font, descRect, Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }

            // Draw size info
            Rectangle sizeRect = new Rectangle(rect.X + CardPadding, rect.Bottom - 20, rect.Width - (CardPadding * 2), 16);
            using (Font font = Theme.Tiny)
            {
                TextRenderer.DrawText(g, finding.SizeFormatted, font, sizeRect, Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }

            // Draw category icon
            if (finding.IconPath != null)
            {
                try
                {
                    Image icon = Image.FromFile(finding.IconPath);
                    int iconSize = 24;
                    Rectangle iconRect = new Rectangle(rect.X + CardPadding, rect.Y + CardPadding + 4, iconSize, iconSize);
                    g.DrawImage(icon, iconRect);
                }
                catch
                {
                    // Fallback to default icon
                }
            }

            // Draw category badge
            if (finding.CategoryBadge != null)
            {
                Rectangle badgeRect = new Rectangle(rect.X + CardPadding, rect.Y + CardPadding + 4, 40, 24);
                using (SolidBrush brush = new SolidBrush(Glyphs.Colour(finding.CategoryBadgeColor)))
                {
                    g.FillRectangle(brush, badgeRect);
                }
                using (Pen pen = new Pen(Theme.Line, 1f))
                {
                    g.DrawRectangle(pen, badgeRect);
                }
                using (Font font = Theme.Tiny)
                {
                    g.DrawString(finding.CategoryBadgeLabel, font, new SolidBrush(Theme.TextDim), badgeRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private bool IsMouseOverCard(Finding finding)
        {
            // Implementation depends on how selection is tracked
            return finding.IsSelected;
        }

        private int GetCardsPerRow(int cardWidth)
        {
            switch (_view)
            {
                case GridView.Large:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                case GridView.Medium:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                case GridView.List:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                case GridView.Details:
                    return (Width - (CardPadding * 2)) / (cardWidth + CardSpacing);
                default:
                    return 1;
            }
        }

        public GridView View
        {
            get { return _view; }
            set
            {
                _view = value;
                Invalidate();
            }
        }

        public SortField Sort
        {
            get { return _sort; }
            set
            {
                _sort = value;
                Invalidate();
            }
        }

        public bool Descending
        {
            get { return _descending; }
            set
            {
                _descending = value;
                Invalidate();
            }
        }

        public void ScrollTo(int index)
        {
            if (index < 0 || index >= _dataSource.Count) return;

            int cardWidth = GetCardWidth();
            int cardHeight = CardHeight;
            int cardsPerRow = GetCardsPerRow(cardWidth);
            int row = index / cardsPerRow;
            int cardHeightWithSpacing = cardHeight + CardSpacing;

            _scrollOffset = new Point(0, row * cardHeightWithSpacing);
            Invalidate();
        }
    }