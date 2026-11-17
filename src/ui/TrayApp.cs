// ShieldGate - biểu tượng khay hệ thống cho bản bảo vệ chạy nền.
// Chạy trong phiên đăng nhập của người dùng để thấy được trạng thái bảo vệ.
using System;
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
        private readonly MenuItem _status;
        private DateTime _lastEvent = DateTime.MinValue;
        private DateTime _lastDeviceEvent = DateTime.MinValue;
        private DateTime _lastBehaviorEvent = DateTime.MinValue;

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

            // Bỏ qua sự kiện cũ trước khi biểu tượng này xuất hiện.
            _lastEvent = Settings.DriveEventTime;
            _lastDeviceEvent = Settings.DeviceEventTime;
            _lastBehaviorEvent = Settings.BehaviorEventTime;
            Refresh();
            try { _icon.ShowBalloonTip(5000); }
            catch { }

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 5000;
            _timer.Tick += delegate { Refresh(); };
            _timer.Start();
        }

        private void Refresh()
        {
            bool running = Guard.HeartbeatAge() < 30;
            string tooltip = running ? S.Tray.TooltipOn : S.Tray.TooltipOff;
            if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);
            _icon.Text = tooltip;
            _status.Text = running ? S.Tray.StatusOn : S.Tray.StatusOff;
            CheckDriveEvent();
            CheckDeviceEvents();
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

        private void CheckDeviceEvents()
        {
            DateTime device = Settings.DeviceEventTime;
            if (device != DateTime.MinValue && device != _lastDeviceEvent)
            {
                _lastDeviceEvent = device;
                ShowBalloon(Settings.DeviceEventText);
            }

            DateTime behavior = Settings.BehaviorEventTime;
            if (behavior != DateTime.MinValue && behavior != _lastBehaviorEvent)
            {
                _lastBehaviorEvent = behavior;
                ShowBalloon(S.Tray.Behavior(Settings.BehaviorEventDrive));
            }
        }

        // Bảo vệ nền ghi sự kiện vào settings.txt. Chỉ báo một lần cho mỗi mốc thời gian.
        private void CheckDriveEvent()
        {
            DateTime when = Settings.DriveEventTime;
            if (when == DateTime.MinValue || when == _lastEvent) return;
            _lastEvent = when;

            int cleaned = Settings.DriveEventCleaned;
            int review = Settings.DriveEventReview;
            string drive = Settings.DriveEventDrive;
            string text;
            if (cleaned > 0 && review > 0) text = S.Tray.CleanedReview(drive, cleaned, review);
            else if (cleaned > 0) text = S.Tray.Cleaned(drive, cleaned);
            else if (review > 0) text = S.Tray.Attention(review);
            else return;

            ShowBalloon(text);
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
            try { _timer.Stop(); } catch { }
            try { _icon.Visible = false; } catch { }
            try { _icon.Dispose(); } catch { }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null) _timer.Dispose();
                if (_icon != null) _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
