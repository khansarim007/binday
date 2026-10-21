using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>
    /// Hand-drawn vector glyphs for every kind of item. Deliberately not shell icons:
    /// they stay crisp at any size and never depend on file associations.
    /// </summary>
    internal static class Glyphs
    {
        public static Color Colour(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Build: return ColorTranslator.FromHtml("#34D399");
                case ItemKind.Cache: return ColorTranslator.FromHtml("#60A5FA");
                case ItemKind.Temp: return ColorTranslator.FromHtml("#FBBF24");
                case ItemKind.Installer: return ColorTranslator.FromHtml("#C084FC");
                case ItemKind.Duplicate: return ColorTranslator.FromHtml("#22D3EE");
                case ItemKind.Video: return ColorTranslator.FromHtml("#FB7185");
                case ItemKind.Photo: return ColorTranslator.FromHtml("#FDBA74");
                case ItemKind.Audio: return ColorTranslator.FromHtml("#F472B6");
                case ItemKind.Package: return ColorTranslator.FromHtml("#4ADE80");
                case ItemKind.Redundant: return ColorTranslator.FromHtml("#F59E0B");
                case ItemKind.Folder: return ColorTranslator.FromHtml("#34D399");
                default: return ColorTranslator.FromHtml("#94A3B8");
            }
        }

        public static Color Colour(Category category)
        {
            return Colour(CategoryInfo.Shape(category));
        }

        /// <summary>Rounded tile with the glyph centred inside.</summary>
        public static void Tile(Graphics g, ItemKind kind, Rectangle bounds, bool filled)
        {
            Color colour = Colour(kind);
            int radius = Math.Max(6, bounds.Width / 5);

            if (filled)
            {
                Ink.FillRounded(g, bounds, radius, Theme.Mix(colour, Theme.Card, 0.13));
            }

            int pad = Math.Max(4, bounds.Width / 4);
            Rectangle inner = new Rectangle(bounds.X + pad, bounds.Y + pad,
                Math.Max(4, bounds.Width - pad * 2), Math.Max(4, bounds.Height - pad * 2));

            Draw(g, kind, inner, colour);
        }

        public static void Draw(Graphics g, ItemKind kind, Rectangle r, Color colour)
        {
            if (r.Width < 4 || r.Height < 4) return;

            SmoothingMode previousSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float w = r.Width;
            float h = r.Height;
            float x = r.X;
            float y = r.Y;
            float pen = Math.Max(1.2f, w / 11f);
            Color soft = Color.FromArgb(110, colour);

            using (Pen p = new Pen(colour, pen))
            using (Pen faint = new Pen(soft, pen))
            using (SolidBrush brush = new SolidBrush(colour))
            using (SolidBrush softBrush = new SolidBrush(soft))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                faint.StartCap = LineCap.Round;
                faint.EndCap = LineCap.Round;
                faint.LineJoin = LineJoin.Round;

                switch (kind)
                {
                    case ItemKind.Folder:
                        DrawFolder(g, p, brush, x, y, w, h);
                        break;

                    case ItemKind.Build:
                        DrawCube(g, p, brush, x, y, w, h);
                        break;

                    case ItemKind.Cache:
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                float pad = i * h * 0.22f;
                                RectangleF bar = new RectangleF(x + w * 0.1f + pad * 0.4f, y + h * 0.16f + pad,
                                    w * 0.8f - pad * 0.8f, h * 0.18f);
                                using (GraphicsPath path = Rounded(Rectangle.Round(bar), (int)(bar.Height / 2)))
                                using (SolidBrush b = new SolidBrush(i == 0 ? colour : soft))
                                {
                                    g.FillPath(b, path);
                                }
                            }
                        }
                        break;

                    case ItemKind.Temp:
                        {
                            RectangleF circle = new RectangleF(x + w * 0.12f, y + h * 0.12f, w * 0.76f, h * 0.76f);
                            g.DrawEllipse(p, circle);
                            g.DrawLine(p, x + w * 0.5f, y + h * 0.3f, x + w * 0.5f, y + h * 0.52f);
                            g.DrawLine(p, x + w * 0.5f, y + h * 0.52f, x + w * 0.68f, y + h * 0.62f);
                        }
                        break;

                    case ItemKind.Installer:
                        {
                            RectangleF box = new RectangleF(x + w * 0.12f, y + h * 0.1f, w * 0.76f, h * 0.8f);
                            using (GraphicsPath path = Rounded(Rectangle.Round(box), (int)(w * 0.12f)))
                            {
                                g.FillPath(softBrush, path);
                                g.DrawPath(p, path);
                            }
                            g.DrawLine(p, x + w * 0.5f, y + h * 0.3f, x + w * 0.5f, y + h * 0.58f);
                            g.DrawLine(p, x + w * 0.38f, y + h * 0.48f, x + w * 0.5f, y + h * 0.6f);
                            g.DrawLine(p, x + w * 0.62f, y + h * 0.48f, x + w * 0.5f, y + h * 0.6f);
                        }
                        break;

                    case ItemKind.Duplicate:
                        {
                            RectangleF back = new RectangleF(x + w * 0.24f, y + h * 0.08f, w * 0.62f, h * 0.62f);
                            RectangleF front = new RectangleF(x + w * 0.14f, y + h * 0.26f, w * 0.62f, h * 0.62f);
                            using (GraphicsPath b = Rounded(Rectangle.Round(back), (int)(w * 0.1f)))
                            using (GraphicsPath f = Rounded(Rectangle.Round(front), (int)(w * 0.1f)))
                            {
                                g.FillPath(softBrush, b);
                                g.DrawPath(faint, b);
                                g.FillPath(softBrush, f);
                                g.DrawPath(p, f);
                            }
                        }
                        break;

                    case ItemKind.Video:
                        {
                            RectangleF frame = new RectangleF(x + w * 0.06f, y + h * 0.14f, w * 0.88f, h * 0.72f);
                            using (GraphicsPath path = Rounded(Rectangle.Round(frame), (int)(w * 0.1f)))
                            {
                                g.FillPath(softBrush, path);
                                g.DrawPath(p, path);
                            }

                            PointF[] play =
                            {
                                new PointF(x + w * 0.42f, y + h * 0.35f),
                                new PointF(x + w * 0.42f, y + h * 0.65f),
                                new PointF(x + w * 0.64f, y + h * 0.5f)
                            };
                            g.FillPolygon(brush, play);
                        }
                        break;

                    case ItemKind.Photo:
                        {
                            RectangleF frame = new RectangleF(x + w * 0.08f, y + h * 0.14f, w * 0.84f, h * 0.72f);
                            using (GraphicsPath path = Rounded(Rectangle.Round(frame), (int)(w * 0.1f)))
                            {
                                g.FillPath(softBrush, path);
                                g.DrawPath(p, path);
                            }

                            g.FillEllipse(brush, x + w * 0.22f, y + h * 0.28f, w * 0.14f, h * 0.14f);

                            PointF[] mountain =
                            {
                                new PointF(x + w * 0.16f, y + h * 0.76f),
                                new PointF(x + w * 0.38f, y + h * 0.48f),
                                new PointF(x + w * 0.54f, y + h * 0.66f),
                                new PointF(x + w * 0.68f, y + h * 0.52f),
                                new PointF(x + w * 0.86f, y + h * 0.76f)
                            };
                            g.FillPolygon(brush, mountain);
                        }
                        break;

                    case ItemKind.Audio:
                        {
                            float[] heights = { 0.34f, 0.62f, 0.9f, 0.52f };
                            float barWidth = w * 0.13f;
                            float gap = (w * 0.84f - barWidth * heights.Length) / (heights.Length - 1);

                            for (int i = 0; i < heights.Length; i++)
                            {
                                float bh = h * heights[i];
                                RectangleF bar = new RectangleF(x + w * 0.08f + i * (barWidth + gap), y + (h - bh) / 2f, barWidth, bh);
                                using (GraphicsPath path = Rounded(Rectangle.Round(bar), (int)(barWidth / 2)))
                                using (SolidBrush b = new SolidBrush(i == 2 ? colour : soft))
                                {
                                    g.FillPath(b, path);
                                }
                            }
                        }
                        break;

                    case ItemKind.Package:
                        {
                            RectangleF box = new RectangleF(x + w * 0.1f, y + h * 0.1f, w * 0.8f, h * 0.8f);
                            using (GraphicsPath path = Rounded(Rectangle.Round(box), (int)(w * 0.16f)))
                            {
                                g.FillPath(softBrush, path);
                                g.DrawPath(p, path);
                            }
                            g.DrawLine(p, x + w * 0.5f, y + h * 0.28f, x + w * 0.5f, y + h * 0.58f);
                            g.DrawLine(p, x + w * 0.38f, y + h * 0.47f, x + w * 0.5f, y + h * 0.59f);
                            g.DrawLine(p, x + w * 0.62f, y + h * 0.47f, x + w * 0.5f, y + h * 0.59f);
                            g.DrawLine(p, x + w * 0.34f, y + h * 0.72f, x + w * 0.66f, y + h * 0.72f);
                        }
                        break;

                    case ItemKind.Redundant:
                        {
                            RectangleF back = new RectangleF(x + w * 0.1f, y + h * 0.1f, w * 0.55f, h * 0.7f);
                            RectangleF front = new RectangleF(x + w * 0.35f, y + h * 0.24f, w * 0.55f, h * 0.7f);
                            using (GraphicsPath b = Rounded(Rectangle.Round(back), (int)(w * 0.1f)))
                            using (GraphicsPath f = Rounded(Rectangle.Round(front), (int)(w * 0.1f)))
                            {
                                g.DrawPath(faint, b);
                                g.FillPath(softBrush, f);
                                g.DrawPath(p, f);
                            }
                        }
                        break;

                    default:
                        DrawPage(g, p, softBrush, x, y, w, h);
                        break;
                }
            }

            g.SmoothingMode = previousSmoothing;
        }

        private static void DrawFolder(Graphics g, Pen p, SolidBrush brush, float x, float y, float w, float h)
        {
            GraphicsPath path = new GraphicsPath();
            float left = x + w * 0.08f;
            float right = x + w * 0.92f;
            float top = y + h * 0.2f;
            float bottom = y + h * 0.84f;
            int radius = (int)(w * 0.1f);

            path.AddLine(left + radius, top, x + w * 0.4f, top);
            path.AddLine(x + w * 0.44f, top, x + w * 0.5f, y + h * 0.3f);
            path.AddLine(x + w * 0.5f, y + h * 0.3f, right - radius, y + h * 0.3f);
            path.AddArc(right - radius * 2, y + h * 0.3f, radius * 2, radius * 2, -90, 90);
            path.AddLine(right, y + h * 0.3f + radius, right, bottom - radius);
            path.AddArc(right - radius * 2, bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddLine(right - radius, bottom, left + radius, bottom);
            path.AddArc(left, bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.AddLine(left, bottom - radius, left, top + radius);
            path.AddArc(left, top, radius * 2, radius * 2, 180, 90);
            path.CloseFigure();

            using (path)
            {
                g.FillPath(brush, path);
            }
        }

        private static void DrawCube(Graphics g, Pen p, SolidBrush brush, float x, float y, float w, float h)
        {
            PointF top = new PointF(x + w * 0.5f, y + h * 0.12f);
            PointF left = new PointF(x + w * 0.14f, y + h * 0.33f);
            PointF right = new PointF(x + w * 0.86f, y + h * 0.33f);
            PointF bottom = new PointF(x + w * 0.5f, y + h * 0.54f);

            g.DrawPolygon(p, new[] { top, right, bottom, left });

            g.DrawLine(p, left, new PointF(x + w * 0.14f, y + h * 0.74f));
            g.DrawLine(p, right, new PointF(x + w * 0.86f, y + h * 0.74f));
            g.DrawLine(p, bottom, new PointF(x + w * 0.5f, y + h * 0.92f));
            g.DrawLine(p, new PointF(x + w * 0.14f, y + h * 0.74f), new PointF(x + w * 0.5f, y + h * 0.92f));
            g.DrawLine(p, new PointF(x + w * 0.86f, y + h * 0.74f), new PointF(x + w * 0.5f, y + h * 0.92f));

            using (SolidBrush soft = new SolidBrush(Color.FromArgb(70, brush.Color)))
            {
                g.FillPolygon(soft, new[] { top, right, bottom, left });
            }
        }

        private static void DrawPage(Graphics g, Pen p, SolidBrush brush, float x, float y, float w, float h)
        {
            float left = x + w * 0.22f;
            float right = x + w * 0.78f;
            float top = y + h * 0.1f;
            float bottom = y + h * 0.9f;
            float fold = w * 0.2f;

            PointF[] outline =
            {
                new PointF(left, top),
                new PointF(right - fold, top),
                new PointF(right, top + fold),
                new PointF(right, bottom),
                new PointF(left, bottom)
            };

            g.FillPolygon(brush, outline);
            g.DrawPolygon(p, outline);

            using (SolidBrush ink = new SolidBrush(Theme.Card))
            {
                g.FillPolygon(ink, new[]
                {
                    new PointF(right - fold, top),
                    new PointF(right - fold, top + fold),
                    new PointF(right, top + fold)
                });
            }
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            return Ink.Rounded(bounds, radius);
        }

        /// <summary>Small tick used by checkboxes and selected rows.</summary>
        public static void Tick(Graphics g, Rectangle bounds, Color colour, float width)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(colour, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                float x = bounds.X;
                float y = bounds.Y;
                float w = bounds.Width;
                float h = bounds.Height;

                g.DrawLines(pen, new[]
                {
                    new PointF(x + w * 0.06f, y + h * 0.55f),
                    new PointF(x + w * 0.38f, y + h * 0.82f),
                    new PointF(x + w * 0.95f, y + h * 0.16f)
                });
            }
            g.SmoothingMode = previous;
        }

        public static void Chevron(Graphics g, Rectangle bounds, Color colour, float width, bool down)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(colour, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                float x = bounds.X;
                float y = bounds.Y;
                float w = bounds.Width;
                float h = bounds.Height;

                if (down)
                {
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + w * 0.2f, y + h * 0.38f),
                        new PointF(x + w * 0.5f, y + h * 0.65f),
                        new PointF(x + w * 0.8f, y + h * 0.38f)
                    });
                }
                else
                {
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + w * 0.2f, y + h * 0.62f),
                        new PointF(x + w * 0.5f, y + h * 0.35f),
                        new PointF(x + w * 0.8f, y + h * 0.62f)
                    });
                }
            }
            g.SmoothingMode = previous;
        }
    }
}
