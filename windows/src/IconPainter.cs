using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LinkDrop.UI
{
    internal enum IconKind
    {
        None,
        Send,
        Folder,
        Settings,
        Shield,
        Wifi,
        Laptop,
        Phone,
        Check,
        Close,
        Minimize,
        Maximize,
        Restore,
        Download,
        Upload,
        File,
        Link,
        Refresh,
        Copy,
        Trash,
        Plus,
        External,
        Clock,
        Firewall,
        Alert,
        Info,
        ArrowRight,
        Sparkle,
        Lock,
        Bolt
    }

    internal static class IconPainter
    {
        public static void Draw(Graphics g, IconKind kind, RectangleF box, Color color, float thickness)
        {
            if (kind == IconKind.None || box.Width <= 0 || box.Height <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Min(box.Width, box.Height) / 24f;
                g.TranslateTransform(box.X + (box.Width - 24f * scale) / 2f, box.Y + (box.Height - 24f * scale) / 2f);
                g.ScaleTransform(scale, scale);
                using (Pen pen = new Pen(color, 1.75f * thickness))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        DrawCore(g, pen, brush, kind);
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        public static void DrawLogo(Graphics g, RectangleF box)
        {
            GraphicsState state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = Math.Min(box.Width, box.Height) / 24f;
                g.TranslateTransform(box.X + (box.Width - 24f * scale) / 2f, box.Y + (box.Height - 24f * scale) / 2f);
                g.ScaleTransform(scale, scale);
                using (GraphicsPath bg = Gfx.Rounded(new RectangleF(0, 0, 24, 24), 6.2f))
                using (LinearGradientBrush gradient = new LinearGradientBrush(new RectangleF(0, 0, 24, 24), Color.FromArgb(27, 107, 98), Color.FromArgb(8, 46, 43), 42f))
                {
                    g.FillPath(gradient, bg);
                }
                using (Pen pen = new Pen(Color.FromArgb(102, 240, 211), 2.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLine(pen, 5.3f, 9.4f, 14.4f, 9.4f);
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(102, 240, 211)))
                    {
                        g.FillPolygon(brush, new PointF[] { new PointF(13.2f, 6.2f), new PointF(18.5f, 9.4f), new PointF(13.2f, 12.6f) });
                    }
                }
                using (Pen pen = new Pen(Color.FromArgb(247, 255, 252), 2.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLine(pen, 18.7f, 14.6f, 9.6f, 14.6f);
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(247, 255, 252)))
                    {
                        g.FillPolygon(brush, new PointF[] { new PointF(10.8f, 11.4f), new PointF(5.5f, 14.6f), new PointF(10.8f, 17.8f) });
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private static void DrawCore(Graphics g, Pen pen, SolidBrush brush, IconKind kind)
        {
            switch (kind)
            {
                case IconKind.Send:
                    {
                        PointF[] plane = new PointF[] {
                            new PointF(21f, 3f), new PointF(14.2f, 21f), new PointF(10.8f, 13.2f),
                            new PointF(3f, 9.7f), new PointF(21f, 3f)
                        };
                        g.DrawPolygon(pen, plane);
                        g.DrawLine(pen, 10.8f, 13.2f, 14.5f, 9.6f);
                        break;
                    }
                case IconKind.Folder:
                    {
                        GraphicsPath path = new GraphicsPath();
                        path.AddLine(3.2f, 7.2f, 3.2f, 18.2f);
                        path.AddArc(3.2f, 16.2f, 3.6f, 3.6f, 90, 90);
                        path.AddLine(5f, 19.8f, 19f, 19.8f);
                        path.AddArc(17.2f, 16.2f, 3.6f, 3.6f, 0, 90);
                        path.AddLine(20.8f, 18.2f, 20.8f, 8.8f);
                        path.AddArc(17.2f, 5.2f, 3.6f, 3.6f, 0, 90);
                        path.AddLine(19f, 5.2f, 12.3f, 5.2f);
                        path.AddLine(10.7f, 7.2f, 5f, 7.2f);
                        path.AddArc(3.2f, 5.2f, 3.6f, 3.6f, 90, 90);
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                        path.Dispose();
                        break;
                    }
                case IconKind.Settings:
                    {
                        g.DrawEllipse(pen, 8.3f, 8.3f, 7.4f, 7.4f);
                        g.DrawEllipse(pen, 10.6f, 10.6f, 2.8f, 2.8f);
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = Math.PI * i / 4.0;
                            float x1 = 12f + (float)Math.Cos(angle) * 6.1f;
                            float y1 = 12f + (float)Math.Sin(angle) * 6.1f;
                            float x2 = 12f + (float)Math.Cos(angle) * 8.2f;
                            float y2 = 12f + (float)Math.Sin(angle) * 8.2f;
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;
                    }
                case IconKind.Shield:
                    {
                        GraphicsPath path = new GraphicsPath();
                        path.AddLine(12f, 2.7f, 20f, 5.7f);
                        path.AddLine(20f, 5.7f, 20f, 11.2f);
                        path.AddBezier(20f, 11.2f, 20f, 16.3f, 16.6f, 19.8f, 12f, 21.5f);
                        path.AddBezier(12f, 21.5f, 7.4f, 19.8f, 4f, 16.3f, 4f, 11.2f);
                        path.AddLine(4f, 11.2f, 4f, 5.7f);
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                        path.Dispose();
                        g.DrawLines(pen, new PointF[] { new PointF(8.3f, 11.8f), new PointF(10.9f, 14.4f), new PointF(15.8f, 9.2f) });
                        break;
                    }
                case IconKind.Wifi:
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float size = 4.2f + i * 4.8f;
                            g.DrawArc(pen, 12f - size / 2f, 17.7f - size / 2f, size, size, 205, 130);
                        }
                        g.FillEllipse(brush, 10.6f, 16.6f, 2.8f, 2.8f);
                        break;
                    }
                case IconKind.Laptop:
                    {
                        GraphicsPath path = Gfx.Rounded(new RectangleF(4.2f, 4f, 15.6f, 10.5f), 1.7f);
                        g.DrawPath(pen, path); path.Dispose();
                        g.DrawLine(pen, 2.2f, 18.2f, 21.8f, 18.2f);
                        g.DrawLine(pen, 9.7f, 18.2f, 10.3f, 20.2f);
                        g.DrawLine(pen, 14.3f, 18.2f, 13.7f, 20.2f);
                        break;
                    }
                case IconKind.Phone:
                    {
                        GraphicsPath path = Gfx.Rounded(new RectangleF(6.6f, 2.2f, 10.8f, 19.6f), 2.6f);
                        g.DrawPath(pen, path); path.Dispose();
                        g.DrawLine(pen, 10.2f, 18.2f, 13.8f, 18.2f);
                        g.DrawLine(pen, 10.6f, 5.1f, 13.4f, 5.1f);
                        break;
                    }
                case IconKind.Check:
                    g.DrawLines(pen, new PointF[] { new PointF(4.2f, 12.4f), new PointF(9.4f, 17.4f), new PointF(19.8f, 6.4f) });
                    break;
                case IconKind.Close:
                    g.DrawLine(pen, 6f, 6f, 18f, 18f);
                    g.DrawLine(pen, 18f, 6f, 6f, 18f);
                    break;
                case IconKind.Minimize:
                    g.DrawLine(pen, 5.5f, 12f, 18.5f, 12f);
                    break;
                case IconKind.Maximize:
                    g.DrawRectangle(pen, 5.5f, 5.5f, 13f, 13f);
                    break;
                case IconKind.Restore:
                    g.DrawRectangle(pen, 5f, 8.5f, 10.5f, 10.5f);
                    g.DrawLines(pen, new PointF[] { new PointF(8.5f, 8.5f), new PointF(8.5f, 5f), new PointF(19f, 5f), new PointF(19f, 15.5f), new PointF(15.5f, 15.5f) });
                    break;
                case IconKind.Download:
                    g.DrawLine(pen, 12f, 3.5f, 12f, 15f);
                    g.DrawLines(pen, new PointF[] { new PointF(7.3f, 10.5f), new PointF(12f, 15.2f), new PointF(16.7f, 10.5f) });
                    g.DrawLines(pen, new PointF[] { new PointF(4f, 17f), new PointF(4f, 20.2f), new PointF(20f, 20.2f), new PointF(20f, 17f) });
                    break;
                case IconKind.Upload:
                    g.DrawLine(pen, 12f, 15.5f, 12f, 4f);
                    g.DrawLines(pen, new PointF[] { new PointF(7.3f, 8.7f), new PointF(12f, 4f), new PointF(16.7f, 8.7f) });
                    g.DrawLines(pen, new PointF[] { new PointF(4f, 17f), new PointF(4f, 20.2f), new PointF(20f, 20.2f), new PointF(20f, 17f) });
                    break;
                case IconKind.File:
                    {
                        GraphicsPath path = new GraphicsPath();
                        path.AddLine(6f, 2.8f, 13.3f, 2.8f);
                        path.AddLine(13.3f, 2.8f, 18.5f, 8f);
                        path.AddLine(18.5f, 8f, 18.5f, 21.2f);
                        path.AddLine(18.5f, 21.2f, 6f, 21.2f);
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                        path.Dispose();
                        g.DrawLines(pen, new PointF[] { new PointF(13.2f, 2.9f), new PointF(13.2f, 8.1f), new PointF(18.4f, 8.1f) });
                        g.DrawLine(pen, 8.5f, 13f, 15.8f, 13f);
                        g.DrawLine(pen, 8.5f, 16.3f, 14.3f, 16.3f);
                        break;
                    }
                case IconKind.Link:
                    {
                        GraphicsPath path1 = new GraphicsPath();
                        path1.AddBezier(10.2f, 13.4f, 8.4f, 15.1f, 7.1f, 16.3f, 5.6f, 16.3f);
                        path1.AddBezier(5.6f, 16.3f, 2.8f, 16.3f, 1.6f, 12.4f, 3.5f, 10.5f);
                        path1.AddLine(3.5f, 10.5f, 6.9f, 7.2f);
                        path1.AddBezier(6.9f, 7.2f, 8.7f, 5.4f, 11.2f, 6.4f, 12.2f, 8.1f);
                        g.DrawPath(pen, path1); path1.Dispose();
                        GraphicsPath path2 = new GraphicsPath();
                        path2.AddBezier(13.8f, 10.6f, 15.6f, 8.9f, 16.9f, 7.7f, 18.4f, 7.7f);
                        path2.AddBezier(18.4f, 7.7f, 21.2f, 7.7f, 22.4f, 11.6f, 20.5f, 13.5f);
                        path2.AddLine(20.5f, 13.5f, 17.1f, 16.8f);
                        path2.AddBezier(17.1f, 16.8f, 15.3f, 18.6f, 12.8f, 17.6f, 11.8f, 15.9f);
                        g.DrawPath(pen, path2); path2.Dispose();
                        g.DrawLine(pen, 8.8f, 15.2f, 15.2f, 8.8f);
                        break;
                    }
                case IconKind.Refresh:
                    g.DrawArc(pen, 3.2f, 4f, 17.6f, 17.6f, 210, 280);
                    g.DrawLines(pen, new PointF[] { new PointF(16.9f, 2.6f), new PointF(20.3f, 5.4f), new PointF(17f, 8.1f) });
                    break;
                case IconKind.Copy:
                    {
                        GraphicsPath path = Gfx.Rounded(new RectangleF(8.5f, 8.5f, 11.8f, 12.2f), 2f);
                        g.DrawPath(pen, path); path.Dispose();
                        g.DrawLines(pen, new PointF[] { new PointF(15.8f, 5.2f), new PointF(15.8f, 3.7f), new PointF(3.8f, 3.7f), new PointF(3.8f, 15.6f), new PointF(8.4f, 15.6f) });
                        break;
                    }
                case IconKind.Trash:
                    g.DrawLine(pen, 4.5f, 6.7f, 19.5f, 6.7f);
                    g.DrawLine(pen, 9.2f, 6.5f, 9.2f, 4.2f);
                    g.DrawLine(pen, 9.2f, 4.2f, 14.8f, 4.2f);
                    g.DrawLine(pen, 14.8f, 4.2f, 14.8f, 6.5f);
                    g.DrawLines(pen, new PointF[] { new PointF(6.4f, 6.8f), new PointF(7.4f, 20.5f), new PointF(16.6f, 20.5f), new PointF(17.6f, 6.8f) });
                    g.DrawLine(pen, 10.2f, 10.5f, 10.2f, 17.2f);
                    g.DrawLine(pen, 13.8f, 10.5f, 13.8f, 17.2f);
                    break;
                case IconKind.Plus:
                    g.DrawLine(pen, 12f, 5f, 12f, 19f);
                    g.DrawLine(pen, 5f, 12f, 19f, 12f);
                    break;
                case IconKind.External:
                    g.DrawLines(pen, new PointF[] { new PointF(14f, 4.5f), new PointF(19.5f, 4.5f), new PointF(19.5f, 10f) });
                    g.DrawLine(pen, 19.5f, 4.5f, 11f, 13f);
                    g.DrawLines(pen, new PointF[] { new PointF(17.2f, 13.4f), new PointF(17.2f, 19.2f), new PointF(4.8f, 19.2f), new PointF(4.8f, 6.8f), new PointF(10.6f, 6.8f) });
                    break;
                case IconKind.Clock:
                    g.DrawEllipse(pen, 3f, 3f, 18f, 18f);
                    g.DrawLines(pen, new PointF[] { new PointF(12f, 7f), new PointF(12f, 12.2f), new PointF(15.6f, 14.3f) });
                    break;
                case IconKind.Firewall:
                    {
                        GraphicsPath path = Gfx.Rounded(new RectangleF(3f, 3.5f, 18f, 17f), 2.4f);
                        g.DrawPath(pen, path); path.Dispose();
                        g.DrawLine(pen, 3f, 9f, 21f, 9f);
                        g.DrawLine(pen, 3f, 14.5f, 21f, 14.5f);
                        g.DrawLine(pen, 9f, 3.5f, 9f, 9f);
                        g.DrawLine(pen, 15f, 9f, 15f, 14.5f);
                        g.DrawLine(pen, 8f, 14.5f, 8f, 20.5f);
                        g.DrawLine(pen, 16f, 14.5f, 16f, 20.5f);
                        break;
                    }
                case IconKind.Alert:
                    {
                        GraphicsPath path = new GraphicsPath();
                        path.AddLine(12f, 3f, 22f, 20.2f);
                        path.AddLine(22f, 20.2f, 2f, 20.2f);
                        path.CloseFigure();
                        g.DrawPath(pen, path); path.Dispose();
                        g.DrawLine(pen, 12f, 9f, 12f, 14.5f);
                        g.FillEllipse(brush, 11.1f, 16.6f, 1.8f, 1.8f);
                        break;
                    }
                case IconKind.Info:
                    g.DrawEllipse(pen, 3f, 3f, 18f, 18f);
                    g.DrawLine(pen, 12f, 10.5f, 12f, 17f);
                    g.FillEllipse(brush, 11.1f, 6.2f, 1.8f, 1.8f);
                    break;
                case IconKind.ArrowRight:
                    g.DrawLine(pen, 3.5f, 12f, 20f, 12f);
                    g.DrawLines(pen, new PointF[] { new PointF(14.5f, 6.5f), new PointF(20f, 12f), new PointF(14.5f, 17.5f) });
                    break;
                case IconKind.Sparkle:
                    g.DrawLine(pen, 12f, 2.5f, 12f, 8.5f);
                    g.DrawLine(pen, 12f, 15.5f, 12f, 21.5f);
                    g.DrawLine(pen, 2.5f, 12f, 8.5f, 12f);
                    g.DrawLine(pen, 15.5f, 12f, 21.5f, 12f);
                    g.DrawLine(pen, 5.3f, 5.3f, 9f, 9f);
                    g.DrawLine(pen, 15f, 15f, 18.7f, 18.7f);
                    g.DrawLine(pen, 18.7f, 5.3f, 15f, 9f);
                    g.DrawLine(pen, 9f, 15f, 5.3f, 18.7f);
                    break;
                case IconKind.Lock:
                    g.DrawArc(pen, 6f, 3f, 12f, 12f, 190, 160);
                    GraphicsPath body = Gfx.Rounded(new RectangleF(4.5f, 10f, 15f, 10.8f), 2.2f);
                    g.DrawPath(pen, body); body.Dispose();
                    g.DrawLine(pen, 12f, 14f, 12f, 16.8f);
                    break;
                case IconKind.Bolt:
                    g.DrawPolygon(pen, new PointF[] { new PointF(13.3f, 2.5f), new PointF(4.5f, 13.6f), new PointF(11f, 13.6f), new PointF(10.3f, 21.5f), new PointF(19.5f, 10.2f), new PointF(13f, 10.2f) });
                    break;
            }
        }
    }
}
