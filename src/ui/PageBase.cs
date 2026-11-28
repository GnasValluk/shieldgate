// ShieldGate - khung chung cho năm màn hình.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShieldGate
{
    internal abstract class PageBase : UserControl
    {
        private readonly SmoothScroll _smooth;

        protected PageBase()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            AutoScroll = true;
            _smooth = new SmoothScroll(this);
        }

        protected int Pad { get { return Ui.Px(Theme.PagePad); } }
        protected int ContentWidth { get { return Math.Max(Ui.Px(320), ClientSize.Width - Pad * 2); } }
        protected int ScrollOffset { get { return -AutoScrollPosition.Y; } }

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

        // Trang tự cuộn control vào khung khi control nhận focus. Nếu người dùng
        // đang giữ chuột, cú cuộn đó dời control ngay giữa lúc nhấn và lúc nhả,
        // làm cú bấm bị nuốt. Giữ nguyên vị trí cuộn khi chuột còn nhấn.
        protected override Point ScrollToControl(Control activeControl)
        {
            if (Control.MouseButtons != MouseButtons.None) return AutoScrollPosition;
            return base.ScrollToControl(activeControl);
        }

        // Cuộn bằng timer thay vì nhảy 120 px mỗi nấc của WinForms.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // ContainerControl tự cuộn tới control đang focus sau mỗi lần bố
            // cục. Đánh dấu "người dùng đã cuộn" để bước đó không kéo trang
            // khỏi chỗ đang xem (lỗi hay gặp khi chuyển tab rồi quay lại).
            SetScrollState(ScrollStateUserHasScrolled, true);
            if (_smooth != null && _smooth.Wheel(e)) return;
            base.OnMouseWheel(e);
        }

        // Trang tự vẽ: cú cuộn của WinForms chỉ chép lại pixel, nên vẽ lại toàn
        // trang để không còn vệt của vị trí cũ ở rìa.
        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }

        // Đặt chiều cao nội dung. Vị trí cuộn do LayoutPage giữ, nên ở đây chỉ
        // cập nhật dải cuộn; không tự cuộn lại để tránh dời control con.
        protected void SetScrollHeight(int height)
        {
            Size size = new Size(0, Math.Max(0, height));
            if (AutoScrollMinSize != size) AutoScrollMinSize = size;
        }

        // Bố cục lại trang. WinForms luôn đặt control con theo toạ độ thiết kế
        // (bỏ qua vị trí cuộn), nên phải đưa trang về đỉnh, đặt toạ độ, rồi
        // cuộn lại; nếu không nội dung nhảy mỗi lần đổi kích thước, đổi trạng
        // thái, hoặc khi quay lại tab đang cuộn dở.
        protected void LayoutPage()
        {
            int offset = ScrollOffset;
            // Chặn ContainerControl tự cuộn tới control đang focus trong lúc
            // bố cục (xem OnMouseWheel).
            SetScrollState(ScrollStateUserHasScrolled, true);

            if (offset != 0) AutoScrollPosition = new Point(0, 0);
            LayoutPageCore();
            if (offset != 0)
            {
                // Bố cục có thể đã tự cuộn; đưa về đỉnh lần nữa để chắc chắn
                // control con đang ở toạ độ thiết kế trước khi cuộn lại.
                AutoScrollPosition = new Point(0, 0);
                AutoScrollPosition = new Point(0, offset);
            }
            else
            {
                AutoScrollPosition = new Point(0, 0);
            }
        }

        // Phần bố cục riêng của từng trang, đặt control theo toạ độ thiết kế.
        protected virtual void LayoutPageCore() { }

        // WinForms dời control con về toạ độ thiết kế khi trang bị ẩn rồi hiện
        // lại, trong khi vị trí cuộn vẫn giữ nguyên; bố cục lại sau khi hiện để
        // căn control con đúng với vị trí cuộn.
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && IsHandleCreated && Visible) LayoutPage();
                });
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _smooth != null) _smooth.Dispose();
            base.Dispose(disposing);
        }

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
