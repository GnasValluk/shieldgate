// ShieldGate - khung chung cho năm màn hình.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShieldGate
{
    internal abstract class PageBase : UserControl
    {
        protected PageBase()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            AutoScroll = true;
        }

        protected int Pad { get { return Ui.Px(Theme.PagePad); } }
        protected int ContentWidth { get { return Math.Max(Ui.Px(320), ClientSize.Width - Pad * 2); } }

        public event Action<PageId> NavigateRequested;
        public event Action<string> UsbRequested;

        protected void Go(PageId id)
        {
            if (NavigateRequested != null) NavigateRequested(id);
        }

        protected void GoUsb(string drive)
        {
            if (UsbRequested != null) UsbRequested(drive);
        }

        public virtual void OnShown() { }
        public virtual bool Busy { get { return false; } }
        public virtual void CancelWork() { }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            PaintPage(e.Graphics);
        }

        protected virtual void PaintPage(Graphics g) { }

        protected Label MakeLabel(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text ?? "";
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        protected Label MakeHeading(string text)
        {
            return MakeLabel(text, Theme.H1, Theme.Ink);
        }

        protected FlatButton MakeButton(string text, ButtonKind kind)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Kind = kind;
            b.Height = Ui.Px(Theme.ControlHeight);
            return b;
        }

        protected LinkText MakeLink(string text, string icon)
        {
            LinkText l = new LinkText();
            l.Text = text;
            l.IconName = icon;
            l.Height = Ui.Px(22);
            return l;
        }

        protected static int TextWidth(string text, Font font)
        {
            return Ui.Measure(text, font).Width + Ui.Px(34);
        }

        // Đặt vị trí theo đơn vị thiết kế (đã nhân tỉ lệ DPI).
        protected static void Place(Control c, int x, int y, int w, int h)
        {
            c.Bounds = Ui.Rect(x, y, w, h);
        }
    }
}
