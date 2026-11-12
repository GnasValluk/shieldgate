// ShieldGate - nền vẽ dùng chung: tỉ lệ DPI, chữ, đường kẻ, nút, liên kết.
// Mọi kích thước đi qua Ui.Px để giao diện giữ đúng tỉ lệ ở 125% / 150%.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShieldGate
{
    internal static class Ui
    {
        public static float Scale = 1f;

        public static void InitScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            }
            catch { Scale = 1f; }
            if (Scale < 1f) Scale = 1f;
            if (Scale > 2f) Scale = 2f;
        }

        public static int Px(double v)
        {
            return (int)Math.Round(v * Scale);
        }

        public static Rectangle Rect(int x, int y, int w, int h)
        {
            return new Rectangle(Px(x), Px(y), Px(w), Px(h));
        }

        public static void Fill(Graphics g, Rectangle r, Color color)
        {
            using (SolidBrush b = new SolidBrush(color)) g.FillRectangle(b, r);
        }

        public static void HLine(Graphics g, int x1, int x2, int y, Color color)
        {
            if (x2 <= x1) return;
            Fill(g, new Rectangle(x1, y, x2 - x1, 1), color);
        }

        public static void Text(Graphics g, string text, Font font, Color color, Rectangle box,
                                TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text ?? "", font, box, color,
                flags | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static void TextLeft(Graphics g, string text, Font font, Color color, Rectangle box)
        {
            Text(g, text, font, color, box,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        public static void TextRight(Graphics g, string text, Font font, Color color, Rectangle box)
        {
            Text(g, text, font, color, box,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        public static Size Measure(string text, Font font)
        {
            return TextRenderer.MeasureText(text ?? "", font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void RoundFill(Graphics g, Rectangle r, int radius, Color color)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(b, p);
                g.SmoothingMode = old;
            }
        }

        public static void RoundStroke(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawPath(pen, p);
                g.SmoothingMode = old;
            }
        }

        public static void Chip(Graphics g, string text, Font font, Rectangle box, Color back, Color fore)
        {
            RoundFill(g, box, Px(3), back);
            Text(g, text, font, fore, box,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        // "1,2 MB" - dấu phẩy thập phân theo quy tắc.
        public static string Size(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] unit = { "B", "KB", "MB", "GB" };
            double v = bytes;
            int i = 0;
            while (v >= 1024 && i < unit.Length - 1) { v /= 1024; i++; }
            string number = i == 0 ? ((long)Math.Round(v)).ToString("#,0") : v.ToString("0.#");
            return number.Replace('.', ',') + " " + unit[i];
        }

        public static Font Semibold(float points)
        {
            try { return new Font("Segoe UI Semibold", points, FontStyle.Regular); }
            catch { return new Font("Segoe UI", points, FontStyle.Bold); }
        }
    }

    // ------------------------------------------------------------------ nút

    internal enum ButtonKind { Primary, Secondary, Quiet, Danger }

    internal sealed class FlatButton : Control
    {
        private ButtonKind _kind = ButtonKind.Primary;
        private bool _hot, _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.BodyStrong;
            Height = Ui.Px(Theme.ControlHeight);
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public ButtonKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Surface : Parent.BackColor);

            Rectangle r = new Rectangle(0, 0, Width, Height);
            Rectangle inner = Rectangle.Inflate(r, -1, -1);
            Color fill, fore, border = Color.Empty;

            if (!Enabled)
            {
                fill = _kind == ButtonKind.Quiet ? Color.Empty : Theme.Canvas;
                fore = Theme.InkFaint;
                if (_kind == ButtonKind.Secondary) border = Theme.LineSoft;
            }
            else if (_kind == ButtonKind.Primary)
            {
                fill = _down ? Theme.AccentHover : (_hot ? Theme.AccentHover : Theme.Accent);
                fore = Color.White;
            }
            else if (_kind == ButtonKind.Danger)
            {
                fill = _down ? Theme.Danger : (_hot ? Color.FromArgb(0xA8, 0x24, 0x24) : Theme.Danger);
                fore = Color.White;
            }
            else if (_kind == ButtonKind.Secondary)
            {
                fill = _down ? Theme.Pressed : (_hot ? Theme.HoverSoft : Theme.Surface);
                fore = Theme.Ink;
                border = Theme.BorderStrong;
            }
            else
            {
                fill = _down ? Theme.Pressed : (_hot ? Theme.HoverSoft : Color.Empty);
                fore = Theme.Accent;
            }

            if (fill != Color.Empty) Ui.RoundFill(g, inner, Ui.Px(3), fill);
            if (border != Color.Empty) Ui.RoundStroke(g, Rectangle.Inflate(inner, 0, 0), Ui.Px(3), border, 1f);

            Ui.Text(g, Text, Font, fore, inner,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues && Enabled)
            {
                Rectangle ring = Rectangle.Inflate(inner, -Ui.Px(2), -Ui.Px(2));
                ControlPaint.DrawFocusRectangle(g, ring, fore, fill == Color.Empty ? Parent.BackColor : fill);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Enabled) return;
            _hot = true;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false; _down = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            _down = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            bool click = _down;
            _down = false;
            Invalidate();
            if (click && ClientRectangle.Contains(e.Location)) OnClick(EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Default : Cursors.Default;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Space || k == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (Enabled && (keyData == Keys.Enter || keyData == Keys.Space))
            {
                OnClick(EventArgs.Empty);
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }

    // ------------------------------------------------------------- liên kết

    internal sealed class LinkText : Control
    {
        private bool _hot;
        private string _icon = "";
        private int _iconSize = 13;

        public LinkText()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Small;
            Height = Ui.Px(22);
            TabStop = true;
            AccessibleRole = AccessibleRole.Link;
        }

        public string IconName
        {
            get { return _icon; }
            set { _icon = value ?? ""; Invalidate(); }
        }

        public int IconSize
        {
            get { return _iconSize; }
            set { _iconSize = value; Invalidate(); }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Surface : Parent.BackColor);

            Color fore = Enabled ? (_hot || Focused ? Theme.AccentHover : Theme.Accent) : Theme.InkFaint;
            int x = 0;
            if (_icon.Length > 0)
            {
                int s = Ui.Px(_iconSize);
                Icons.Draw(g, _icon, new Rectangle(x, (Height - s) / 2, s, s), fore);
                x += s + Ui.Px(6);
            }

            Rectangle textRect = new Rectangle(x, 0, Math.Max(0, Width - x), Height);
            Ui.TextLeft(g, Text, Font, fore, textRect);

            if ((_hot || Focused) && Enabled)
            {
                Size sz = Ui.Measure(Text, Font);
                int underlineWidth = Math.Min(sz.Width, Width - x);
                Ui.HLine(g, x, x + underlineWidth, Height / 2 + sz.Height / 2 - Ui.Px(1), fore);
            }

            if (Focused && ShowFocusCues && Enabled)
                ControlPaint.DrawFocusRectangle(g, ClientRectangle, fore, Parent.BackColor);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Enabled) return;
            _hot = true;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            if (ClientRectangle.Contains(e.Location)) OnClick(EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Space || k == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (Enabled && (keyData == Keys.Enter || keyData == Keys.Space))
            {
                OnClick(EventArgs.Empty);
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }

    // ------------------------------------------------------------- biểu tượng

    internal sealed class LogoView : Control
    {
        public LogoView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            SetStyle(ControlStyles.Selectable, false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Surface : Parent.BackColor);
            Icons.DrawLogo(e.Graphics, new RectangleF(0, 0, Width, Height), Theme.Accent);
        }
    }

    internal sealed class Glyph : Control
    {
        private string _name = "check";
        private Color _color = Theme.InkMuted;

        public Glyph()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            SetStyle(ControlStyles.Selectable, false);
        }

        public string IconName
        {
            get { return _name; }
            set { _name = value ?? ""; Invalidate(); }
        }

        public Color IconColor
        {
            get { return _color; }
            set { _color = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Surface : Parent.BackColor);
            Icons.Draw(e.Graphics, _name, new Rectangle(0, 0, Width, Height), _color);
        }
    }

    // ---------------------------------------------------------------- vòng xoay

    internal sealed class Spinner : Control
    {
        private readonly Timer _timer;
        private int _angle;

        public Spinner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
            AccessibleRole = AccessibleRole.Animation;
            SetStyle(ControlStyles.Selectable, false);
            _timer = new Timer();
            _timer.Interval = 70;
            _timer.Tick += delegate { _angle = (_angle + 30) % 360; Invalidate(); };
        }

        public void Run(bool running)
        {
            if (running) { if (!_timer.Enabled) _timer.Start(); }
            else { _timer.Stop(); _angle = 0; }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Surface : Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(Ui.Px(3), Ui.Px(3), Width - Ui.Px(6), Height - Ui.Px(6));
            using (Pen pen = new Pen(Theme.Accent, Math.Max(1.5f, Ui.Px(2))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, r, _angle, 280);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------- thanh lọc

    internal sealed class Segment : Control
    {
        private string[] _options = new string[0];
        private int _selected;
        private int _hot = -1;

        public Segment()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Small;
            Height = Ui.Px(30);
            TabStop = true;
            AccessibleRole = AccessibleRole.PageTabList;
        }

        public string[] Options
        {
            get { return _options; }
            set
            {
                _options = value ?? new string[0];
                _selected = 0;
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int v = Math.Max(0, Math.Min(_options.Length - 1, value));
                if (v == _selected) return;
                _selected = v;
                AccessibleName = _options.Length > 0 ? _options[_selected] : "";
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler SelectedChanged;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Surface : Parent.BackColor);
            if (_options.Length == 0) return;

            int x = 0;
            for (int i = 0; i < _options.Length; i++)
            {
                Size sz = Ui.Measure(_options[i], Font);
                int w = sz.Width + Ui.Px(12);
                bool sel = i == _selected;
                Color fore = sel ? Theme.Ink : (_hot == i ? Theme.Ink : Theme.InkMuted);
                Ui.Text(g, _options[i], sel ? Theme.BodyStrong : Font, fore,
                    new Rectangle(x, 0, w, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                if (sel) Ui.HLine(g, x, x + Math.Min(w, sz.Width), Height - Ui.Px(2), Theme.Accent);
                x += w + Ui.Px(8);
            }

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, ClientRectangle, Theme.Ink, Parent.BackColor);
        }

        private int Hit(Point p)
        {
            int x = 0;
            for (int i = 0; i < _options.Length; i++)
            {
                Size sz = Ui.Measure(_options[i], Font);
                int w = sz.Width + Ui.Px(12);
                if (p.X >= x && p.X <= x + w) return i;
                x += w + Ui.Px(8);
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.Location);
            if (h != _hot)
            {
                _hot = h;
                Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int h = Hit(e.Location);
            if (h >= 0) SelectedIndex = h;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Left || k == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) { SelectedIndex = _selected - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { SelectedIndex = _selected + 1; e.Handled = true; }
        }
    }
}
