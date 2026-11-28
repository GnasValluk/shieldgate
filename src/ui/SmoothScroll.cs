// ShieldGate - cuộn mượt cho trang và danh sách dài.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShieldGate
{
    // WinForms cuộn một nấc chuột 120 px (ba "dòng" của hệ thống), nên trang
    // dài chỉ vài nấc là hết, mắt không bám được nội dung. Lớp này chia mỗi
    // nấc thành nhiều bước nhỏ chạy bằng timer, giảm tốc ở cuối, và cộng dồn
    // khi người dùng lăn liên tiếp để không bị khựng giữa chừng.
    internal sealed class SmoothScroll
    {
        private const int Duration = 150;   // ms cho một nấc
        private const int Interval = 15;    // nhịp cập nhật
        private const int LineHeight = 18;  // px thiết kế cho một dòng

        private readonly ScrollableControl _owner;
        private readonly Timer _timer;
        private int _from;
        private int _to;
        private int _started;
        private bool _active;

        public SmoothScroll(ScrollableControl owner)
        {
            _owner = owner;
            _timer = new Timer();
            _timer.Interval = Interval;
            _timer.Tick += delegate { Step(); };
        }

        // Trả về true khi đã nhận sự kiện, để nơi gọi không cuộn thêm lần nữa.
        public bool Wheel(MouseEventArgs e)
        {
            if (e == null || e.Delta == 0) return false;
            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null && handled.Handled) return true;

            int max = MaxOffset();
            if (max <= 0) return false;

            int notches = e.Delta / 120;
            if (notches == 0) notches = e.Delta > 0 ? 1 : -1;
            // Đang chạy thì cộng dồn vào đích cũ, nhưng luôn xuất phát từ vị trí
            // thật để không nhảy cóc tới đích của nấc trước.
            int current = -_owner.AutoScrollPosition.Y;
            int from = _active ? _to : current;
            int target = from - notches * WheelStep();
            if (target < 0) target = 0;
            if (target > max) target = max;
            if (target != current) Start(current, target);

            if (handled != null) handled.Handled = true;
            return true;
        }

        // Dừng ngay: dùng khi chuột nhấn xuống (người dùng kéo thanh cuộn).
        public void Stop()
        {
            if (!_active) return;
            _active = false;
            _timer.Stop();
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
        }

        private int MaxOffset()
        {
            int content = _owner.AutoScrollMinSize.Height;
            int view = _owner.ClientSize.Height;
            return content > view ? content - view : 0;
        }

        private int WheelStep()
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            // -1: hệ thống đặt chế độ cuộn cả trang.
            if (lines < 0) return Math.Max(Ui.Px(24), _owner.ClientSize.Height);
            if (lines == 0) lines = 3;
            return lines * Ui.Px(LineHeight);
        }

        private void Start(int from, int to)
        {
            _from = from;
            _to = to;
            _started = Environment.TickCount;
            _active = true;
            _timer.Start();
        }

        private void Step()
        {
            if (!_active) return;
            // Chuột đang nhấn: nhường quyền cho thanh cuộn.
            if (Control.MouseButtons != MouseButtons.None) { Stop(); return; }

            int elapsed = unchecked(Environment.TickCount - _started);
            if (elapsed >= Duration) { Apply(_to); Stop(); return; }

            double t = (double)elapsed / Duration;
            double ease = 1 - Math.Pow(1 - t, 3);
            Apply(_from + (int)Math.Round((_to - _from) * ease));
        }

        private void Apply(int y)
        {
            if (!_owner.IsHandleCreated) { Stop(); return; }
            _owner.AutoScrollPosition = new Point(0, y);
        }
    }

    // Danh sách tự vẽ (ổ USB, thiết bị, nhật ký, kết quả quét) dùng chung cách
    // cuộn mượt; phần vẽ và dữ liệu vẫn nằm ở từng lớp con.
    internal abstract class SmoothListControl : ScrollableControl
    {
        private readonly SmoothScroll _smooth;

        protected SmoothListControl()
        {
            _smooth = new SmoothScroll(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_smooth != null && _smooth.Wheel(e)) return;
            base.OnMouseWheel(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _smooth != null) _smooth.Dispose();
            base.Dispose(disposing);
        }
    }
}
