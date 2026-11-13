// ShieldGate - biểu tượng nét vẽ bằng GDI+ trên lưới 20x20.
// Không dùng ký tự Unicode làm biểu tượng (↻, ▶, ●) và không cần tệp ảnh đi kèm.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShieldGate
{
    internal static class Icons
    {
        private static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            float d = r * 2f;
            if (d > w) d = w;
            if (d > h) d = h;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static GraphicsPath ShieldPath(float s, float ox, float oy)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddLine(ox + 10 * s, oy + 2 * s, ox + 17 * s, oy + 4.6f * s);
            p.AddLine(ox + 17 * s, oy + 4.6f * s, ox + 17 * s, oy + 9.4f * s);
            p.AddBezier(ox + 17 * s, oy + 9.4f * s,
                        ox + 17 * s, oy + 14f * s,
                        ox + 13.8f * s, oy + 17f * s,
                        ox + 10 * s, oy + 18.4f * s);
            p.AddBezier(ox + 10 * s, oy + 18.4f * s,
                        ox + 6.2f * s, oy + 17f * s,
                        ox + 3 * s, oy + 14f * s,
                        ox + 3 * s, oy + 9.4f * s);
            p.AddLine(ox + 3 * s, oy + 9.4f * s, ox + 3 * s, oy + 4.6f * s);
            p.CloseFigure();
            return p;
        }

        public static void Draw(Graphics g, string name, RectangleF box, Color color)
        {
            float s = box.Width / 20f;
            float ox = box.X, oy = box.Y;

            SmoothingMode oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                using (Pen pen = new Pen(color, 1.5f * s))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    switch (name)
                    {
                        case "grid":
                            using (GraphicsPath a = RoundRect(ox + 3 * s, oy + 3 * s, 5.5f * s, 5.5f * s, 1.4f * s))
                            using (GraphicsPath b = RoundRect(ox + 11.5f * s, oy + 3 * s, 5.5f * s, 5.5f * s, 1.4f * s))
                            using (GraphicsPath c = RoundRect(ox + 3 * s, oy + 11.5f * s, 5.5f * s, 5.5f * s, 1.4f * s))
                            using (GraphicsPath d = RoundRect(ox + 11.5f * s, oy + 11.5f * s, 5.5f * s, 5.5f * s, 1.4f * s))
                            {
                                g.DrawPath(pen, a); g.DrawPath(pen, b);
                                g.DrawPath(pen, c); g.DrawPath(pen, d);
                            }
                            break;

                        case "usb":
                            using (GraphicsPath body = RoundRect(ox + 2.2f * s, oy + 7.2f * s, 11.6f * s, 5.6f * s, 1.4f * s))
                                g.DrawPath(pen, body);
                            using (GraphicsPath tip = RoundRect(ox + 13.9f * s, oy + 8.7f * s, 3.8f * s, 2.6f * s, 0.8f * s))
                                g.DrawPath(pen, tip);
                            g.DrawLine(pen, ox + 5.4f * s, oy + 10f * s, ox + 8.2f * s, oy + 10f * s);
                            break;

                        case "search":
                            g.DrawEllipse(pen, ox + 3.2f * s, oy + 3.2f * s, 9.6f * s, 9.6f * s);
                            g.DrawLine(pen, ox + 11.9f * s, oy + 11.9f * s, ox + 16.8f * s, oy + 16.8f * s);
                            break;

                        case "shield":
                            using (GraphicsPath sh = ShieldPath(s, ox, oy)) g.DrawPath(pen, sh);
                            break;

                        case "check":
                            g.DrawLines(pen, new PointF[]
                            {
                                new PointF(ox + 4.2f * s, oy + 10.4f * s),
                                new PointF(ox + 8.6f * s, oy + 14.8f * s),
                                new PointF(ox + 16.2f * s, oy + 5.4f * s)
                            });
                            break;

                        case "alert":
                            g.DrawPolygon(pen, new PointF[]
                            {
                                new PointF(ox + 10 * s, oy + 3 * s),
                                new PointF(ox + 17.6f * s, oy + 16.4f * s),
                                new PointF(ox + 2.4f * s, oy + 16.4f * s)
                            });
                            g.DrawLine(pen, ox + 10 * s, oy + 8 * s, ox + 10 * s, oy + 11.6f * s);
                            break;

                        case "list":
                            g.DrawLine(pen, ox + 7.6f * s, oy + 4.6f * s, ox + 16.6f * s, oy + 4.6f * s);
                            g.DrawLine(pen, ox + 7.6f * s, oy + 10 * s, ox + 16.6f * s, oy + 10 * s);
                            g.DrawLine(pen, ox + 7.6f * s, oy + 15.4f * s, ox + 12.6f * s, oy + 15.4f * s);
                            g.DrawEllipse(pen, ox + 3.4f * s, oy + 3.7f * s, 1.8f * s, 1.8f * s);
                            g.DrawEllipse(pen, ox + 3.4f * s, oy + 9.1f * s, 1.8f * s, 1.8f * s);
                            g.DrawEllipse(pen, ox + 3.4f * s, oy + 14.5f * s, 1.8f * s, 1.8f * s);
                            break;

                        case "info":
                            g.DrawEllipse(pen, ox + 2.6f * s, oy + 2.6f * s, 14.8f * s, 14.8f * s);
                            g.DrawLine(pen, ox + 10 * s, oy + 9.2f * s, ox + 10 * s, oy + 14 * s);
                            g.DrawLine(pen, ox + 10 * s, oy + 6.4f * s, ox + 10 * s, oy + 6.5f * s);
                            break;

                        case "chevron":
                            g.DrawLines(pen, new PointF[]
                            {
                                new PointF(ox + 8 * s, oy + 5.4f * s),
                                new PointF(ox + 12.6f * s, oy + 10 * s),
                                new PointF(ox + 8 * s, oy + 14.6f * s)
                            });
                            break;

                        case "refresh":
                            g.DrawArc(pen, ox + 3.4f * s, oy + 3.4f * s, 13.2f * s, 13.2f * s, 40, 275);
                            g.DrawLines(pen, new PointF[]
                            {
                                new PointF(ox + 12.1f * s, oy + 1.6f * s),
                                new PointF(ox + 16.2f * s, oy + 4.4f * s),
                                new PointF(ox + 13.2f * s, oy + 7.6f * s)
                            });
                            break;

                        case "undo":
                            g.DrawArc(pen, ox + 3.4f * s, oy + 3.4f * s, 13.2f * s, 13.2f * s, 130, 250);
                            g.DrawLines(pen, new PointF[]
                            {
                                new PointF(ox + 7.6f * s, oy + 2.6f * s),
                                new PointF(ox + 3.4f * s, oy + 5.6f * s),
                                new PointF(ox + 7.2f * s, oy + 8.4f * s)
                            });
                            break;
                    }
                }
            }
            finally
            {
                g.SmoothingMode = oldMode;
            }
        }

        // Logo: khiên đặc, một màu, không chữ.
        public static void DrawLogo(Graphics g, RectangleF box, Color color)
        {
            float s = box.Width / 20f;
            SmoothingMode oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                using (GraphicsPath sh = ShieldPath(s, box.X, box.Y))
                using (SolidBrush b = new SolidBrush(color))
                    g.FillPath(b, sh);

                using (Pen pen = new Pen(Color.White, 1.6f * s))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(box.X + 6.6f * s, box.Y + 9.8f * s),
                        new PointF(box.X + 9.2f * s, box.Y + 12.4f * s),
                        new PointF(box.X + 13.6f * s, box.Y + 7.4f * s)
                    });
                }
            }
            finally
            {
                g.SmoothingMode = oldMode;
            }
        }

        // Biểu tượng ứng dụng (cửa sổ chính và khay hệ thống).
        public static Icon AppIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    DrawLogo(g, new RectangleF(2, 2, 28, 28), Theme.Accent);
                }
                IntPtr h = bmp.GetHicon();
                return Icon.FromHandle(h);
            }
        }
    }
}
