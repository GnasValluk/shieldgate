// ShieldGate - màn hình Tổng quan: trạng thái máy, ổ USB, bảo vệ, cách ly.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class OverviewPage : PageBase
    {
        private enum PrimaryMode { Scan, Cancel, ShowFindings, GoProtect }

        private readonly StatusInfo _status = new StatusInfo();
        private List<PcItem> _findings = new List<PcItem>();
        private bool _scanning;
        private volatile bool _cancel;
        private volatile string _stage = "";
        private bool _refreshing;
        private PrimaryMode _primaryMode = PrimaryMode.Scan;

        private readonly System.Windows.Forms.Timer _stageTimer;
        private Label _title, _stateTitle, _stateSub, _drivesTitle, _emptyTitle, _emptyHint;
        private Label _protectTitle, _protectText, _quarTitle, _quarText;
        private Glyph _stateIcon;
        private FlatButton _primary;
        private LinkText _secondary, _refresh, _protectLink, _quarLink, _quarClean;
        private DriveList _drives;

        private int _drivesLine, _protectLine, _quarLine, _protectRowY;

        public OverviewPage()
        {
            AutoScroll = true;

            _title = MakeHeading(S.Overview.Title);
            Controls.Add(_title);

            _refresh = MakeLink(S.Overview.Refresh, "refresh");
            _refresh.Click += delegate { RefreshAsync(); };
            Controls.Add(_refresh);

            _stateIcon = new Glyph();
            _stateIcon.IconName = "search";
            _stateIcon.IconColor = Theme.Accent;
            Controls.Add(_stateIcon);

            _stateTitle = MakeLabel("", Theme.Hero, Theme.Ink);
            Controls.Add(_stateTitle);

            _stateSub = MakeLabel("", Theme.Body, Theme.InkMuted);
            _stateSub.AutoEllipsis = true;
            Controls.Add(_stateSub);

            _primary = MakeButton(S.Overview.ActionScan, ButtonKind.Primary);
            _primary.Click += delegate { OnPrimary(); };
            Controls.Add(_primary);

            _secondary = MakeLink(S.Overview.ActionLog, "");
            _secondary.Click += delegate { Go(PageId.Log); };
            Controls.Add(_secondary);

            _drivesTitle = MakeLabel(S.Overview.Drives, Theme.Section, Theme.Ink);
            Controls.Add(_drivesTitle);

            _emptyTitle = MakeLabel(S.Overview.DrivesEmpty, Theme.Body, Theme.Ink);
            Controls.Add(_emptyTitle);

            _emptyHint = MakeLabel(S.Overview.DrivesEmptyHint, Theme.Small, Theme.InkMuted);
            _emptyHint.AutoEllipsis = true;
            Controls.Add(_emptyHint);

            _drives = new DriveList();
            _drives.RowClicked += delegate(string drive) { GoUsb(drive); };
            Controls.Add(_drives);

            _protectTitle = MakeLabel(S.Overview.Protect, Theme.Section, Theme.Ink);
            Controls.Add(_protectTitle);

            _protectText = MakeLabel("", Theme.Body, Theme.Ink);
            _protectText.AutoEllipsis = true;
            Controls.Add(_protectText);

            _protectLink = MakeLink(S.Overview.ProtectAction, "");
            _protectLink.Click += delegate { Go(PageId.Protect); };
            Controls.Add(_protectLink);

            _quarTitle = MakeLabel(S.Overview.Quarantine, Theme.Section, Theme.Ink);
            Controls.Add(_quarTitle);

            _quarText = MakeLabel("", Theme.Body, Theme.Ink);
            _quarText.AutoEllipsis = true;
            Controls.Add(_quarText);

            _quarLink = MakeLink(S.Overview.QuarantineAction, "");
            _quarLink.Click += delegate { ShowRestore(); };
            Controls.Add(_quarLink);

            _quarClean = MakeLink(S.Overview.QuarantineClean, "");
            _quarClean.Click += delegate { CleanQuarantine(); };
            Controls.Add(_quarClean);

            _stageTimer = new System.Windows.Forms.Timer();
            _stageTimer.Interval = 120;
            _stageTimer.Tick += delegate
            {
                if (_scanning) _stateSub.Text = _stage;
            };

            UpdateState();
            LayoutPage();
        }

        public override bool Busy { get { return _scanning; } }

        public override void CancelWork()
        {
            _cancel = true;
            _stageTimer.Stop();
        }

        public override void OnShown()
        {
            RefreshAsync();
        }

        // ------------------------------------------------------------ trạng thái

        private void UpdateState()
        {
            int attention = Attention(_findings);
            if (_scanning)
            {
                _primaryMode = PrimaryMode.Cancel;
                _stateIcon.IconName = "search";
                _stateIcon.IconColor = Theme.Accent;
                _stateTitle.Text = S.Overview.StateScanning;
                _stateSub.Text = _stage.Length > 0 ? _stage : S.Pc.StageStartup;
                _primary.Text = S.Overview.Cancel;
                _primary.Kind = ButtonKind.Secondary;
                _secondary.Visible = false;
            }
            else if (attention > 0)
            {
                _primaryMode = PrimaryMode.ShowFindings;
                _stateIcon.IconName = "alert";
                _stateIcon.IconColor = Theme.Warn;
                _stateTitle.Text = S.Overview.StateAttentionFiles(attention);
                _stateSub.Text = S.Overview.SubAttention(attention);
                _primary.Text = S.Overview.ActionSeeFiles(attention);
                _primary.Kind = ButtonKind.Primary;
                _secondary.Visible = true;
            }
            else if (Settings.LastPcScanTime == DateTime.MinValue)
            {
                _primaryMode = PrimaryMode.Scan;
                _stateIcon.IconName = "search";
                _stateIcon.IconColor = Theme.Accent;
                _stateTitle.Text = S.Overview.StateNeverTitle;
                _stateSub.Text = S.Overview.SubNever;
                _primary.Text = S.Overview.ActionScan;
                _primary.Kind = ButtonKind.Primary;
                _secondary.Visible = true;
            }
            else if (Settings.LastPcFound > 0)
            {
                _primaryMode = PrimaryMode.Scan;
                _stateIcon.IconName = "alert";
                _stateIcon.IconColor = Theme.Warn;
                _stateTitle.Text = S.Overview.StateAttentionFiles(Settings.LastPcFound);
                _stateSub.Text = S.Overview.SubAttentionUnseen(Settings.LastPcFound);
                _primary.Text = S.Overview.ActionScan;
                _primary.Kind = ButtonKind.Primary;
                _secondary.Visible = true;
            }
            else if (_status.FullyProtected)
            {
                _primaryMode = PrimaryMode.Scan;
                _stateIcon.IconName = "check";
                _stateIcon.IconColor = Theme.Safe;
                _stateTitle.Text = S.Overview.StateSafe;
                _stateSub.Text = S.Overview.SubScannedShort(Settings.When(Settings.LastPcScanTime));
                _primary.Text = S.Overview.ActionScan;
                _primary.Kind = ButtonKind.Primary;
                _secondary.Visible = true;
            }
            else
            {
                _primaryMode = PrimaryMode.GoProtect;
                _stateIcon.IconName = "shield";
                _stateIcon.IconColor = Theme.Accent;
                _stateTitle.Text = S.Overview.ProtectOff;
                _stateSub.Text = S.Overview.SubScannedUnprotected(Settings.When(Settings.LastPcScanTime));
                _primary.Text = S.Overview.ActionProtect;
                _primary.Kind = ButtonKind.Primary;
                _secondary.Visible = true;
            }

            if (_primaryMode == PrimaryMode.Cancel) _stageTimer.Start();
            else _stageTimer.Stop();

            LayoutPage();
        }

        private void OnPrimary()
        {
            switch (_primaryMode)
            {
                case PrimaryMode.Cancel:
                    _cancel = true;
                    break;
                case PrimaryMode.ShowFindings:
                    ShowFindings();
                    break;
                case PrimaryMode.GoProtect:
                    Go(PageId.Protect);
                    break;
                default:
                    StartScan();
                    break;
            }
        }

        // -------------------------------------------------------------- kiểm tra

        private void StartScan()
        {
            if (_scanning) return;
            _scanning = true;
            _cancel = false;
            _stage = S.Pc.StageStartup;
            UpdateState();

            Thread t = new Thread(delegate()
            {
                List<PcItem> found;
                try
                {
                    found = PcScan.Detect(delegate(string stage) { _stage = stage; },
                                          delegate { return _cancel; });
                }
                catch (Exception e)
                {
                    Log.Write(S.Log.PcScanError(e.Message), "ERROR");
                    found = new List<PcItem>();
                }
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { FinishScan(found); });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void FinishScan(List<PcItem> found)
        {
            _stageTimer.Stop();
            bool cancelled = _cancel;
            _scanning = false;
            _cancel = false;

            if (!cancelled)
            {
                _findings = found;
                int attention = Attention(found);
                Settings.SetPcScan(0, attention);
                if (found.Count > 0) Log.Write(S.Log.PcScanFound(found.Count));
                else Log.Write(S.Log.PcScanClean);
            }

            UpdateState();
            RefreshAsync();
            if (!cancelled && Attention(found) > 0) ShowFindings();
        }

        private void ShowFindings()
        {
            if (_findings.Count == 0) return;
            using (PcResultForm f = new PcResultForm(_findings))
            {
                if (FindForm() != null) f.ShowDialog(FindForm());
                else f.ShowDialog();
                _findings = new List<PcItem>(f.Remaining);
            }
            Settings.SetPcScan(0, Attention(_findings));
            UpdateState();
            RefreshAsync();
        }

        // Chỉ đếm mục sửa được. Mục chỉ để tham khảo (Defender) không cần người dùng xử lý.
        private static int Attention(IList<PcItem> items)
        {
            int n = 0;
            if (items != null)
                foreach (PcItem item in items)
                    if (item.Fixable) n++;
            return n;
        }

        // ----------------------------------------------------------- làm mới nền

        private void RefreshAsync()
        {
            if (_refreshing) return;
            _refreshing = true;

            Thread t = new Thread(delegate()
            {
                StatusInfo st = null;
                List<string> drives = new List<string>();
                try { st = Guard.GetStatus(); }
                catch { }
                try { drives = Native.RemovableDrives(); }
                catch { }

                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { ApplyStatus(st, drives); });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void ApplyStatus(StatusInfo st, List<string> driveRoots)
        {
            _refreshing = false;
            if (st != null)
            {
                _status.Admin = st.Admin;
                _status.Autorun = st.Autorun;
                _status.Ini = st.Ini;
                _status.Wsh = st.Wsh;
                _status.Registry = st.Registry;
                _status.Task = st.Task;
                _status.Running = st.Running;
                _status.QuarantineFiles = st.QuarantineFiles;
                _status.QuarantineBatches = st.QuarantineBatches;
            }

            List<DriveRow> rows = new List<DriveRow>();
            foreach (string root in driveRoots)
            {
                DateTime when = Settings.DriveScanTime(root);
                int found = Settings.DriveScanFound(root);
                DriveRow row = new DriveRow();
                row.Drive = root;
                row.Title = UsbScan.DriveLabel(root);
                if (when == DateTime.MinValue)
                {
                    row.Sub = S.Overview.DriveSubNew(UsbScan.DriveFileSystem(root));
                    row.Chip = S.Overview.ChipNew;
                    row.ChipColor = Theme.InkMuted;
                    row.ChipBack = Theme.HoverSoft;
                }
                else
                {
                    row.Sub = S.Overview.DriveSub(UsbScan.DriveFileSystem(root),
                        Settings.DriveScanFiles(root), Settings.When(when));
                    if (found > 0)
                    {
                        row.Attention = true;
                        row.Chip = S.Overview.ChipAttention(found);
                        row.ChipColor = Theme.Warn;
                        row.ChipBack = Theme.WarnSoft;
                    }
                    else
                    {
                        row.Chip = S.Overview.ChipSafe;
                        row.ChipColor = Theme.Safe;
                        row.ChipBack = Theme.SafeSoft;
                    }
                }
                rows.Add(row);
            }
            _drives.SetRows(rows);

            int missing = 0;
            if (!_status.Autorun) missing++;
            if (!_status.Ini) missing++;
            if (!_status.Registry) missing++;
            if (!_status.Running) missing++;

            if (missing == 0)
            {
                _protectText.Text = S.Overview.ProtectOk;
                _protectDot = Theme.Safe;
            }
            else if (!_status.Autorun && !_status.Ini && !_status.Registry && !_status.Running)
            {
                _protectText.Text = S.Overview.ProtectOff;
                _protectDot = Theme.InkFaint;
            }
            else
            {
                _protectText.Text = S.Overview.ProtectPartial(missing);
                _protectDot = Theme.Warn;
            }

            if (_status.QuarantineFiles > 0)
            {
                _quarText.Text = S.Overview.QuarantineCount(_status.QuarantineFiles);
                _quarText.ForeColor = Theme.Ink;
                _quarLink.Visible = true;
                _quarClean.Visible = true;
            }
            else
            {
                _quarText.Text = S.Overview.QuarantineEmpty;
                _quarText.ForeColor = Theme.InkMuted;
                _quarLink.Visible = false;
                _quarClean.Visible = false;
            }

            UpdateState();
        }

        private void ShowRestore()
        {
            using (RestoreForm f = new RestoreForm())
            {
                if (FindForm() != null) f.ShowDialog(FindForm());
                else f.ShowDialog();
            }
            RefreshAsync();
        }

        // Dọn sạch toàn bộ tệp đang cách ly ngay từ màn hình Tổng quan.
        private void CleanQuarantine()
        {
            if (_status.QuarantineFiles <= 0) return;
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.CleanAllTitle, S.Dlg.CleanAllBody,
                                   S.Dlg.CleanOk, S.Dlg.Close, ButtonKind.Danger, false))
                return;

            int batches, files;
            Quarantine.RemoveAll(out batches, out files);
            Log.Write(S.Log.QuarantineCleanedAll(batches, files), "WARN");
            RefreshAsync();
        }

        // -------------------------------------------------------------- bố cục

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutPage();
        }

        private void LayoutPage()
        {
            if (ClientSize.Width <= 0) return;

            int pad = Pad;
            int right = ClientSize.Width - pad;
            int width = right - pad;

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            int refreshW = Ui.Measure(S.Overview.Refresh, Theme.Small).Width + Ui.Px(24);
            _refresh.Bounds = new Rectangle(right - refreshW, Ui.Px(28), refreshW, Ui.Px(22));

            _stateIcon.Bounds = Ui.Rect(Theme.PagePad, 82, 24, 24);
            _stateTitle.Bounds = new Rectangle(pad + Ui.Px(34), Ui.Px(76),
                width - Ui.Px(34), Ui.Px(32));
            _stateSub.Bounds = new Rectangle(pad + Ui.Px(34), Ui.Px(108),
                width - Ui.Px(34), Ui.Px(24));

            _primary.Bounds = new Rectangle(pad, Ui.Px(152),
                TextWidth(_primary.Text, Theme.BodyStrong), Ui.Px(Theme.ControlHeight));

            int secW = Ui.Measure(_secondary.Text, Theme.Small).Width + Ui.Px(4);
            _secondary.Bounds = new Rectangle(_primary.Right + Ui.Px(22), Ui.Px(160),
                secW, Ui.Px(22));

            _drivesTitle.Bounds = Ui.Rect(Theme.PagePad, 214, 200, 20);
            _drivesLine = Ui.Px(240);

            _drives.Bounds = new Rectangle(pad, Ui.Px(248), width, _drives.Height);
            bool hasDrives = _drives.Rows.Count > 0;
            _drives.Visible = hasDrives;
            _emptyTitle.Visible = !hasDrives;
            _emptyHint.Visible = !hasDrives;

            int afterDrives;
            if (hasDrives)
            {
                afterDrives = Ui.Px(248) + _drives.Rows.Count * _drives.RowHeight;
            }
            else
            {
                _emptyTitle.Bounds = new Rectangle(pad, Ui.Px(250), width, Ui.Px(22));
                _emptyHint.Bounds = new Rectangle(pad, Ui.Px(274), width, Ui.Px(20));
                afterDrives = Ui.Px(304);
            }

            int protectY = afterDrives + Ui.Px(22);
            _protectTitle.Bounds = new Rectangle(pad, protectY, Ui.Px(200), Ui.Px(20));
            _protectLine = protectY + Ui.Px(24);
            _protectRowY = protectY + Ui.Px(34);

            int plw = Ui.Measure(S.Overview.ProtectAction, Theme.Small).Width;
            _protectLink.Bounds = new Rectangle(right - plw, _protectRowY + Ui.Px(8), plw, Ui.Px(22));
            int protectTextX = pad + Ui.Px(18);
            _protectText.Bounds = new Rectangle(protectTextX, _protectRowY,
                Math.Max(Ui.Px(120), _protectLink.Left - Ui.Px(12) - protectTextX), Ui.Px(40));

            int quarY = _protectRowY + Ui.Px(48);
            _quarTitle.Bounds = new Rectangle(pad, quarY, Ui.Px(200), Ui.Px(20));
            _quarLine = quarY + Ui.Px(24);
            int qlw = Ui.Measure(S.Overview.QuarantineAction, Theme.Small).Width;
            int qcw = Ui.Measure(S.Overview.QuarantineClean, Theme.Small).Width;
            _quarClean.Bounds = new Rectangle(right - qcw, quarY + Ui.Px(42), qcw, Ui.Px(22));
            _quarLink.Bounds = new Rectangle(_quarClean.Left - Ui.Px(20) - qlw,
                quarY + Ui.Px(42), qlw, Ui.Px(22));
            // Chừa chỗ cho hai liên kết bên phải: nhãn trong suốt vẽ đè lên vùng
            // của chúng và làm mất chữ nếu hai khung chồng nhau.
            _quarText.Bounds = new Rectangle(pad, quarY + Ui.Px(34),
                Math.Max(Ui.Px(120), _quarLink.Left - Ui.Px(12) - pad), Ui.Px(40));

            AutoScrollMinSize = new Size(0, quarY + Ui.Px(90));
        }

        private Color _protectDot = Theme.InkFaint;

        protected override void PaintPage(Graphics g)
        {
            int pad = Pad;
            int right = ClientSize.Width - pad;
            Ui.HLine(g, pad, right, _drivesLine, Theme.Line);
            Ui.HLine(g, pad, right, _protectLine, Theme.Line);
            Ui.HLine(g, pad, right, _quarLine, Theme.Line);

            using (SolidBrush b = new SolidBrush(_protectDot))
                g.FillEllipse(b, pad + Ui.Px(2), _protectRowY + Ui.Px(17), Ui.Px(8), Ui.Px(8));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _stageTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
