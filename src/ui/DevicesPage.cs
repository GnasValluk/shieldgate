// ShieldGate 2.0 - màn hình thiết bị: kiểm kê, tin cậy/chặn, dòng thời gian.
// Chặn ở đây chỉ là đánh dấu và cảnh báo; không tắt thiết bị.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class DevicesPage : PageBase
    {
        private readonly Label _title, _intro, _count, _devicesTitle, _eventsTitle, _eventsSub;
        private readonly LinkText _refresh, _forget, _exportCsv, _exportHtml;
        private readonly FlatButton _trust, _unknown, _block;
        private readonly DeviceListControl _devices;
        private readonly EventListControl _events;
        private readonly Notice _notice;
        private readonly ToolTip _tips;

        private readonly List<DeviceInfo> _found = new List<DeviceInfo>();
        private bool _busy;
        private bool _eventsLoading;

        private int _devicesLine, _eventsLine;

        public DevicesPage()
        {
            _title = MakeHeading(S.Devices.Title);
            Controls.Add(_title);

            _refresh = MakeLink(S.Devices.Refresh, "refresh");
            _refresh.Click += delegate { RefreshAll(); };
            Controls.Add(_refresh);

            _intro = MakeLabel(S.Devices.Intro, Theme.Body, Theme.InkMuted);
            _intro.AutoEllipsis = false;
            _intro.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(_intro);

            _devicesTitle = MakeLabel(S.Devices.ConnectedTitle, Theme.Section, Theme.Ink);
            Controls.Add(_devicesTitle);

            _count = MakeLabel("", Theme.Small, Theme.InkMuted);
            Controls.Add(_count);

            _notice = new Notice();
            Controls.Add(_notice);

            _devices = new DeviceListControl();
            _devices.EmptyText = S.Devices.Empty;
            _devices.SelectionChanged += delegate { UpdateButtons(); };
            Controls.Add(_devices);

            _trust = MakeButton(S.Devices.MarkTrusted, ButtonKind.Primary);
            _trust.IconName = "check";
            _trust.Click += delegate { SetTrust(DeviceTrust.Trusted); };
            Controls.Add(_trust);

            _unknown = MakeButton(S.Devices.MarkUnknown, ButtonKind.Secondary);
            _unknown.IconName = "undo";
            _unknown.Click += delegate { SetTrust(DeviceTrust.Unknown); };
            Controls.Add(_unknown);

            _block = MakeButton(S.Devices.MarkBlocked, ButtonKind.Danger);
            _block.IconName = "ban";
            _block.Click += delegate { SetTrust(DeviceTrust.Blocked); };
            Controls.Add(_block);

            // Thao tác phụ: liên kết, không chen ngang ba nút chính.
            _forget = MakeLink(S.Devices.Forget, "");
            _forget.Click += delegate { ForgetSelected(); };
            Controls.Add(_forget);

            _tips = new ToolTip();
            _tips.SetToolTip(_block, S.Devices.DisabledHint);
            _tips.SetToolTip(_forget, S.Devices.DisabledHint);

            _eventsTitle = MakeLabel(S.Devices.EventsTitle, Theme.Section, Theme.Ink);
            Controls.Add(_eventsTitle);

            _eventsSub = MakeLabel("", Theme.Small, Theme.InkMuted);
            Controls.Add(_eventsSub);

            _exportCsv = MakeLink(S.Devices.ExportCsv, "");
            _exportCsv.Click += delegate { Export(true); };
            Controls.Add(_exportCsv);

            _exportHtml = MakeLink(S.Devices.ExportHtml, "");
            _exportHtml.Click += delegate { Export(false); };
            Controls.Add(_exportHtml);

            _events = new EventListControl();
            _events.EmptyText = S.Devices.EventsEmpty;
            Controls.Add(_events);

            UpdateButtons();
            LayoutPage();
        }

        public override bool Busy { get { return _busy; } }

        public override void OnShown()
        {
            RefreshAll();
        }

        // --------------------------------------------------------------- quét

        private void RefreshAll()
        {
            if (_busy) return;
            _busy = true;
            _refresh.Enabled = false;
            _notice.SetMessage(S.Devices.Scanning, false);
            UpdateButtons();

            Thread t = new Thread(delegate()
            {
                DeviceScanResult ds = null;
                List<EventEntry> entries = new List<EventEntry>();
                try
                {
                    ds = Devices.Scan();
                    entries = ShieldGate.Events.Read(200);
                }
                catch (Exception e)
                {
                    Log.Write(S.Log.DeviceScanError(e.Message), "WARN");
                }
                RunOnUi(delegate { Fill(ds, entries); });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void Fill(DeviceScanResult ds, List<EventEntry> entries)
        {
            _busy = false;
            _refresh.Enabled = true;
            _found.Clear();
            if (ds != null) _found.AddRange(ds.Devices);

            _devices.SetItems(_found);
            _count.Text = S.Devices.Count(_found.Count);
            ApplyEvents(entries);

            UpdateNotice();
            UpdateButtons();
            LayoutPage();
        }

        private void UpdateNotice()
        {
            string warn = "";
            bool blocked = false;
            foreach (DeviceInfo d in _found)
            {
                if (d.Trust == DeviceTrust.Blocked)
                {
                    warn = S.Devices.BlockedNotice(d.Name);
                    blocked = true;
                    break;
                }
                if (d.CompositeSuspect && warn.Length == 0) warn = S.Devices.CompositeNotice(d.Name);
            }
            if (warn.Length > 0) _notice.SetMessage(warn, blocked);
            else if (_found.Count > 0) _notice.SetMessage(S.Devices.AllClear, false);
            else _notice.ClearMessage();
        }

        // ----------------------------------------------------------- dòng thời gian

        private void ApplyEvents(List<EventEntry> entries)
        {
            _events.SetItems(entries);
            // Lúc chưa có sự kiện, danh sách tự hiện câu trống; không lặp lại ở tiêu đề.
            _eventsSub.Text = entries.Count == 0 ? "" : S.Devices.EventCount(entries.Count);
            _exportCsv.Enabled = entries.Count > 0;
            _exportHtml.Enabled = entries.Count > 0;
            LayoutPage();
        }

        // Chỉ đọc lại tệp sự kiện sau khi đổi trạng thái; không quét WMI lần nữa
        // để nút bấm phản hồi ngay.
        private void LoadEventsAsync()
        {
            if (_eventsLoading) return;
            _eventsLoading = true;
            Thread t = new Thread(delegate()
            {
                List<EventEntry> entries = new List<EventEntry>();
                try { entries = ShieldGate.Events.Read(200); }
                catch { }
                RunOnUi(delegate
                {
                    _eventsLoading = false;
                    ApplyEvents(entries);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        // ------------------------------------------------------------- thao tác

        private void UpdateButtons()
        {
            bool enabled = _devices.Selected != null && !_busy;
            _trust.Enabled = enabled;
            _unknown.Enabled = enabled;
            _block.Enabled = enabled;
            _forget.Enabled = enabled;
        }

        private void SetTrust(DeviceTrust trust)
        {
            DeviceInfo d = _devices.Selected;
            if (d == null) { _notice.SetMessage(S.Devices.NoSelection, false); return; }
            if (!DeviceStore.SetTrust(d.Key, trust))
            {
                _notice.SetMessage(S.Devices.ActionFailed, true);
                return;
            }

            // Cập nhật ngay mô hình đang hiện: không chờ quét lại WMI.
            d.Trust = trust;
            _devices.Invalidate();
            ShieldGate.Events.Add(ShieldGate.Events.KindDevice,
                trust == DeviceTrust.Blocked ? "WARN" : "INFO",
                d.Name, S.Events.DeviceTrust(d.Name, S.Devices.TrustName(trust)));
            Log.Write(S.Log.DeviceTrustChanged(d.Name, S.Devices.TrustName(trust)));

            string message;
            if (trust == DeviceTrust.Trusted) message = S.Devices.TrustedDone(d.Name);
            else if (trust == DeviceTrust.Blocked) message = S.Devices.BlockedDone(d.Name);
            else message = S.Devices.UnknownDone(d.Name);
            _notice.SetMessage(message, trust == DeviceTrust.Blocked);
            UpdateButtons();
            LoadEventsAsync();
        }

        private void ForgetSelected()
        {
            DeviceInfo d = _devices.Selected;
            if (d == null) { _notice.SetMessage(S.Devices.NoSelection, false); return; }
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ForgetTitle, S.Dlg.ForgetBody(d.Name),
                                   S.Dlg.ForgetOk, S.Dlg.Cancel, ButtonKind.Danger, false))
                return;
            if (!DeviceStore.Forget(d.Key))
            {
                _notice.SetMessage(S.Devices.ActionFailed, true);
                return;
            }

            // Kho đã quên thiết bị: trả trạng thái về "chưa rõ" ngay trên danh sách.
            d.Trust = DeviceTrust.Unknown;
            _devices.Invalidate();
            ShieldGate.Events.Add(ShieldGate.Events.KindDevice, "INFO", d.Name,
                                  S.Events.DeviceTrust(d.Name, S.Devices.Unknown));
            _notice.SetMessage(S.Devices.ForgottenDone(d.Name), false);
            UpdateButtons();
            LoadEventsAsync();
        }

        private void Export(bool csv)
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = csv ? S.Devices.ExportCsv : S.Devices.ExportHtml;
                d.Filter = csv ? "CSV|*.csv" : "HTML|*.html";
                d.FileName = csv ? "shieldgate-events.csv" : "shieldgate-events.html";
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                bool ok = csv ? ShieldGate.Events.ExportCsv(d.FileName) : ShieldGate.Events.ExportHtml(d.FileName);
                if (ok) _notice.SetMessage(S.Devices.Exported(d.FileName), false);
                else _notice.SetMessage(S.Devices.ExportFailed, true);
            }
        }

        private void RunOnUi(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch { }
        }

        // --------------------------------------------------------------- bố cục

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutPage();
        }

        protected override void LayoutPageCore()
        {
            if (ClientSize.Width <= 0) return;
            int pad = Pad;
            int right = ClientSize.Width - pad;
            int width = right - pad;

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 340, 34);

            int refreshW = Ui.Measure(S.Devices.Refresh, Theme.Small).Width + Ui.Px(24);
            _refresh.Bounds = new Rectangle(right - refreshW, Ui.Px(28), refreshW, Ui.Px(22));

            // Hai dòng ở cỡ chữ hiện tại cần 42 px; 38 px làm mất hẳn dòng thứ
            // hai, nên nới ra và dời các mục bên dưới xuống 6 px.
            _intro.Bounds = new Rectangle(pad, Ui.Px(58), width, Ui.Px(44));

            _devicesTitle.Bounds = new Rectangle(pad, Ui.Px(110), Ui.Px(240), Ui.Px(20));
            _count.Bounds = new Rectangle(pad + Ui.Px(248), Ui.Px(110), width - Ui.Px(248), Ui.Px(20));
            _devicesLine = Ui.Px(132);

            _notice.Bounds = new Rectangle(pad, Ui.Px(138), width, Ui.Px(38));

            int listH = Ui.Px(240);
            _devices.Bounds = new Rectangle(pad, Ui.Px(182), width, listH);

            int by = Ui.Px(182) + listH + Ui.Px(10);
            int bw = Ui.Px(126);
            _trust.Bounds = new Rectangle(pad, by, bw, Ui.Px(Theme.ControlHeight));
            _unknown.Bounds = new Rectangle(pad + bw + Ui.Px(8), by, bw, Ui.Px(Theme.ControlHeight));
            _block.Bounds = new Rectangle(pad + (bw + Ui.Px(8)) * 2, by, bw, Ui.Px(Theme.ControlHeight));

            int forgetW = Ui.Measure(S.Devices.Forget, Theme.Small).Width + Ui.Px(4);
            _forget.Bounds = new Rectangle(right - forgetW, by + Ui.Px(8), forgetW, Ui.Px(22));

            int ey = by + Ui.Px(Theme.ControlHeight) + Ui.Px(16);
            _eventsTitle.Bounds = new Rectangle(pad, ey, Ui.Px(180), Ui.Px(20));
            _eventsLine = ey + Ui.Px(22);

            int linkY = ey - Ui.Px(2);
            int htmlW = Ui.Measure(S.Devices.ExportHtml, Theme.Small).Width + Ui.Px(4);
            int csvW = Ui.Measure(S.Devices.ExportCsv, Theme.Small).Width + Ui.Px(4);
            _exportHtml.Bounds = new Rectangle(right - htmlW, linkY, htmlW, Ui.Px(22));
            _exportCsv.Bounds = new Rectangle(_exportHtml.Left - Ui.Px(18) - csvW, linkY, csvW, Ui.Px(22));

            // Số sự kiện nằm cạnh tiêu đề, chừa chỗ cho hai liên kết xuất bên phải.
            int subX = pad + Ui.Px(188);
            _eventsSub.Bounds = new Rectangle(subX, ey,
                Math.Max(Ui.Px(120), _exportCsv.Left - Ui.Px(12) - subX), Ui.Px(20));

            int top = ey + Ui.Px(32);
            int h = Math.Max(Ui.Px(190), ClientSize.Height - top - Ui.Px(26));
            _events.Bounds = new Rectangle(pad, top, width, h);

            SetScrollHeight(top + h + Ui.Px(26));
        }

        protected override void PaintPage(Graphics g)
        {
            int pad = Pad;
            int right = ClientSize.Width - pad;
            Ui.HLine(g, pad, right, _devicesLine, Theme.Line);
            Ui.HLine(g, pad, right, _eventsLine, Theme.Line);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
