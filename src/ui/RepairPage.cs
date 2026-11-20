// ShieldGate 2.0 - màn hình sửa ổ: chẩn đoán, đọc thử, sao lưu, chkdsk, boot sector.
// Mọi thao tác ghi đều qua Repair.RefuseReason và phải xác nhận; boot sector chỉ đọc.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class RepairPage : PageBase
    {
        private readonly Label _title, _intro, _driveCaption, _outputTitle, _iconHint,
                             _groupCheck, _groupBackup, _groupRepair;
        private readonly SelectBox _drive;
        private readonly LinkText _refresh, _clearOutput;
        private readonly FlatButton _diagnose, _readTest, _preview, _backup, _openFolder,
                                   _chkRead, _chkFix, _boot, _defender;
        private readonly InfoCard _info;
        private readonly Panel _outputBox;
        private readonly TextBox _output;
        private readonly Notice _notice;
        private readonly ToolTip _tips;

        private readonly List<string> _roots = new List<string>();
        private bool _busy;
        private bool _loading;
        private bool _hasInfo;
        private RepairInfo _lastInfo;
        private bool _outputHasText;
        private string _previewDrive = "";
        private long _previewFiles, _previewBytes;
        private int _outputLine;

        public RepairPage()
        {
            _title = MakeHeading(S.Repair.Title);
            Controls.Add(_title);

            _refresh = MakeLink(S.Repair.Refresh, "refresh");
            _refresh.Click += delegate { RefreshDrives(); };
            Controls.Add(_refresh);

            _intro = MakeLabel(S.Repair.Intro, Theme.Body, Theme.InkMuted);
            _intro.AutoEllipsis = false;
            _intro.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(_intro);

            _driveCaption = MakeLabel(S.Repair.InfoDrive, Theme.Small, Theme.InkMuted);
            Controls.Add(_driveCaption);

            _drive = new SelectBox();
            _drive.AccessibleName = S.Repair.ChooseDrive;
            _drive.SelectedChanged += delegate
            {
                if (_loading) return;
                _hasInfo = false;
                _lastInfo = null;
                _info.SetFields(Placeholder());
                _notice.SetMessage(S.Repair.EmptyHint, false);
                UpdateButtons();
                // Đọc thông tin ngay khi đổi ổ (chỉ đọc) để thẻ không trống.
                Diagnose();
            };
            Controls.Add(_drive);

            _info = new InfoCard();
            _info.AccessibleName = S.Repair.DetailsTitle;
            Controls.Add(_info);

            // Tên chức năng hiện khi rê chuột; nhãn nhóm luôn hiện để không mất ngữ cảnh.
            _tips = new ToolTip();
            _tips.InitialDelay = 350;
            _tips.ReshowDelay = 120;
            _tips.AutoPopDelay = 12000;

            _groupCheck = MakeLabel(S.Repair.GroupCheck, Theme.SmallStrong, Theme.InkMuted);
            Controls.Add(_groupCheck);

            _iconHint = MakeLabel(S.Repair.IconHint, Theme.Small, Theme.InkFaint);
            _iconHint.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(_iconHint);

            _diagnose = MakeIconButton("search", S.Repair.Diagnose, S.Repair.DiagnoseHint,
                                       ButtonKind.Primary);
            _diagnose.Click += delegate { Diagnose(); };
            Controls.Add(_diagnose);

            _readTest = MakeIconButton("eye", S.Repair.ReadTest, S.Repair.ReadTestHint,
                                       ButtonKind.Secondary);
            _readTest.Click += delegate { DoReadTest(); };
            Controls.Add(_readTest);

            _boot = MakeIconButton("disk", S.Repair.BootSector, S.Repair.BootHint,
                                   ButtonKind.Secondary);
            _boot.Click += delegate { DoBootSector(); };
            Controls.Add(_boot);

            _groupBackup = MakeLabel(S.Repair.GroupBackup, Theme.SmallStrong, Theme.InkMuted);
            Controls.Add(_groupBackup);

            _preview = MakeIconButton("list", S.Repair.Preview, S.Repair.PreviewHint,
                                      ButtonKind.Secondary);
            _preview.Click += delegate { DoPreview(); };
            Controls.Add(_preview);

            _backup = MakeIconButton("save", S.Repair.Backup, S.Repair.BackupHint,
                                     ButtonKind.Secondary);
            _backup.Click += delegate { DoBackup(); };
            Controls.Add(_backup);

            _openFolder = MakeIconButton("folder", S.Repair.OpenFolder, S.Repair.OpenFolderHint,
                                         ButtonKind.Secondary);
            _openFolder.Click += delegate { OpenRepairFolder(); };
            Controls.Add(_openFolder);

            _groupRepair = MakeLabel(S.Repair.GroupRepair, Theme.SmallStrong, Theme.InkMuted);
            Controls.Add(_groupRepair);

            _chkRead = MakeIconButton("check", S.Repair.ChkdskRead, S.Repair.ChkdskReadHint,
                                      ButtonKind.Secondary);
            _chkRead.Click += delegate { DoChkdsk(false); };
            Controls.Add(_chkRead);

            _chkFix = MakeIconButton("wrench", S.Repair.ChkdskFix, S.Repair.ChkdskFixHint,
                                     ButtonKind.Danger);
            _chkFix.Click += delegate { DoChkdsk(true); };
            Controls.Add(_chkFix);

            _defender = MakeIconButton("shield", S.Repair.Defender, S.Repair.DefenderHint,
                                       ButtonKind.Secondary);
            _defender.Click += delegate { DoDefender(); };
            Controls.Add(_defender);

            _notice = new Notice();
            Controls.Add(_notice);

            _outputTitle = MakeLabel(S.Repair.OutputTitle, Theme.Section, Theme.Ink);
            Controls.Add(_outputTitle);

            _clearOutput = MakeLink(S.Repair.ClearOutput, "");
            _clearOutput.Click += delegate { ClearOutput(); };
            Controls.Add(_clearOutput);

            _outputBox = new Panel();
            _outputBox.BackColor = Theme.Surface;
            _outputBox.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, _outputBox.Width - 1, _outputBox.Height - 1);
            };
            Controls.Add(_outputBox);

            _output = new TextBox();
            _output.Multiline = true;
            _output.ReadOnly = true;
            _output.WordWrap = true;
            _output.ScrollBars = ScrollBars.Vertical;
            _output.BorderStyle = BorderStyle.None;
            _output.BackColor = Theme.Surface;
            _output.ForeColor = Theme.Ink;
            _output.Font = Theme.Mono;
            _output.AccessibleRole = AccessibleRole.Text;
            _output.AccessibleName = S.Repair.OutputTitle;
            _outputBox.Controls.Add(_output);

            _info.SetFields(Placeholder());
            ShowOutputPlaceholder();
            UpdateButtons();
            LayoutPage();
        }

        public override bool Busy { get { return _busy; } }

        public override void OnShown()
        {
            if (_drive.Items.Length == 0) RefreshDrives();
        }

        private void UpdateButtons()
        {
            bool enabled = !_busy && _drive.SelectedIndex >= 0;
            _diagnose.Enabled = enabled;
            _readTest.Enabled = enabled;
            _preview.Enabled = enabled;
            _backup.Enabled = enabled;
            _chkRead.Enabled = enabled;
            _chkFix.Enabled = enabled;
            _boot.Enabled = enabled;
            _defender.Enabled = enabled;
            _refresh.Enabled = !_busy && !_loading;
            _drive.Enabled = !_busy && !_loading;
            // Mở thư mục chỉ là thao tác xem, không cần chọn ổ hay chờ.
            _openFolder.Enabled = true;
            _clearOutput.Visible = _outputHasText;
        }

        // ---------------------------------------------------------- danh sách ổ

        private void RefreshDrives()
        {
            if (_loading || _busy) return;
            string keep = SelectedDrive();
            _loading = true;
            UpdateButtons();

            Thread t = new Thread(delegate()
            {
                List<string> roots = new List<string>();
                List<string> labels = new List<string>();
                try
                {
                    foreach (DriveInfo d in DriveInfo.GetDrives())
                    {
                        try
                        {
                            if (!d.IsReady) continue;
                            if (d.DriveType == DriveType.Network || d.DriveType == DriveType.CDRom ||
                                d.DriveType == DriveType.NoRootDirectory) continue;
                            string root = d.RootDirectory.FullName;
                            if (string.Equals(root, Paths.SystemDrive, StringComparison.OrdinalIgnoreCase)) continue;
                            roots.Add(root);
                            string label = root;
                            string vol = d.VolumeLabel;
                            if (vol != null && vol.Trim().Length > 0) label += "  " + vol.Trim();
                            try { label += "  (" + d.DriveFormat + ")"; }
                            catch { }
                            labels.Add(label);
                        }
                        catch { }
                    }
                }
                catch { }
                RunOnUi(delegate { FillDrives(roots, labels, keep); });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void FillDrives(List<string> roots, List<string> labels, string keep)
        {
            _loading = false;
            _roots.Clear();
            _roots.AddRange(roots);
            _drive.Items = labels.ToArray();

            int index = -1;
            for (int i = 0; i < _roots.Count && keep.Length > 0; i++)
            {
                if (!string.Equals(_roots[i], keep, StringComparison.OrdinalIgnoreCase)) continue;
                index = i;
                break;
            }
            if (index < 0 && _roots.Count > 0) index = 0;

            _loading = true;
            _drive.SelectedIndex = index;
            _loading = false;

            // Giữ thẻ thông tin khi ổ chọn không đổi; đổi ổ thì trả về ô trống.
            string now = SelectedDrive();
            if (!_hasInfo || !string.Equals(keep, now, StringComparison.OrdinalIgnoreCase))
            {
                _hasInfo = false;
                _lastInfo = null;
                _info.SetFields(Placeholder());
            }

            if (_roots.Count == 0) _notice.SetMessage(S.Repair.NoDrives, false);
            else if (!_hasInfo)
            {
                _notice.SetMessage(S.Repair.EmptyHint, false);
                // Ổ đầu tiên được chọn sẵn: đọc thông tin luôn thay vì bắt bấm thêm.
                Diagnose();
            }

            UpdateButtons();
            LayoutPage();
        }

        private string SelectedDrive()
        {
            int i = _drive.SelectedIndex;
            if (i < 0 || i >= _roots.Count) return "";
            return _roots[i];
        }

        // Trả về ổ đã chọn và bật trạng thái bận; "" khi không thể bắt đầu.
        private string Prepare(string message)
        {
            if (_busy) return "";
            string drive = SelectedDrive();
            if (drive.Length == 0)
            {
                _notice.SetMessage(S.Repair.NoDrive, false);
                return "";
            }
            _busy = true;
            UpdateButtons();
            _notice.SetMessage(message, false);
            return drive;
        }

        private void Done(string message, bool error)
        {
            _busy = false;
            UpdateButtons();
            _notice.SetMessage(message, error);
        }

        // ------------------------------------------------------------ thao tác

        private void Diagnose()
        {
            string drive = Prepare(S.Repair.Diagnosing);
            if (drive.Length == 0) return;
            Thread t = new Thread(delegate()
            {
                RepairInfo info = null;
                try { info = Repair.Inspect(drive); }
                catch (Exception e) { Append(e.Message); }
                RunOnUi(delegate
                {
                    ShowInfo(info);
                    string message = info == null
                        ? S.Repair.RunFinished
                        : (info.Problem.Length > 0 ? info.Problem : S.Repair.NoProblem);
                    Done(message, info != null && info.Problem.Length > 0);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private List<InfoField> Placeholder()
        {
            string drive = SelectedDrive();
            List<InfoField> fields = new List<InfoField>();
            fields.Add(new InfoField(S.Repair.InfoDrive, drive.Length > 0 ? drive : "—"));
            fields.Add(new InfoField(S.Repair.InfoFileSystem, "—"));
            fields.Add(new InfoField(S.Repair.InfoLabel, "—"));
            fields.Add(new InfoField(S.Repair.InfoCapacity, "—"));
            fields.Add(new InfoField(S.Repair.InfoStatus, "—", 2, Theme.InkMuted));
            fields.Add(new InfoField(S.Repair.InfoBackup, "—", 2, Theme.InkMuted));
            return fields;
        }

        private void ShowInfo(RepairInfo info)
        {
            _lastInfo = info;
            if (info == null)
            {
                _hasInfo = false;
                _info.SetFields(Placeholder());
                return;
            }

            string status = info.Ready ? S.Repair.Ready : S.Repair.NotReadyLabel;
            status += " · " + (info.ReadOnly ? S.Repair.ReadOnlyLabel : S.Repair.WritableLabel);
            status += " · " + (info.DirtyKnown
                ? (info.Dirty ? S.Repair.DirtyLabel : S.Repair.CleanLabel)
                : S.Repair.DirtyUnknown);
            if (info.SystemDrive) status += " · " + S.Repair.SystemDriveLabel;

            Color statusColor = Theme.Ink;
            if (!info.Ready || info.SystemDrive) statusColor = Theme.Danger;
            else if (info.Dirty) statusColor = Theme.Warn;

            List<InfoField> fields = new List<InfoField>();
            fields.Add(new InfoField(S.Repair.InfoDrive, info.Drive));
            fields.Add(new InfoField(S.Repair.InfoFileSystem, Dash(info.FileSystem)));
            fields.Add(new InfoField(S.Repair.InfoLabel, Dash(info.Label)));
            fields.Add(new InfoField(S.Repair.InfoCapacity,
                       S.Repair.FreeOf(info.FreeBytes, info.TotalBytes)));
            fields.Add(new InfoField(S.Repair.InfoStatus, status, 2, statusColor));
            fields.Add(new InfoField(S.Repair.InfoBackup, BackupText(info.Drive), 2, Theme.Ink));
            _info.SetFields(fields);
            _hasInfo = true;
        }

        private static string BackupText(string drive)
        {
            // Ly do cu the (khac serial, qua han, ban sao mat) huu ich hon cau chung.
            string reason;
            bool have = Repair.CheckBackup(drive, out reason);
            if (!have) return reason.Length > 0 ? reason : S.Repair.NoBackup;
            string last = Repair.LastBackup(drive);
            return last.Length == 0 ? S.Repair.NoBackup : S.Repair.LastBackup(last);
        }

        // Vẽ lại thẻ từ thông tin đã đọc, không chạy lại fsutil sau khi sao lưu.
        private void RefreshInfoCard()
        {
            if (_lastInfo != null) ShowInfo(_lastInfo);
        }

        private void DoReadTest()
        {
            string drive = Prepare(S.Repair.ReadTesting);
            if (drive.Length == 0) return;
            Append(S.Repair.ReadTesting);
            Thread t = new Thread(delegate()
            {
                ReadTestResult r = null;
                try { r = Repair.ReadTest(drive, 20, 64L * 1024 * 1024); }
                catch (Exception e) { Append(e.Message); }
                RunOnUi(delegate
                {
                    if (r != null)
                    {
                        Append(S.Repair.ReadDone(r.Files, r.Bytes, r.Errors, r.TimedOut));
                        if (r.FirstError.Length > 0) Append(r.FirstError);
                    }
                    Done(S.Repair.RunFinished, r != null && r.Errors > 0);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void DoPreview()
        {
            string drive = Prepare(S.Repair.Preview + "…");
            if (drive.Length == 0) return;
            Thread t = new Thread(delegate()
            {
                BackupPlan plan = null;
                try { plan = Repair.PreviewBackup(drive); }
                catch (Exception e) { Append(e.Message); }
                RunOnUi(delegate
                {
                    if (plan != null && plan.Ok)
                    {
                        _previewDrive = drive;
                        _previewFiles = plan.Files;
                        _previewBytes = plan.Bytes;
                        Append(S.Repair.BackupPreviewDone(plan.Files, plan.Bytes));
                        Done(S.Repair.BackupPreviewDone(plan.Files, plan.Bytes), false);
                    }
                    else
                    {
                        string detail = plan == null ? S.Repair.RunFinished : plan.Error;
                        Append(S.Repair.BackupFailed(detail));
                        Done(S.Repair.BackupFailed(detail), true);
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void DoBackup()
        {
            string drive = SelectedDrive();
            if (drive.Length == 0) { _notice.SetMessage(S.Repair.NoDrive, false); return; }
            if (!string.Equals(drive, _previewDrive, StringComparison.OrdinalIgnoreCase))
            {
                _notice.SetMessage(S.Repair.PreviewFirst, false);
                return;
            }
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.BackupTitle,
                                   S.Dlg.BackupBody(drive, _previewFiles, _previewBytes),
                                   S.Dlg.BackupOk, S.Dlg.Cancel, ButtonKind.Danger, false))
                return;

            drive = Prepare(S.Repair.BackingUp);
            if (drive.Length == 0) return;
            Thread t = new Thread(delegate()
            {
                string dest = "", output = "";
                bool ok = false;
                try { ok = Repair.Backup(drive, out dest, out output); }
                catch (Exception e) { output = e.Message; }
                string destCopy = dest, outCopy = output;
                bool okCopy = ok;
                RunOnUi(delegate
                {
                    if (okCopy)
                    {
                        Append(S.Repair.BackupDone(destCopy));
                        Done(S.Repair.BackupDone(destCopy), false);
                        // Marker vừa ghi: cập nhật dòng "Sao lưu" từ dữ liệu đã có.
                        RefreshInfoCard();
                    }
                    else
                    {
                        Append(S.Repair.BackupFailed(outCopy));
                        Done(S.Repair.BackupFailed(outCopy), true);
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void DoChkdsk(bool fix)
        {
            string drive = SelectedDrive();
            if (drive.Length == 0) { _notice.SetMessage(S.Repair.NoDrive, false); return; }

            if (fix)
            {
                string backupReason;
                if (!Repair.CheckBackup(drive, out backupReason))
                {
                    _notice.SetMessage(backupReason.Length > 0
                        ? backupReason : S.Repair.BackupRequired, true);
                    return;
                }
                if (!Native.IsAdmin())
                {
                    _notice.SetMessage(S.Cli.AdminRequired, true);
                    return;
                }
                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ChkdskFixTitle,
                                       S.Dlg.ChkdskFixBody(drive), S.Dlg.ChkdskFixOk,
                                       S.Dlg.Cancel, ButtonKind.Danger, false))
                    return;
                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ChkdskFixAgainTitle,
                                       S.Dlg.ChkdskFixAgainBody(drive), S.Dlg.ChkdskFixAgainOk,
                                       S.Dlg.Cancel, ButtonKind.Danger, false))
                    return;
            }
            else
            {
                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ChkdskReadTitle,
                                       S.Dlg.ChkdskReadBody(drive), S.Dlg.ChkdskReadOk,
                                       S.Dlg.Cancel, ButtonKind.Secondary, true))
                    return;
            }

            drive = Prepare(fix ? S.Repair.ChkdskFixRunning : S.Repair.ChkdskReadRunning);
            if (drive.Length == 0) return;
            Thread t = new Thread(delegate()
            {
                int code = -1;
                try { code = Repair.RunChkdsk(drive, fix, Append); }
                catch (Exception e) { Append(e.Message); }
                int codeCopy = code;
                RunOnUi(delegate
                {
                    Append(S.Repair.ChkdskDone(codeCopy));
                    Done(S.Repair.ChkdskDone(codeCopy), codeCopy != 0);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void DoBootSector()
        {
            string drive = Prepare(S.Repair.BootReading);
            if (drive.Length == 0) return;
            Thread t = new Thread(delegate()
            {
                BootSectorInfo info = null;
                try { info = Repair.ReadBootSector(drive); }
                catch (Exception e) { Append(e.Message); }
                RunOnUi(delegate
                {
                    if (info != null && info.Read)
                    {
                        Append(S.Repair.BootReport(info.FileSystem, info.Signature55AA));
                        Done(S.Repair.BootReport(info.FileSystem, info.Signature55AA), false);
                    }
                    else
                    {
                        string detail = info == null ? "?" : info.Error;
                        Append(S.Repair.BootReadFailed(detail));
                        Done(S.Repair.BootReadFailed(detail), true);
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void DoDefender()
        {
            if (!Defender.Available)
            {
                _notice.SetMessage(S.Repair.DefenderMissing, true);
                return;
            }
            string drive = SelectedDrive();
            if (drive.Length == 0) { _notice.SetMessage(S.Repair.NoDrive, false); return; }
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.DefenderTitle, S.Dlg.DefenderBody(drive),
                                   S.Dlg.DefenderOk, S.Dlg.Cancel, ButtonKind.Secondary, false))
                return;

            drive = Prepare(S.Repair.DefenderRunning);
            if (drive.Length == 0) return;
            Thread t = new Thread(delegate()
            {
                DefenderResult r = null;
                try { r = Defender.Scan(drive, 30 * 60 * 1000); }
                catch (Exception e) { Append(e.Message); }
                RunOnUi(delegate
                {
                    if (r == null || !r.Ran)
                    {
                        string detail = r == null ? "?" : r.Error;
                        Append(detail);
                        Done(detail, true);
                        return;
                    }
                    Append(r.Clean ? S.Repair.DefenderClean : S.Repair.DefenderFound);
                    Done(r.Clean ? S.Repair.DefenderClean : S.Repair.DefenderFound, !r.Clean);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void OpenRepairFolder()
        {
            try
            {
                Paths.EnsureDirs();
                Process.Start(Native.ShellExe("explorer.exe"), "\"" + Paths.RepairDir + "\"");
            }
            catch (Exception e)
            {
                _notice.SetMessage(e.Message, true);
            }
        }

        // ------------------------------------------------------------- kết quả

        private void Append(string text)
        {
            if (text == null || text.Length == 0) return;
            RunOnUi(delegate { SafeAppend(text); });
        }

        private void SafeAppend(string text)
        {
            if (IsDisposed) return;
            if (!_outputHasText)
            {
                _output.Text = "";
                _outputHasText = true;
                _output.ForeColor = Theme.Ink;
                _clearOutput.Visible = true;
            }
            if (_output.TextLength > 60000)
                _output.Text = _output.Text.Substring(_output.TextLength - 30000);
            _output.AppendText((_output.TextLength > 0 ? "\r\n" : "") + text);
            _output.SelectionStart = _output.TextLength;
            _output.ScrollToCaret();
        }

        private void ShowOutputPlaceholder()
        {
            _output.Text = S.Repair.OutputEmpty;
            _output.ForeColor = Theme.InkFaint;
            _outputHasText = false;
            _clearOutput.Visible = false;
        }

        private void ClearOutput()
        {
            _output.Clear();
            ShowOutputPlaceholder();
        }

        private void RunOnUi(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch { }
        }

        private static string Dash(string value)
        {
            return string.IsNullOrEmpty(value) ? "—" : value;
        }

        // Nút chỉ có icon: tên chức năng nằm trong tooltip và AccessibleName.
        private FlatButton MakeIconButton(string icon, string name, string hint, ButtonKind kind)
        {
            FlatButton b = new FlatButton();
            b.Kind = kind;
            b.IconName = icon;
            b.Text = "";
            b.Width = Ui.Px(44);
            b.Height = Ui.Px(40);
            b.AccessibleName = name;
            b.AccessibleDescription = hint;
            _tips.SetToolTip(b, S.Repair.HoverHint(name, hint));
            return b;
        }

        // --------------------------------------------------------------- layout

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

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 280, 34);
            int refreshW = Ui.Measure(S.Repair.Refresh, Theme.Small).Width + Ui.Px(24);
            _refresh.Bounds = new Rectangle(right - refreshW, Ui.Px(28), refreshW, Ui.Px(22));
            _intro.Bounds = new Rectangle(pad, Ui.Px(58), width, Ui.Px(34));

            int driveY = Ui.Px(100);
            _driveCaption.Bounds = new Rectangle(pad, driveY + Ui.Px(4), Ui.Px(34), Ui.Px(22));
            int driveX = pad + Ui.Px(40);
            _drive.Bounds = new Rectangle(driveX, driveY,
                Math.Min(Ui.Px(340), Math.Max(Ui.Px(150), right - driveX)), Ui.Px(30));

            _info.Bounds = new Rectangle(pad, driveY + Ui.Px(38), width, _info.Height);

            int y = _info.Bottom + Ui.Px(10);
            _notice.Bounds = new Rectangle(pad, y, width, Ui.Px(38));
            y += Ui.Px(48);

            // Ba nhóm thao tác trên một hàng icon; mỗi nút có tooltip tên + giải thích.
            int capH = Ui.Px(16);
            int bh = Ui.Px(40);
            int bw = Ui.Px(44);
            int bgap = Ui.Px(6);
            int groupGap = Ui.Px(26);
            int groupW = bw * 3 + bgap * 2;
            int btnY = y + capH + Ui.Px(4);

            int g1 = pad;
            int g2 = g1 + groupW + groupGap;
            int g3 = g2 + groupW + groupGap;

            _groupCheck.Bounds = new Rectangle(g1, y, groupW, capH);
            _diagnose.Bounds = new Rectangle(g1, btnY, bw, bh);
            _readTest.Bounds = new Rectangle(g1 + bw + bgap, btnY, bw, bh);
            _boot.Bounds = new Rectangle(g1 + (bw + bgap) * 2, btnY, bw, bh);

            _groupBackup.Bounds = new Rectangle(g2, y, groupW, capH);
            _preview.Bounds = new Rectangle(g2, btnY, bw, bh);
            _backup.Bounds = new Rectangle(g2 + bw + bgap, btnY, bw, bh);
            _openFolder.Bounds = new Rectangle(g2 + (bw + bgap) * 2, btnY, bw, bh);

            _groupRepair.Bounds = new Rectangle(g3, y, groupW, capH);
            _chkRead.Bounds = new Rectangle(g3, btnY, bw, bh);
            _chkFix.Bounds = new Rectangle(g3 + bw + bgap, btnY, bw, bh);
            _defender.Bounds = new Rectangle(g3 + (bw + bgap) * 2, btnY, bw, bh);

            int hintX = g3 + groupW + Ui.Px(16);
            _iconHint.Bounds = new Rectangle(hintX, y, Math.Max(0, right - hintX), capH);

            y = btnY + bh + Ui.Px(14);
            _outputTitle.Bounds = new Rectangle(pad, y, width, Ui.Px(22));
            int clearW = Ui.Measure(S.Repair.ClearOutput, Theme.Small).Width + Ui.Px(4);
            _clearOutput.Bounds = new Rectangle(right - clearW, y, clearW, Ui.Px(22));

            _outputLine = y + Ui.Px(24);
            int top = _outputLine + Ui.Px(8);
            int h = Math.Max(Ui.Px(140), ClientSize.Height - top - Ui.Px(34));
            _outputBox.Bounds = new Rectangle(pad, top, width, h);
            _output.Bounds = new Rectangle(Ui.Px(12), Ui.Px(10),
                Math.Max(Ui.Px(60), _outputBox.Width - Ui.Px(24)),
                Math.Max(Ui.Px(80), _outputBox.Height - Ui.Px(20)));

            AutoScrollMinSize = new Size(0, top + h + Ui.Px(26));
        }

        protected override void PaintPage(Graphics g)
        {
            int pad = Pad;
            int right = ClientSize.Width - pad;
            Ui.HLine(g, pad, right, _outputLine, Theme.Line);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
