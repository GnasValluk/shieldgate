// ShieldGate - màn hình Xử lý USB: kiểm tra trước, người dùng chọn, rồi mới cách ly.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class UsbPage : PageBase
    {
        private enum UsbState { NoDrive, Idle, Scanning, Results, Clean, Done }

        private readonly SelectBox _drive;
        private readonly List<string> _driveRoots = new List<string>();
        private readonly LinkText _refresh, _vaccine, _unhide, _scanFolder, _rename, _selectAll, _selectNone, _flagToggle, _undo, _log;
        private readonly FlatButton _scan, _apply, _cancel;
        private readonly Notice _notice;
        private readonly Spinner _spinner;
        private readonly Glyph _stateIcon;
        private readonly Label _title, _statusTitle, _statusSub, _resultsTitle, _resultsNote;
        private readonly UsbListControl _list;
        private readonly System.Windows.Forms.Timer _progressTimer;
        private readonly ToolTip _tips;

        private UsbScanResult _result;
        private string _batch = "";
        // Lần dọn này do engine SYSTEM thực hiện qua pipe: hoàn tác cục bộ không
        // có quyền đọc vùng cách ly, nên phải báo cần quản trị.
        private bool _batchRemote;
        private string _scanDrive = "";
        private int _doneCount, _failedCount, _unhidden, _cleanFiles;
        private bool _scanning, _applying, _unhiding, _loading;
        // Quét một thư mục người dùng chọn thay vì cả ổ. _scanDrive giữ gốc quét
        // hiện tại (ổ hoặc thư mục) để quét lại sau khi cách ly.
        private bool _isFolder;
        private volatile bool _cancelFlag;
        private volatile int _progressFiles;
        private volatile string _progressPath = "";
        private UsbState _state = UsbState.NoDrive;

        public UsbPage()
        {
            AutoScroll = false;

            _title = MakeHeading(S.Usb.Title);
            Controls.Add(_title);

            _drive = new SelectBox();
            _drive.AccessibleName = S.Usb.Choose;
            _drive.SelectedChanged += delegate
            {
                if (_loading || _scanning || _applying) return;
                _isFolder = false;
                _scanDrive = "";
                _result = null;
                _list.Clear();
                _notice.ClearMessage();
                ShowState(SelectedDrive() == null ? UsbState.NoDrive : UsbState.Idle);
            };
            Controls.Add(_drive);

            _refresh = MakeLink(S.Usb.Refresh, "refresh");
            _refresh.Click += delegate { RefreshDrives(); };
            Controls.Add(_refresh);

            _scan = MakeButton(S.Usb.Scan, ButtonKind.Primary);
            _scan.Click += delegate { StartScan(); };
            Controls.Add(_scan);

            _scanFolder = MakeLink(S.Usb.ScanFolder, "");
            _scanFolder.Click += delegate { ChooseFolder(); };
            Controls.Add(_scanFolder);

            _vaccine = MakeLink(S.Usb.Vaccine, "shield");
            _vaccine.IconSize = 14;
            _vaccine.Click += delegate { DoVaccinate(); };
            Controls.Add(_vaccine);

            _unhide = MakeLink(S.Usb.Unhide, "");
            _unhide.Click += delegate { DoUnhide(); };
            Controls.Add(_unhide);

            _rename = MakeLink(S.Usb.RenameBase64(0), "");
            _rename.Click += delegate { DoRenameBase64(); };
            Controls.Add(_rename);

            _tips = new ToolTip();
            _tips.SetToolTip(_vaccine, S.Usb.VaccineHint);
            _tips.SetToolTip(_unhide, S.Usb.UnhideHint);
            _tips.SetToolTip(_refresh, S.Usb.Refresh);
            _tips.SetToolTip(_scanFolder, S.Usb.ChooseFolderHint);
            _tips.SetToolTip(_rename, S.Usb.RenameBase64Hint);

            _notice = new Notice();
            Controls.Add(_notice);

            _spinner = new Spinner();
            Controls.Add(_spinner);

            _stateIcon = new Glyph();
            _stateIcon.IconName = "check";
            _stateIcon.IconColor = Theme.Safe;
            Controls.Add(_stateIcon);

            _statusTitle = MakeLabel("", Theme.Hero, Theme.Ink);
            Controls.Add(_statusTitle);

            _statusSub = MakeLabel("", Theme.Body, Theme.InkMuted);
            _statusSub.AutoEllipsis = false;
            _statusSub.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(_statusSub);

            _cancel = MakeButton(S.Overview.Cancel, ButtonKind.Secondary);
            _cancel.Click += delegate { _cancelFlag = true; };
            Controls.Add(_cancel);

            _resultsTitle = MakeLabel("", Theme.Section, Theme.Ink);
            Controls.Add(_resultsTitle);

            _resultsNote = MakeLabel(S.Usb.ResultsNote, Theme.Small, Theme.InkMuted);
            _resultsNote.AutoEllipsis = true;
            Controls.Add(_resultsNote);

            _list = new UsbListControl();
            _list.SelectionChanged += delegate { UpdateApplyButton(); };
            Controls.Add(_list);

            _selectAll = MakeLink(S.Usb.SelectAll, "");
            _selectAll.Click += delegate { _list.SetAll(true); UpdateApplyButton(); };
            Controls.Add(_selectAll);

            _selectNone = MakeLink(S.Usb.SelectNone, "");
            _selectNone.Click += delegate { _list.SetAll(false); UpdateApplyButton(); };
            Controls.Add(_selectNone);

            _flagToggle = MakeLink(S.Usb.ShowFlagged(0), "eye");
            _flagToggle.IconSize = 13;
            _flagToggle.Click += delegate
            {
                _list.ShowFlagged = !_list.ShowFlagged;
                UpdateFlagToggle();
                UpdateApplyButton();
                LayoutPage();
            };
            Controls.Add(_flagToggle);

            _apply = MakeButton(S.Usb.Apply(0), ButtonKind.Primary);
            _apply.Click += delegate { DoApply(); };
            Controls.Add(_apply);

            _undo = MakeLink(S.Usb.Undo, "undo");
            _undo.IconSize = 13;
            _undo.Click += delegate { DoUndo(); };
            Controls.Add(_undo);

            _log = MakeLink(S.Usb.OpenLog, "");
            _log.Click += delegate { Go(PageId.Log); };
            Controls.Add(_log);

            _progressTimer = new System.Windows.Forms.Timer();
            _progressTimer.Interval = 120;
            _progressTimer.Tick += delegate
            {
                if (!_scanning) { _progressTimer.Stop(); return; }
                _statusSub.Text = ProgressText();
            };

            ShowState(UsbState.NoDrive);
            LayoutPage();
        }

        public override bool Busy { get { return _scanning || _applying || _unhiding; } }

        public override void CancelWork()
        {
            _cancelFlag = true;
        }

        public override void OnShown()
        {
            if (_scanning || _applying || _unhiding) return;
            RefreshDrives();
        }

        public void FocusDrive(string drive)
        {
            _isFolder = false;
            _scanDrive = "";
            RefreshDrives();
            SelectDrive(drive);
        }

        // ------------------------------------------------------------ danh sách ổ

        private void RefreshDrives()
        {
            if (_scanning || _applying || _unhiding) return;

            string keep = SelectedDrive();
            List<string> drives;
            try { drives = Native.RemovableDrives(); }
            catch { drives = new List<string>(); }

            _loading = true;
            _driveRoots.Clear();
            List<string> labels = new List<string>();
            foreach (string d in drives)
            {
                labels.Add(UsbScan.DriveLabel(d));
                _driveRoots.Add(d);
            }
            _drive.Items = labels.ToArray();

            int index = -1;
            if (keep != null)
            {
                for (int i = 0; i < _driveRoots.Count; i++)
                    if (string.Equals(_driveRoots[i], keep, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            }
            if (index < 0 && _driveRoots.Count > 0) index = 0;
            if (index >= 0) _drive.SelectedIndex = index;
            _loading = false;

            if (drives.Count == 0)
            {
                if (_isFolder) { LayoutPage(); return; }
                _result = null;
                _list.Clear();
                ShowState(UsbState.NoDrive);
                LayoutPage();
                return;
            }

            if (_state == UsbState.NoDrive) ShowState(UsbState.Idle);
            LayoutPage();
            if (_scan.Visible && _scan.Enabled) _scan.Focus();
        }

        private string SelectedDrive()
        {
            int i = _drive.SelectedIndex;
            if (i < 0 || i >= _driveRoots.Count) return null;
            string d = _driveRoots[i];
            if (string.IsNullOrEmpty(d) || d.Length < 2) return null;
            return d.EndsWith("\\") ? d : d + "\\";
        }

        // Gốc đang thao tác: thư mục người dùng chọn nếu có, ngược lại là ổ.
        private string CurrentRoot()
        {
            if (_isFolder && _scanDrive.Length > 0) return _scanDrive;
            return SelectedDrive();
        }

        // Chỉ gốc ổ mới đi qua pipe: thao tác trên thư mục vẫn chạy cục bộ như 2.3.
        // GUI quyền thường cần engine SYSTEM để tạm mở khoá ghi và ghi vùng cách ly.
        private static bool UsePipe(string root)
        {
            try
            {
                return Settings.PipeEnabled && !Native.IsAdmin() && UsbScan.IsDriveRoot(root);
            }
            catch { return false; }
        }

        private void SelectDrive(string drive)
        {
            if (string.IsNullOrEmpty(drive)) return;
            for (int i = 0; i < _driveRoots.Count; i++)
            {
                if (!string.Equals(_driveRoots[i], drive, StringComparison.OrdinalIgnoreCase)) continue;
                _loading = true;
                _drive.SelectedIndex = i;
                _loading = false;
                _result = null;
                _list.Clear();
                ShowState(UsbState.Idle);
                return;
            }
        }

        // --------------------------------------------------------------- trạng thái

        private void ShowState(UsbState state)
        {
            _state = state;
            bool hasDrive = CurrentRoot() != null;

            _spinner.Visible = state == UsbState.Scanning;
            _spinner.Run(state == UsbState.Scanning);
            _stateIcon.Visible = state == UsbState.Idle || state == UsbState.Clean || state == UsbState.Done;
            _stateIcon.IconName = state == UsbState.Idle ? "usb" : "check";
            _stateIcon.IconColor = state == UsbState.Idle ? Theme.InkMuted : Theme.Safe;

            _statusTitle.Visible = state != UsbState.Results;
            _statusSub.Visible = state != UsbState.Results;

            _cancel.Visible = state == UsbState.Scanning;
            _scan.Visible = state != UsbState.NoDrive && state != UsbState.Scanning;
            _scan.Enabled = hasDrive && !_applying && !_unhiding;
            _scan.Text = _isFolder ? S.Usb.ScanFolderButton : S.Usb.Scan;
            _scan.AccessibleName = _scan.Text;
            _scanFolder.Visible = state != UsbState.Scanning;
            _scanFolder.Enabled = !_applying && !_unhiding;
            // Tiêm autorun.inf chỉ có nghĩa ở gốc ổ, không áp cho thư mục con.
            _vaccine.Visible = !_isFolder && hasDrive && state != UsbState.NoDrive
                               && state != UsbState.Scanning;
            _vaccine.Enabled = !_applying && !_unhiding && !ReadOnlyResult();
            bool canUnhide = hasDrive && !ReadOnlyResult() &&
                (state == UsbState.Idle || state == UsbState.Clean
                 || (state == UsbState.Results && _isFolder));
            _unhide.Visible = canUnhide;
            _unhide.Enabled = canUnhide && !_unhiding;
            bool canRename = _result != null && _result.Base64Names > 0 &&
                             !ReadOnlyResult() && !_applying && !_unhiding;
            _rename.Visible = canRename;
            _rename.Enabled = canRename;
            _rename.Text = S.Usb.RenameBase64(_result == null ? 0 : _result.Base64Names);
            _rename.AccessibleName = _rename.Text;
            _drive.Enabled = !_scanning && !_applying && !_unhiding;
            _refresh.Enabled = !_scanning && !_applying && !_unhiding;

            bool results = state == UsbState.Results;
            _resultsTitle.Visible = results;
            _resultsNote.Visible = results;
            _list.Visible = results;
            _selectAll.Visible = results;
            _selectNone.Visible = results;
            _apply.Visible = results;
            UpdateFlagToggle();

            _undo.Visible = state == UsbState.Done;
            _log.Visible = state == UsbState.Done;

            switch (state)
            {
                case UsbState.NoDrive:
                    _statusTitle.Font = Theme.Hero;
                    _statusTitle.ForeColor = Theme.Ink;
                    _statusTitle.Text = S.Usb.NoDriveTitle;
                    _statusSub.Text = S.Usb.NoDriveHint;
                    _statusSub.ForeColor = Theme.InkMuted;
                    break;
                case UsbState.Idle:
                    _statusTitle.Font = Theme.Hero;
                    _statusTitle.ForeColor = Theme.Ink;
                    _statusTitle.Text = S.Usb.IdleTitle;
                    _statusSub.Text = S.Usb.IdleHint;
                    _statusSub.ForeColor = Theme.InkMuted;
                    break;
                case UsbState.Scanning:
                    _statusTitle.Font = Theme.Hero;
                    _statusTitle.ForeColor = Theme.Ink;
                    _statusTitle.Text = S.Usb.Scanning(CurrentRoot());
                    _statusSub.ForeColor = Theme.InkMuted;
                    _statusSub.Text = _progressFiles > 0 ? ProgressText() : S.Usb.Starting;
                    break;
                case UsbState.Results:
                    _resultsTitle.Text = S.Usb.ResultsTitle(_result == null ? 0 : _result.Items.Count);
                    _resultsNote.Text = _result != null && _result.KeptFiles > 0
                        ? S.Usb.ResultsNote + " " + S.Usb.FakeFolderKeptWarning(_result.KeptFiles)
                        : S.Usb.ResultsNote;
                    break;
                case UsbState.Clean:
                    _statusTitle.Font = Theme.Hero;
                    _statusTitle.ForeColor = Theme.Ink;
                    _statusTitle.Text = S.Usb.CleanTitle;
                    _statusSub.ForeColor = Theme.InkMuted;
                    _statusSub.Text = S.Usb.CleanBody(_cleanFiles);
                    break;
                case UsbState.Done:
                    _statusTitle.Font = Theme.Hero;
                    _statusTitle.ForeColor = Theme.Ink;
                    _statusTitle.Text = S.Usb.DoneTitle(_doneCount);
                    _statusSub.ForeColor = Theme.InkMuted;
                    _statusSub.Text = _unhidden > 0
                        ? S.Usb.DoneBody(_unhidden)
                        : S.Usb.DoneBodyNoUnhide;
                    break;
            }

            UpdateApplyButton();
            LayoutPage();
        }

        private string ProgressText()
        {
            string path = _progressPath;
            if (path != null && path.Length > 0 && _scanDrive.Length > 0 &&
                path.StartsWith(_scanDrive, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(_scanDrive.Length).TrimStart('\\');
            if (string.IsNullOrEmpty(path)) return S.Usb.ScanningCount(_progressFiles);
            return S.Usb.ScanningSub(_progressFiles, path);
        }

        private void UpdateApplyButton()
        {
            int n = _list.CheckedCount;
            _apply.Text = S.Usb.Apply(n);
            _apply.Enabled = n > 0 && !_applying && !ReadOnlyResult();
        }

        // Muc chi gan co (macro, DLL nghi van) mac dinh an; chip chi hien khi
        // co it nhat mot muc de nguoi dung chu dong mo ra xem.
        private void UpdateFlagToggle()
        {
            int n = _list.FlaggedCount;
            _flagToggle.Visible = _state == UsbState.Results && n > 0;
            _flagToggle.Text = _list.ShowFlagged ? S.Usb.HideFlagged : S.Usb.ShowFlagged(n);
            _flagToggle.AccessibleName = _flagToggle.Text;
        }

        // Ổ chỉ đọc: kiểm tra được nhưng không sửa được, nên tắt các thao tác ghi.
        private bool ReadOnlyResult()
        {
            return _result != null && _result.ReadOnly;
        }

        // ---------------------------------------------------------------- kiểm tra

        private void StartScan()
        {
            string drive = CurrentRoot();
            if (drive == null || _scanning || _applying || _unhiding) return;
            if (!Directory.Exists(drive))
            {
                _notice.SetMessage(S.Err.Unplugged(drive), true);
                if (!_isFolder) RefreshDrives();
                return;
            }

            _scanning = true;
            _cancelFlag = false;
            _progressFiles = 0;
            _progressPath = "";
            _scanDrive = drive;
            _result = null;
            _notice.ClearMessage();
            _list.Clear();
            ShowState(UsbState.Scanning);
            _progressTimer.Start();

            Thread t = new Thread(delegate()
            {
                UsbScanResult result = null;
                try
                {
                    result = UsbScan.Detect(drive,
                        delegate(int files, string path)
                        {
                            _progressFiles = files;
                            if (path != null) _progressPath = path;
                        },
                        delegate { return _cancelFlag; });
                }
                catch (Exception e)
                {
                    Log.Write(S.Log.UsbScanError(drive, e.Message), "ERROR");
                }
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { FinishScan(drive, result); });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void FinishScan(string drive, UsbScanResult result)
        {
            _progressTimer.Stop();
            bool cancelled = _cancelFlag;
            _scanning = false;
            _cancelFlag = false;

            if (cancelled)
            {
                _notice.SetMessage(S.Usb.ScanCancelled, false);
                ShowState(CurrentRoot() == null ? UsbState.NoDrive : UsbState.Idle);
                return;
            }
            if (result == null)
            {
                _notice.SetMessage(S.Err.Unknown, true);
                ShowState(UsbState.Idle);
                return;
            }
            if (!Directory.Exists(drive))
            {
                _notice.SetMessage(S.Err.Unplugged(drive), true);
                if (_isFolder) { _isFolder = false; _scanDrive = ""; }
                ShowState(UsbState.Idle);
                return;
            }

            _result = result;
            if (result.IsDriveRoot)
                Settings.SetDriveScan(drive, result.Files, result.Items.Count);
            Log.Write(S.Log.UsbScanDone(drive, result.Files, result.Items.Count, result.Seconds));

            List<string> warnings = new List<string>();
            if (result.ReadOnly) warnings.Add(S.Usb.ReadOnlyWarning(drive));
            if (result.Truncated) warnings.Add(S.Usb.Truncated(result.Files));
            if (result.KeptFiles > 0) warnings.Add(S.Usb.FakeFolderKeptWarning(result.KeptFiles));
            int rootExe = 0;
            foreach (UsbItem item in result.Items)
                if (item.Kind == UsbKind.RootExe) rootExe++;
            if (rootExe > 0) warnings.Add(S.Usb.RootExeWarning(rootExe));
            if (warnings.Count > 0) _notice.SetMessage(string.Join(" ", warnings.ToArray()), true);

            if (result.Items.Count == 0)
            {
                _cleanFiles = result.Files;
                ShowState(UsbState.Clean);
                return;
            }

            _list.SetItems(result.Items);
            ShowState(UsbState.Results);
            UpdateApplyButton();
        }

        // ---------------------------------------------------------------- cách ly

        // Tạm mở khoá ghi của ShieldGate để thao tác lên USB. Trả true khi đã mở;
        // người gọi phải gọi ResumeWriteProtect trong finally.
        private static bool PauseWriteProtect()
        {
            try { return Policy.BeginCleanPause(); }
            catch { return false; }
        }

        private static void ResumeWriteProtect(bool paused)
        {
            if (!paused) return;
            try { Policy.EndCleanPause(); }
            catch { }
        }

        private void DoApply()
        {
            if (_result == null || _applying || _unhiding) return;
            List<UsbItem> selected = _list.CheckedItems();
            if (selected.Count == 0) return;

            string drive = _result.Root;
            if (string.IsNullOrEmpty(drive)) return;
            // Gốc ổ + GUI quyền thường + pipe bật: engine SYSTEM làm thay, vì nó
            // mới tạm mở được khoá ghi và ghi được vùng cách ly.
            bool viaPipe = UsePipe(drive);
            // Kiểm tra quyền ghi trước khi hỏi: ổ chỉ đọc thật thì không có gì để
            // xác nhận. Khoá ghi do ShieldGate bật thì được tạm mở sau khi xác nhận.
            if (_result.ReadOnly)
            {
                _notice.SetMessage(S.Err.DriveReadOnly(drive), true);
                return;
            }
            if (!viaPipe && !Policy.CanPauseWriteProtect && !Native.CanWrite(drive))
            {
                _notice.SetMessage(S.Err.DriveReadOnly(drive), true);
                return;
            }

            List<string> names = new List<string>();
            for (int i = 0; i < selected.Count && i < 6; i++) names.Add(selected[i].Rel);
            string body = string.Join("\n", names.ToArray());
            if (selected.Count > names.Count) body += "\n…";

            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.QuarantineTitle(selected.Count),
                                   S.Dlg.QuarantineBody(body), S.Dlg.QuarantineOk(selected.Count),
                                   S.Dlg.Cancel, ButtonKind.Primary, true))
                return;

            _applying = true;
            _batchRemote = viaPipe;
            _notice.ClearMessage();
            ShowState(UsbState.Results);
            _apply.Enabled = false;
            _cancelFlag = false;

            if (viaPipe)
            {
                Thread tp = new Thread(delegate() { PipeApply(selected, drive); });
                tp.IsBackground = true;
                tp.Start();
                return;
            }

            UsbScanResult scan = _result;
            Thread t = new Thread(delegate()
            {
                int failed = 0, stopped = 0, done = 0, unhidden = 0;
                string batch = "";
                try
                {
                    // Apply/Unhide tự tạm mở khoá ghi và bật lại sau khi xong.
                    done = UsbScan.Apply(scan, selected, out failed, out batch, out stopped, null);
                    if (done > 0) unhidden = UsbScan.Unhide(drive, null);
                }
                catch (Exception e)
                {
                    Log.Write(S.Log.QuarantineError(e.Message), "ERROR");
                    failed = selected.Count;
                }
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        FinishApply(drive, done, failed, batch, unhidden);
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Dọn gốc ổ qua named pipe khi GUI không elevated. Engine quét lại, nên
        // chỉ gửi chỉ số mục đã chọn; tên tương đối dùng để khớp với kết quả quét
        // của GUI, không gửi đường dẫn.
        private void PipeApply(List<UsbItem> selected, string drive)
        {
            int failed = 0, done = 0, unhidden = 0;
            string batch = "", problem = "", warning = "";
            bool pipeUnavailable = false;
            string letter = drive.Length > 0 ? drive.Substring(0, 1).ToUpperInvariant() : "";
            try
            {
                using (PipeSession session = new PipeSession(PipeProtocol.Name))
                {
                    string error;
                    if (!session.Connect(5000, out error))
                    {
                        problem = error;
                        pipeUnavailable = true;
                    }
                    else
                    {
                        Dictionary<string, object> planArgs = new Dictionary<string, object>();
                        planArgs["drive"] = letter;
                        string planText = session.Send("clean.plan", planArgs, 120000, out error);
                        Dictionary<string, object> plan = PipeResult(planText);
                        if (plan == null)
                        {
                            problem = PipeErrorText(planText, error);
                            if (planText == null) pipeUnavailable = true;
                        }
                        else
                        {
                            List<object> ids = MapIds(
                                Json.AsArray(Json.Get(plan, "items")), selected);
                            if (ids.Count == 0)
                            {
                                // Ổ đã đổi giữa lúc quét và lúc bấm: không gửi
                                // apply rỗng để tránh báo "không có gì thay đổi".
                                problem = S.Usb.PipeItemsGone;
                            }
                            else
                            {
                                if (ids.Count < selected.Count)
                                    warning = S.Usb.PipeItemsPartial(ids.Count);
                                Dictionary<string, object> applyArgs = new Dictionary<string, object>();
                                applyArgs["plan"] = Json.Str(plan, "plan", "");
                                applyArgs["ids"] = ids;
                                applyArgs["confirm"] = true;
                                string applyText = session.Send("clean.apply", applyArgs,
                                                                600000, out error);
                                Dictionary<string, object> applied = PipeResult(applyText);
                                if (applied == null)
                                {
                                    problem = PipeErrorText(applyText, error);
                                    if (applyText == null) pipeUnavailable = true;
                                }
                                else
                                {
                                    done = Json.Int(applied, "done", 0);
                                    failed = Json.Int(applied, "failed", 0);
                                    batch = Json.Str(applied, "batch", "");
                                }
                            }
                        }

                        // Cùng hành vi với đường cục bộ: sau khi cách ly thì gỡ
                        // thuộc tính ẩn mà virus đặt (chỉ Hidden + System).
                        if (problem.Length == 0 && done > 0)
                        {
                            Dictionary<string, object> uhArgs = new Dictionary<string, object>();
                            uhArgs["drive"] = letter;
                            string uhPlanText = session.Send("unhide.plan", uhArgs, 120000, out error);
                            Dictionary<string, object> uhPlan = PipeResult(uhPlanText);
                            if (uhPlan != null)
                            {
                                Dictionary<string, object> uhApplyArgs = new Dictionary<string, object>();
                                uhApplyArgs["plan"] = Json.Str(uhPlan, "plan", "");
                                uhApplyArgs["confirm"] = true;
                                string uhApplyText = session.Send("unhide.apply", uhApplyArgs,
                                                                  600000, out error);
                                Dictionary<string, object> uhApplied = PipeResult(uhApplyText);
                                if (uhApplied != null)
                                    unhidden = Json.Int(uhApplied, "done", 0);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                problem = e.Message;
                pipeUnavailable = true;
            }

            // Dọn xong mà chỉ lỗi bước gỡ ẩn thì vẫn giữ kết quả dọn.
            if (problem.Length > 0)
            {
                if (done > 0) Log.Write(S.Log.PipeError(problem), "WARN");
                else
                {
                    failed = selected.Count;
                    if (pipeUnavailable) problem = S.Err.PipeUnavailable(problem);
                }
            }

            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    FinishApply(drive, done, failed, batch, unhidden);
                    if (problem.Length > 0) _notice.SetMessage(problem, true);
                    else if (warning.Length > 0 && failed == 0) _notice.SetMessage(warning, true);
                });
            }
            catch { }
        }

        private static Dictionary<string, object> PipeResult(string text)
        {
            try
            {
                Dictionary<string, object> root = Json.AsObject(Json.Parse(text ?? "null"));
                if (root == null || !Json.Bool(root, "ok", false)) return null;
                return Json.AsObject(Json.Get(root, "result"));
            }
            catch { return null; }
        }

        private static string PipeErrorText(string text, string fallback)
        {
            try
            {
                Dictionary<string, object> root = Json.AsObject(Json.Parse(text ?? "null"));
                string code = Json.Str(root, "error", "");
                string message = Json.Str(root, "message", "");
                if (code.Length == 0)
                    return string.IsNullOrEmpty(fallback) ? "no response" : fallback;
                return message.Length > 0 ? code + ": " + message : code;
            }
            catch
            {
                return string.IsNullOrEmpty(fallback) ? "bad response" : fallback;
            }
        }

        private static List<object> MapIds(List<object> items, List<UsbItem> selected)
        {
            List<object> ids = new List<object>();
            if (items == null) return ids;
            for (int s = 0; s < selected.Count; s++)
            {
                string rel = selected[s].Rel ?? "";
                for (int i = 0; i < items.Count; i++)
                {
                    Dictionary<string, object> item = Json.AsObject(items[i]);
                    if (item == null) continue;
                    if (!string.Equals(Json.Str(item, "rel", ""), rel,
                                       StringComparison.OrdinalIgnoreCase)) continue;
                    int id = Json.Int(item, "id", 0);
                    if (id > 0 && !ids.Contains(id)) ids.Add(id);
                    break;
                }
            }
            return ids;
        }

        private void FinishApply(string drive, int done, int failed, string batch, int unhidden)
        {
            _applying = false;
            _doneCount = done;
            _failedCount = failed;
            _unhidden = unhidden;
            _batch = batch;
            if (_result != null && _result.IsDriveRoot)
                Settings.SetDriveScan(drive, _result.Files, 0);

            if (done > 0)
            {
                _list.Clear();
                ShowState(UsbState.Done);
                if (failed > 0) _notice.SetMessage(S.Usb.PartialNote(failed), true);
                return;
            }

            // Không cách ly được tệp nào. Danh sách có thể đã cũ (bảo vệ nền hoặc
            // lần chạy trước vừa xử lý) — không báo "lỗi không mong muốn", kiểm tra lại.
            if (done == 0)
            {
                _batch = "";
                _batchRemote = false;
                if (batch.Length > 0) Quarantine.RemoveBatch(batch);
            }
            if (failed > 0)
            {
                ShowState(UsbState.Results);
                UpdateApplyButton();
                _notice.SetMessage(S.Usb.PartialNote(failed), true);
                return;
            }

            ShowState(UsbState.Results);
            UpdateApplyButton();
            _notice.SetMessage(S.Usb.NothingChanged, false);
            StartScan();
            if (_scanning) _notice.SetMessage(S.Usb.NothingChanged, false);
        }

        private void DoUndo()
        {
            if (_batch.Length == 0) return;
            // Vùng cách ly do engine SYSTEM ghi; GUI quyền thường không đọc được
            // manifest. Không thử khôi phục nửa vời rồi báo lỗi khó hiểu.
            if (_batchRemote)
            {
                _notice.SetMessage(S.Usb.UndoNeedsAdmin, true);
                return;
            }
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.UndoTitle(_doneCount), S.Dlg.UndoBody(_doneCount),
                                   S.Dlg.UndoOk, S.Dlg.Close, ButtonKind.Secondary, false))
                return;

            int restored, skipped, missing, refused = 0;
            bool paused = PauseWriteProtect();
            try
            {
                Quarantine.RestoreBatch(_batch, out restored, out skipped, out missing, out refused);
                if (missing == 0 && refused == 0) Quarantine.RemoveBatch(_batch);
            }
            catch (Exception e)
            {
                Log.Write(S.Log.UndoError(e.Message), "ERROR");
                restored = 0; skipped = 0; missing = _doneCount;
            }
            finally { ResumeWriteProtect(paused); }

            _batch = "";
            _batchRemote = false;
            _list.Clear();
            _notice.SetMessage(S.Usb.UndoDone(restored, skipped), false);
            if (missing > 0) _notice.SetMessage(S.Restore.Missing(missing), true);
            if (refused > 0) _notice.SetMessage(S.Restore.Refused(refused), true);
            ShowState(CurrentRoot() == null ? UsbState.NoDrive : UsbState.Idle);
        }

        private void DoVaccinate()
        {
            if (_isFolder) return;
            string drive = SelectedDrive();
            if (drive == null || _scanning || _applying || _unhiding) return;
            if (!Native.CanWrite(drive))
            {
                _notice.SetMessage(S.Err.DriveReadOnly(drive), true);
                return;
            }
            bool existed;
            string error;
            bool paused = PauseWriteProtect();
            bool ok;
            try { ok = Guard.Vaccinate(drive, out existed, out error); }
            finally { ResumeWriteProtect(paused); }
            if (ok)
                _notice.SetMessage(existed ? S.Usb.VaccineExists : S.Usb.VaccineDone(drive), false);
            else
                _notice.SetMessage(error, true);
        }

        // Hiện lại tệp và thư mục bị virus đặt thuộc tính ẩn. Không hoàn tác
        // tự động: người dùng chủ động bấm lại thao tác này khi cần.
        private void DoUnhide()
        {
            string drive = CurrentRoot();
            if (drive == null || _scanning || _applying || _unhiding) return;
            if (!Native.CanWrite(drive))
            {
                _notice.SetMessage(S.Err.DriveReadOnly(drive), true);
                return;
            }
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.UnhideTitle, S.Dlg.UnhideBody,
                                   S.Dlg.UnhideOk, S.Dlg.Cancel, ButtonKind.Secondary, false))
                return;

            _unhiding = true;
            _cancelFlag = false;
            _notice.ClearMessage();
            ShowState(_state);

            Thread t = new Thread(delegate()
            {
                int count = 0;
                bool paused = false;
                try
                {
                    paused = PauseWriteProtect();
                    List<string> nameless = UsbRestore.FindNameless(drive);
                    if (nameless.Count > 0)
                        count += UsbRestore.Restore(drive, nameless, delegate { return _cancelFlag; }).Moved;
                    count += UsbScan.Unhide(drive, UnhideScope.AllHidden,
                                            delegate { return _cancelFlag; });
                }
                catch (Exception e) { Log.Write(S.Log.UnhideError(e.Message), "ERROR"); }
                finally { ResumeWriteProtect(paused); }
                if (IsDisposed || !IsHandleCreated) return;
                _unhiding = false;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _unhiding = false;
                        _notice.SetMessage(count > 0 ? S.Usb.Unhidden(count) : S.Usb.UnhiddenNone, false);
                        ShowState(_state);
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Chọn một thư mục để quét thay vì cả ổ. Từ chối ổ hệ thống: mọi thao tác
        // ghi của dự án không được chạm vào đó.
        private void ChooseFolder()
        {
            if (_scanning || _applying || _unhiding) return;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = S.Usb.ChooseFolderHint;
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                string root = dlg.SelectedPath;
                if (string.IsNullOrEmpty(root) || root.Length < 3) return;
                string sys = Path.GetPathRoot(Environment.SystemDirectory) ?? "";
                if (sys.Length > 0 && root.StartsWith(sys, StringComparison.OrdinalIgnoreCase))
                {
                    _notice.SetMessage(S.Usb.FolderSystemRefused, true);
                    return;
                }
                _isFolder = true;
                _scanDrive = root.TrimEnd('\\') + "\\";
                _result = null;
                _list.Clear();
                _notice.ClearMessage();
                StartScan();
            }
        }

        // Khôi phục tên gốc cho tài liệu bị virus đổi sang Base64. Chỉ chạm tệp
        // trong thư mục giả/không tên và không ghi đè tên đã có.
        private void DoRenameBase64()
        {
            if (_result == null || _scanning || _applying || _unhiding) return;
            int n = _result.Base64Names;
            if (n <= 0) return;
            if (!ConfirmDialog.Ask(FindForm(), S.Dlg.RenameTitle(n), S.Dlg.RenameBody,
                                   S.Dlg.RenameOk(n), S.Dlg.Cancel, ButtonKind.Secondary, false))
                return;

            string root = _result.Root;
            _unhiding = true;
            _cancelFlag = false;
            _notice.ClearMessage();
            ShowState(_state);

            Thread t = new Thread(delegate()
            {
                int done = 0, skipped = 0;
                bool paused = false;
                try
                {
                    paused = PauseWriteProtect();
                    done = UsbRestore.RenameBase64(root, null,
                        delegate { return _cancelFlag; }, out skipped);
                }
                catch (Exception e) { Log.Write(S.Log.UnhideError(e.Message), "ERROR"); }
                finally { ResumeWriteProtect(paused); }
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _unhiding = false;
                        _notice.SetMessage(S.Usb.RenameDone(done, skipped),
                                           done == 0 && skipped > 0);
                        StartScan();
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ----------------------------------------------------------------- bố cục
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
            int contentTop = Ui.Px(206);

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            _drive.Bounds = Ui.Rect(Theme.PagePad, 66, 340, 30);
            int rw = Ui.Measure(S.Usb.Refresh, Theme.Small).Width + Ui.Px(24);
            _refresh.Bounds = new Rectangle(_drive.Right + Ui.Px(16), Ui.Px(70), rw, Ui.Px(22));

            _scan.Bounds = new Rectangle(pad, Ui.Px(112),
                TextWidth(_scan.Text, Theme.BodyStrong), Ui.Px(Theme.ControlHeight));
            _scanFolder.Bounds = new Rectangle(_scan.Right + Ui.Px(16), Ui.Px(119),
                Ui.Measure(S.Usb.ScanFolder, Theme.Small).Width + Ui.Px(4), Ui.Px(22));
            _vaccine.Bounds = new Rectangle(_scanFolder.Right + Ui.Px(24), Ui.Px(119),
                Ui.Measure(S.Usb.Vaccine, Theme.Small).Width + Ui.Px(24), Ui.Px(22));
            _unhide.Bounds = new Rectangle(_vaccine.Right + Ui.Px(16), Ui.Px(119),
                Ui.Measure(S.Usb.Unhide, Theme.Small).Width + Ui.Px(4), Ui.Px(22));
            _rename.Bounds = new Rectangle(_unhide.Right + Ui.Px(16), Ui.Px(119),
                Ui.Measure(S.Usb.RenameBase64(999), Theme.Small).Width + Ui.Px(4), Ui.Px(22));

            _notice.Bounds = new Rectangle(pad, Ui.Px(158), width, Ui.Px(38));

            int iconY = contentTop;
            _spinner.Bounds = new Rectangle(pad, iconY, Ui.Px(24), Ui.Px(24));
            _stateIcon.Bounds = new Rectangle(pad, iconY, Ui.Px(24), Ui.Px(24));

            _statusTitle.Bounds = new Rectangle(pad + Ui.Px(34), contentTop - Ui.Px(6),
                width - Ui.Px(34), Ui.Px(34));
            _statusSub.Bounds = new Rectangle(pad + Ui.Px(34), contentTop + Ui.Px(26),
                width - Ui.Px(34), Ui.Px(44));

            _cancel.Bounds = new Rectangle(right - TextWidth(S.Overview.Cancel, Theme.BodyStrong),
                contentTop - Ui.Px(4), TextWidth(S.Overview.Cancel, Theme.BodyStrong),
                Ui.Px(Theme.ControlHeight));

            int flagW = Math.Max(Ui.Measure(S.Usb.HideFlagged, Theme.Small).Width,
                Ui.Measure(S.Usb.ShowFlagged(999), Theme.Small).Width) + Ui.Px(24);
            _flagToggle.Bounds = new Rectangle(right - flagW, contentTop + Ui.Px(20),
                flagW, Ui.Px(20));

            _resultsTitle.Bounds = new Rectangle(pad, contentTop - Ui.Px(6), width, Ui.Px(24));
            _resultsNote.Bounds = new Rectangle(pad, contentTop + Ui.Px(20),
                Math.Max(Ui.Px(120), width - flagW - Ui.Px(12)), Ui.Px(20));

            int listTop = contentTop + Ui.Px(48);
            int listBottom = ClientSize.Height - Ui.Px(70);
            _list.Bounds = new Rectangle(pad, listTop, width, Math.Max(Ui.Px(120), listBottom - listTop));

            int barY = ClientSize.Height - Ui.Px(58);
            _selectAll.Bounds = new Rectangle(pad, barY, Ui.Measure(S.Usb.SelectAll, Theme.Small).Width + Ui.Px(4), Ui.Px(22));
            _selectNone.Bounds = new Rectangle(_selectAll.Right + Ui.Px(16), barY,
                Ui.Measure(S.Usb.SelectNone, Theme.Small).Width + Ui.Px(4), Ui.Px(22));

            int applyW = Math.Max(TextWidth(S.Usb.Apply(9), Theme.BodyStrong), Ui.Px(150));
            _apply.Bounds = new Rectangle(right - applyW, barY - Ui.Px(8), applyW, Ui.Px(Theme.ControlHeight));

            int undoW = Ui.Measure(S.Usb.Undo, Theme.Small).Width + Ui.Px(24);
            _undo.Bounds = new Rectangle(pad, contentTop + Ui.Px(84), undoW, Ui.Px(22));
            _log.Bounds = new Rectangle(pad, contentTop + Ui.Px(114),
                Ui.Measure(S.Usb.OpenLog, Theme.Small).Width + Ui.Px(4), Ui.Px(22));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _progressTimer.Dispose();
                _tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
