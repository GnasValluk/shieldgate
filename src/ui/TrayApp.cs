// ShieldGate - biểu tượng khay hệ thống cho bản bảo vệ chạy nền.
// Chạy trong phiên đăng nhập của người dùng để thấy được trạng thái bảo vệ.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal static class TrayApp
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate { };

            // Bảo vệ nền có thể chưa chạy; khôi phục khoá ghi ngay khi tray mở.
            try { Policy.RecoverPending(); } catch { }

            bool created;
            using (Mutex mutex = new Mutex(true, AppInfo.TrayMutexName, out created))
            {
                if (!created) return 0;

                using (TrayContext context = new TrayContext())
                    Application.Run(context);

                GC.KeepAlive(mutex);
            }
            return 0;
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly System.Windows.Forms.Timer _watch;
        private readonly MenuItem _status;
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly InputCollector _input;
        private readonly List<PendingBurst> _bursts = new List<PendingBurst>();
        private volatile List<DeviceInfo> _devices = new List<DeviceInfo>();
        private long _lastEventId;
        private DateTime _lastBalloon = DateTime.MinValue;
        private bool _badUsbOn;
        private bool _inputWarned;
        private int _deviceRefreshing;
        private int _refreshTicks;

        public TrayContext()
        {
            _status = new MenuItem("…");
            _status.Enabled = false;

            ContextMenu menu = new ContextMenu(new MenuItem[]
            {
                new MenuItem(S.Tray.Open, delegate { OpenUi(); }),
                _status,
                new MenuItem("-"),
                new MenuItem(S.Tray.Exit, delegate { ExitTray(); })
            });

            _icon = new NotifyIcon();
            _icon.Icon = Icons.TrayIcon(SystemInformation.SmallIconSize.Width);
            _icon.ContextMenu = menu;
            _icon.Text = S.Tray.StatusOn;
            _icon.DoubleClick += delegate { OpenUi(); };
            _icon.BalloonTipClicked += delegate { OpenUi(); };
            _icon.BalloonTipTitle = AppInfo.Name;
            _icon.BalloonTipText = S.Tray.Balloon;
            _icon.Visible = true;

            // W21/W22: đo tốc độ gõ và ghép shell. Chỉ đếm sự kiện, không đọc mã
            // phím; tắt công tắc thì không đăng ký Raw Input và không poll tiến trình.
            // Khởi tạo trước Refresh(): Refresh gọi SyncBadUsb.
            _input = new InputCollector();
            _input.Burst += delegate(InputBurst burst) { OnBurst(burst); };
            SyncBadUsb();
            RefreshDevices();

            // Bỏ qua sự kiện cũ trước khi biểu tượng này xuất hiện; từ đây chỉ
            // đọc các ID lớn hơn con trỏ.
            _lastEventId = Events.MaxId;
            Refresh();
            try { _icon.ShowBalloonTip(5000); }
            catch { }
            _lastBalloon = DateTime.Now;

            _watch = new System.Windows.Forms.Timer();
            _watch.Interval = 1000;
            _watch.Tick += delegate { CheckBursts(); };
            _watch.Start();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 5000;
            _timer.Tick += delegate { Refresh(); };
            _timer.Start();
        }

        // Bật/tắt đo theo công tắc trong trang Bảo vệ. Tắt thì dừng cả Raw Input
        // lẫn poll tiến trình, không chỉ bỏ qua cảnh báo.
        private void SyncBadUsb()
        {
            bool on = Settings.BadUsbEnabled;
            if (on == _badUsbOn) return;
            _badUsbOn = on;
            if (on)
            {
                if (!_input.Start() && !_inputWarned)
                {
                    _inputWarned = true;
                    Log.Write(S.Log.BadUsbUnavailable, "WARN");
                }
                ProcessWatch.Start();
            }
            else
            {
                _input.Stop();
                ProcessWatch.Stop();
                _bursts.Clear();
            }
        }

        private void OnBurst(InputBurst burst)
        {
            if (burst == null || !_badUsbOn) return;
            // Dòng đo của W21 vẫn giữ để đối chiếu trên máy thật; cảnh báo là dòng
            // WARN riêng khi đủ điều kiện.
            Log.Write(S.Log.BadUsbRate(burst.Device, burst.Events,
                                       burst.Seconds, burst.PeakPerSecond), "INFO");

            PendingBurst p = new PendingBurst();
            p.Burst = burst;
            p.Deadline = burst.Last.AddSeconds(Rules.Current.BadUsbShellSeconds);
            _bursts.Add(p);
            if (_bursts.Count > 8) _bursts.RemoveAt(0);
            CheckBursts();
        }

        // Shell có thể xuất hiện sau burst vài giây, nên giữ burst tới hạn rồi mới
        // kết luận. Không có shell trong cửa sổ thì bỏ, không cảnh báo.
        private void CheckBursts()
        {
            if (_bursts.Count == 0) return;
            int shellSeconds = Rules.Current.BadUsbShellSeconds;
            List<ShellStart> shells = ProcessWatch.ShellsSince(DateTime.MinValue);
            DateTime now = DateTime.UtcNow;

            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                PendingBurst p = _bursts[i];
                DeviceInfo device = FindDevice(p.Burst);
                if (device == null)
                {
                    if (now >= p.Deadline) _bursts.RemoveAt(i);
                    else RefreshDevices();
                    continue;
                }

                ShellStart match = null;
                foreach (ShellStart s in shells)
                {
                    if (s.Created < p.Burst.First.AddSeconds(-1)) continue;
                    if (s.Created > p.Burst.Last.AddSeconds(shellSeconds)) continue;
                    if (match == null || s.Created > match.Created) match = s;
                }
                if (match == null)
                {
                    if (now >= p.Deadline) _bursts.RemoveAt(i);
                    continue;
                }

                _bursts.RemoveAt(i);
                BadUsbVerdict verdict = BadUsb.Evaluate(p.Burst, device, match, now);
                if (!verdict.Alert) continue;

                string detail = S.Events.BadUsbAlert(device.Name, p.Burst.Device,
                    p.Burst.Events, p.Burst.Seconds, match.Name, match.ParentName);
                Log.Write(detail, "WARN");
                Events.Add(Events.KindDevice, "WARN", device.Name, detail);
            }
        }

        // Chỉ xét bàn phím ngoài. Khớp đường dẫn raw trước, cùng VID/PID sau.
        private DeviceInfo FindDevice(InputBurst burst)
        {
            List<DeviceInfo> list = _devices;
            if (list == null || burst == null) return null;
            DeviceInfo fallback = null;
            foreach (DeviceInfo d in list)
            {
                if (!d.Keyboard || Devices.IsInternalKeyboard(d)) continue;
                if (burst.Raw.Length > 0 && Devices.MatchesRaw(d, burst.Raw)) return d;
                if (fallback == null && LabelMatches(d, burst.Device)) fallback = d;
            }
            return fallback;
        }

        private static bool LabelMatches(DeviceInfo d, string label)
        {
            if (string.IsNullOrEmpty(label) || d == null) return false;
            if (d.Vid.Length == 0 || d.Pid.Length == 0) return false;
            return label.IndexOf("VID_" + d.Vid, StringComparison.OrdinalIgnoreCase) >= 0
                && label.IndexOf("PID_" + d.Pid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Kho thiết bị đọc trên thread nền; burst chỉ đọc tham chiếu danh sách.
        private void RefreshDevices()
        {
            if (Interlocked.CompareExchange(ref _deviceRefreshing, 1, 0) != 0) return;
            Thread t = new Thread(delegate()
            {
                try
                {
                    DeviceScanResult ds = Devices.Scan();
                    if (ds != null && string.IsNullOrEmpty(ds.Error) && ds.Devices != null)
                        _devices = ds.Devices;
                }
                catch { }
                finally { Interlocked.Exchange(ref _deviceRefreshing, 0); }
            });
            t.IsBackground = true;
            t.Name = "ShieldGateDeviceRefresh";
            t.Start();
        }

        private sealed class PendingBurst
        {
            public InputBurst Burst;
            public DateTime Deadline;
        }

        private void Refresh()
        {
            bool running = Guard.HeartbeatAge() < 30;
            string tooltip = running ? S.Tray.TooltipOn : S.Tray.TooltipOff;
            if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);
            _icon.Text = tooltip;
            _status.Text = running ? S.Tray.StatusOn : S.Tray.StatusOff;

            SyncBadUsb();
            if (_badUsbOn)
            {
                _refreshTicks++;
                if (_refreshTicks >= 6)
                {
                    _refreshTicks = 0;
                    RefreshDevices();
                }
            }

            DrainEvents();
            ShowNextBalloon();
        }

        // Đọc mọi sự kiện mới hơn con trỏ. Hàng đợi giữ lại từng thông báo thay
        // vì chỉ nhớ mốc thời gian cuối như trước (nhiều sự kiện trong 5 giây
        // từng bị mất).
        private void DrainEvents()
        {
            List<EventEntry> list;
            try { list = Events.ReadSince(_lastEventId); }
            catch { return; }
            foreach (EventEntry e in list)
            {
                _lastEventId = e.Id;
                string text = BalloonText(e);
                if (string.IsNullOrEmpty(text)) continue;
                _pending.Enqueue(text);
            }
            while (_pending.Count > 20) _pending.Dequeue();
        }

        private static string BalloonText(EventEntry e)
        {
            if (e == null) return "";
            switch (e.Kind)
            {
                case Events.KindDevice:
                    return string.Equals(e.Severity, "INFO", StringComparison.OrdinalIgnoreCase)
                        ? S.Tray.DeviceNew(e.Subject) : e.Detail;
                case Events.KindBehavior:
                    return S.Tray.Behavior(e.Subject);
                case Events.KindReinfect:
                    return string.Equals(e.Severity, "ERROR", StringComparison.OrdinalIgnoreCase)
                        ? S.Tray.ReinfectedAfterFix(e.Subject) : S.Tray.Reinfected(e.Subject);
                case Events.KindDrive:
                    return e.Detail;
                default:
                    return "";
            }
        }

        // Mỗi nhịp 5 giây hiện một thông báo để không ghi đè lên nhau.
        private void ShowNextBalloon()
        {
            if (_pending.Count == 0) return;
            if (_lastBalloon != DateTime.MinValue &&
                (DateTime.Now - _lastBalloon).TotalSeconds < 5) return;
            ShowBalloon(_pending.Dequeue());
            _lastBalloon = DateTime.Now;
        }

        private void ShowBalloon(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                _icon.BalloonTipText = text;
                _icon.ShowBalloonTip(8000);
            }
            catch { }
        }

        private static bool UiRunning()
        {
            try
            {
                using (Mutex existing = Mutex.OpenExisting(AppInfo.UiMutexName)) return true;
            }
            catch { return false; }
        }

        private void OpenUi()
        {
            if (UiRunning()) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }

        private void ExitTray()
        {
            try { ProcessWatch.Stop(); } catch { }
            try { _input.Dispose(); } catch { }
            try { _watch.Stop(); } catch { }
            try { _timer.Stop(); } catch { }
            try { _icon.Visible = false; } catch { }
            try { _icon.Dispose(); } catch { }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { ProcessWatch.Stop(); } catch { }
                if (_input != null) _input.Dispose();
                if (_watch != null) _watch.Dispose();
                if (_timer != null) _timer.Dispose();
                if (_icon != null) _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
