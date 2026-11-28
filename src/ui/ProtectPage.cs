// ShieldGate - màn hình Bảo vệ: bốn công tắc, sao lưu, chi tiết kỹ thuật.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class ProtectPage : PageBase
    {
        private readonly StatusInfo _status = new StatusInfo();
        private readonly Label _title, _intro, _backup, _tech, _adminNote, _vtTitle, _vtState;
        private readonly SwitchRow _autorun, _ini, _wsh, _registry, _guard, _writeProtect, _blockExec, _badUsbSwitch, _vtSwitch;
        private readonly LinkText _techLink, _reload, _vtClear;
        private readonly FlatButton _toggle, _vtSave;
        private readonly Panel _vtKeyBox;
        private readonly TextBox _vtKey;
        private readonly Notice _notice;

        private bool _busy, _loading, _techOpen;

        public ProtectPage()
        {
            AutoScroll = true;

            _title = MakeHeading(S.Protect.Title);
            Controls.Add(_title);

            _intro = MakeLabel(S.Protect.Intro, Theme.Body, Theme.InkMuted);
            _intro.AutoEllipsis = false;
            _intro.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(_intro);

            _toggle = MakeButton(S.Protect.On, ButtonKind.Primary);
            _toggle.Click += delegate { ToggleAll(); };
            Controls.Add(_toggle);

            _notice = new Notice();
            Controls.Add(_notice);

            _adminNote = MakeLabel(S.Protect.AdminOnly, Theme.Small, Theme.Warn);
            _adminNote.Visible = false;
            Controls.Add(_adminNote);

            _reload = MakeLink(S.Protect.Reload, "refresh");
            _reload.IconSize = 13;
            _reload.Visible = false;
            _reload.Click += delegate { RelaunchElevated(); };
            Controls.Add(_reload);

            _autorun = MakeSwitch(S.Protect.SwitchAutorun, S.Protect.SwitchAutorunDetail);
            _ini = MakeSwitch(S.Protect.SwitchIni, S.Protect.SwitchIniDetail);
            _wsh = MakeSwitch(S.Protect.SwitchWsh, S.Protect.SwitchWshDetail);
            _registry = MakeSwitch(S.Protect.SwitchRegistry, S.Protect.SwitchRegistryDetail);
            _guard = MakeSwitch(S.Protect.SwitchGuard, S.Protect.SwitchGuardDetail);
            _writeProtect = MakeSwitch(S.Protect.SwitchWriteProtect, S.Protect.SwitchWriteProtectDetail);
            _blockExec = MakeSwitch(S.Protect.SwitchBlockExec, S.Protect.SwitchBlockExecDetail);

            // BadUSB là thiết lập của bản tray, không đi qua Guard.Configure;
            // chỉ ghi settings.txt và có hiệu lực ở nhịp làm mới kế tiếp.
            _badUsbSwitch = new SwitchRow();
            _badUsbSwitch.Text = S.Protect.BadUsbSwitch;
            _badUsbSwitch.Detail = S.Protect.BadUsbSwitchDetail;
            _badUsbSwitch.CheckedChanged += delegate
            {
                if (_loading || _busy) return;
                Settings.SetBadUsbEnabled(_badUsbSwitch.Checked);
            };
            Controls.Add(_badUsbSwitch);

            // VirusTotal là opt-in riêng: công tắc này không đi qua Guard.Configure,
            // chỉ ghi settings.txt và không gửi gì khi tắt.
            _vtTitle = MakeLabel(S.Protect.VtTitle, Theme.Section, Theme.Ink);
            Controls.Add(_vtTitle);

            _vtSwitch = new SwitchRow();
            _vtSwitch.Text = S.Protect.VtSwitch;
            _vtSwitch.Detail = S.Protect.VtSwitchDetail;
            _vtSwitch.CheckedChanged += delegate
            {
                if (_loading || _busy) return;
                ApplyVtSwitch();
            };
            Controls.Add(_vtSwitch);

            _vtKeyBox = new Panel();
            _vtKeyBox.BackColor = Theme.Surface;
            _vtKeyBox.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, _vtKeyBox.Width - 1, _vtKeyBox.Height - 1);
            };
            Controls.Add(_vtKeyBox);

            _vtKey = new TextBox();
            _vtKey.BorderStyle = BorderStyle.None;
            _vtKey.BackColor = Theme.Surface;
            _vtKey.ForeColor = Theme.Ink;
            _vtKey.Font = Theme.Mono;
            _vtKey.UseSystemPasswordChar = true;
            _vtKey.AccessibleName = S.Protect.VtKeyLabel;
            _vtKeyBox.Controls.Add(_vtKey);

            _vtSave = MakeButton(S.Protect.VtSave, ButtonKind.Secondary);
            _vtSave.Click += delegate { SaveVtKey(); };
            Controls.Add(_vtSave);

            _vtClear = MakeLink(S.Protect.VtClear, "ban");
            _vtClear.IconSize = 13;
            _vtClear.Click += delegate { ClearVtKey(); };
            Controls.Add(_vtClear);

            _vtState = MakeLabel(S.Protect.VtNoKey, Theme.Small, Theme.InkMuted);
            _vtState.AutoEllipsis = false;
            Controls.Add(_vtState);

            _techLink = MakeLink(S.Protect.Tech, "chevron");
            _techLink.IconSize = 11;
            _techLink.Click += delegate
            {
                _techOpen = !_techOpen;
                _techLink.Text = _techOpen ? S.Protect.TechHide : S.Protect.Tech;
                _tech.Visible = _techOpen;
                LayoutPage();
            };
            Controls.Add(_techLink);

            _tech = MakeLabel(
                S.Tech.Autorun + "\r\n" + S.Tech.Ini + "\r\n" + S.Tech.Wsh + "\r\n"
                + S.Tech.Registry + "\r\n" + S.Tech.Guard + "\r\n"
                + S.Tech.WriteProtect + "\r\n" + S.Tech.BlockExec,
                Theme.Mono, Theme.InkMuted);
            _tech.TextAlign = ContentAlignment.TopLeft;
            _tech.Visible = false;
            Controls.Add(_tech);

            _backup = MakeLabel(S.Protect.BackupNote, Theme.Small, Theme.InkFaint);
            _backup.AutoEllipsis = false;
            _backup.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(_backup);

            ApplyStatus(null);
            LayoutPage();
        }

        private SwitchRow MakeSwitch(string title, string detail)
        {
            SwitchRow row = new SwitchRow();
            row.Text = title;
            row.Detail = detail;
            row.CheckedChanged += delegate
            {
                if (_loading || _busy) return;
                ApplySwitches();
            };
            Controls.Add(row);
            return row;
        }

        public override bool Busy { get { return _busy; } }

        public override void OnShown()
        {
            RefreshStatus();
        }

        // ------------------------------------------------------------- trạng thái

        private void RefreshStatus()
        {
            if (_busy) return;
            Thread t = new Thread(delegate()
            {
                StatusInfo st = null;
                try { st = Guard.GetStatus(); }
                catch { }
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((MethodInvoker)delegate { ApplyStatus(st); }); }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void ApplyStatus(StatusInfo st)
        {
            if (st != null)
            {
                _status.Admin = st.Admin;
                _status.Autorun = st.Autorun;
                _status.Ini = st.Ini;
                _status.Wsh = st.Wsh;
                _status.Registry = st.Registry;
                _status.WriteProtect = st.WriteProtect;
                _status.BlockExec = st.BlockExec;
                _status.Task = st.Task;
                _status.Running = st.Running;
                _status.QuarantineFiles = st.QuarantineFiles;
                _status.QuarantineBatches = st.QuarantineBatches;
            }

            _loading = true;
            _autorun.Checked = _status.Autorun;
            _ini.Checked = _status.Ini;
            _wsh.Checked = _status.Wsh;
            _registry.Checked = _status.Registry;
            _guard.Checked = _status.Running;
            _writeProtect.Checked = _status.WriteProtect;
            _blockExec.Checked = _status.BlockExec;
            _badUsbSwitch.Checked = Settings.BadUsbEnabled;
            _vtSwitch.Checked = Settings.VirusTotalEnabled;
            _loading = false;
            UpdateVtState();

            _toggle.Text = _status.ProtectionOn ? S.Protect.Off : S.Protect.On;
            _toggle.Kind = _status.ProtectionOn ? ButtonKind.Secondary : ButtonKind.Primary;

            _adminNote.Visible = !_status.Admin;
            _reload.Visible = !_status.Admin;
            SetEnabledState();

            LayoutPage();
        }

        private void SetEnabledState()
        {
            bool on = _status.Admin && !_busy;
            _autorun.Enabled = on;
            _ini.Enabled = on;
            _wsh.Enabled = on;
            _registry.Enabled = on;
            _guard.Enabled = on;
            _writeProtect.Enabled = on;
            _blockExec.Enabled = on;
            _badUsbSwitch.Enabled = on;
            _toggle.Enabled = _status.Admin && !_busy;
            // VirusTotal là thiết lập của người dùng, không phải chính sách hệ
            // thống; vẫn cần quyền ghi thư mục dữ liệu nên đi cùng điều kiện.
            _vtSwitch.Enabled = on;
            _vtKey.Enabled = on;
            _vtSave.Enabled = on;
            _vtClear.Enabled = on;
        }

        // ------------------------------------------------------------ VirusTotal

        private void ApplyVtSwitch()
        {
            if (_busy) return;
            bool on = _vtSwitch.Checked;
            if (on && !Settings.VirusTotalConsent)
            {
                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.VtTitle, S.Dlg.VtBody,
                                       S.Dlg.VtOk, S.Dlg.Cancel, ButtonKind.Primary, true))
                {
                    _loading = true;
                    _vtSwitch.Checked = false;
                    _loading = false;
                    return;
                }
                Settings.SetVirusTotalConsent();
            }

            Settings.SetVirusTotalEnabled(on);
            UpdateVtState();
            if (!on) _notice.SetMessage(S.Protect.VtDisabled, false);
            else if (VtKey.HasKey) _notice.SetMessage(S.Protect.VtEnabled, false);
            else _notice.SetMessage(S.Protect.VtOnNoKey, false);
        }

        private void SaveVtKey()
        {
            if (_busy) return;
            string key = (_vtKey.Text ?? "").Trim();
            if (!VirusTotal.IsApiKey(key))
            {
                _notice.SetMessage(S.Protect.VtBadKey, true);
                return;
            }
            if (!VtKey.Save(key))
            {
                _notice.SetMessage(S.Protect.VtSaveFailed, true);
                return;
            }
            _vtKey.Text = "";
            _notice.SetMessage(S.Protect.VtSaved, false);
            UpdateVtState();
        }

        private void ClearVtKey()
        {
            if (_busy) return;
            VtKey.Delete();
            _vtKey.Text = "";
            _notice.SetMessage(S.Protect.VtCleared, false);
            UpdateVtState();
        }

        private void UpdateVtState()
        {
            bool has = VtKey.HasKey;
            _vtState.Text = has ? S.Protect.VtKeySaved : S.Protect.VtNoKey;
            _vtState.ForeColor = has ? Theme.InkMuted : Theme.Warn;
        }

        // --------------------------------------------------------------- áp dụng

        private void ApplySwitches()
        {
            if (_busy) return;
            _busy = true;
            SetEnabledState();
            _notice.SetMessage(S.Protect.Applying, false);

            bool autorun = _autorun.Checked;
            bool ini = _ini.Checked;
            bool wsh = _wsh.Checked;
            bool registry = _registry.Checked;
            bool guard = _guard.Checked;
            bool writeProtect = _writeProtect.Checked;
            bool blockExec = _blockExec.Checked;

            Thread t = new Thread(delegate()
            {
                bool ok = false;
                try { ok = Guard.Configure(autorun, ini, wsh, registry, guard, null, writeProtect, blockExec); }
                catch (Exception e) { Log.Write(S.Log.ProtectionApplyError(e.Message), "ERROR"); }
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((MethodInvoker)delegate { FinishApply(ok); }); }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void FinishApply(bool ok)
        {
            _busy = false;
            SetEnabledState();
            _notice.SetMessage(ok ? S.Cli.Configured : S.Cli.ConfigurePartial, !ok);
            RefreshStatus();
        }

        private void ToggleAll()
        {
            if (_busy || !_status.Admin) return;

            if (!_status.ProtectionOn)
            {
                // Bật lại đúng lựa chọn đã lưu: mục người dùng chủ động tắt
                // (ví dụ chặn AutoRun) phải ở yên, không bị bật lại.
                GuardConfig cfg = Guard.EnableConfig();

                int changes = 0;
                if (cfg.Autorun && !_status.Autorun) changes++;
                if (cfg.Ini && !_status.Ini) changes++;
                if (cfg.Registry && !_status.Registry) changes++;
                if (!_status.Running) changes++;
                if (cfg.Wsh && !_status.Wsh) changes++;
                if (cfg.WriteProtect && !_status.WriteProtect) changes++;
                if (cfg.BlockExec && !_status.BlockExec) changes++;

                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ProtectOnTitle,
                                       S.Dlg.ProtectOnBody(changes), S.Dlg.ProtectOnOk,
                                       S.Dlg.Cancel, ButtonKind.Primary, true))
                    return;

                _loading = true;
                _autorun.Checked = cfg.Autorun;
                _ini.Checked = cfg.Ini;
                _wsh.Checked = cfg.Wsh;
                _registry.Checked = cfg.Registry;
                // "Bật bảo vệ" phải chạy bảo vệ nền; các lớp registry chỉ là
                // phần thêm và đi theo lựa chọn đã lưu.
                _guard.Checked = true;
                _writeProtect.Checked = cfg.WriteProtect;
                _blockExec.Checked = cfg.BlockExec;
                _loading = false;
            }
            else
            {
                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ProtectOffTitle, S.Dlg.ProtectOffBody,
                                       S.Dlg.ProtectOffOk, S.Dlg.Keep, ButtonKind.Danger, false))
                    return;

                _loading = true;
                _autorun.Checked = false;
                _ini.Checked = false;
                _wsh.Checked = false;
                _registry.Checked = false;
                _guard.Checked = false;
                _writeProtect.Checked = false;
                _blockExec.Checked = false;
                _loading = false;
            }

            ApplySwitches();
        }

        private void RelaunchElevated()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Application.Exit();
            }
            catch { }
        }

        // ---------------------------------------------------------------- bố cục

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

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            int toggleW = Math.Max(TextWidth(S.Protect.On, Theme.BodyStrong), Ui.Px(140));
            _toggle.Bounds = new Rectangle(right - toggleW, Ui.Px(58), toggleW, Ui.Px(Theme.ControlHeight));

            _intro.Bounds = new Rectangle(pad, Ui.Px(62), width - toggleW - Ui.Px(28), Ui.Px(44));

            // Câu nhắc dài hai dòng ở cỡ chữ hiện tại; để 22 px thì dòng thứ
            // hai bị cắt mất, nên chừa đủ chỗ tới sát hàng công tắc đầu tiên.
            _adminNote.Bounds = new Rectangle(pad, Ui.Px(112), Ui.Px(320), Ui.Px(40));
            _reload.Bounds = new Rectangle(pad + Ui.Px(330), Ui.Px(112),
                Ui.Measure(S.Protect.Reload, Theme.Small).Width + Ui.Px(24), Ui.Px(22));

            _notice.Bounds = new Rectangle(pad, Ui.Px(112), width, Ui.Px(38));
            if (_adminNote.Visible) _notice.Visible = false;

            int y = Ui.Px(162);
            int rowH = Ui.Px(76);
            _autorun.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _ini.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _wsh.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _registry.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _guard.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _writeProtect.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _blockExec.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;
            _badUsbSwitch.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;

            _vtTitle.Bounds = new Rectangle(pad, y + Ui.Px(10), width, Ui.Px(22));
            y += Ui.Px(36);
            _vtSwitch.Bounds = new Rectangle(pad, y, width, rowH); y += rowH;

            int saveW = Math.Max(TextWidth(S.Protect.VtSave, Theme.BodyStrong), Ui.Px(120));
            int keyW = Math.Max(Ui.Px(180), width - saveW - Ui.Px(16));
            _vtKeyBox.Bounds = new Rectangle(pad, y, keyW, Ui.Px(30));
            _vtKey.Bounds = new Rectangle(Ui.Px(8), Ui.Px(7), keyW - Ui.Px(16), Ui.Px(18));
            _vtSave.Bounds = new Rectangle(right - saveW, y, saveW, Ui.Px(Theme.ControlHeight));

            _vtState.Bounds = new Rectangle(pad, y + Ui.Px(38), width - Ui.Px(120), Ui.Px(22));
            _vtClear.Bounds = new Rectangle(right - Ui.Px(110), y + Ui.Px(38), Ui.Px(110), Ui.Px(22));
            y += Ui.Px(70);

            _techLink.Bounds = new Rectangle(pad, y + Ui.Px(14),
                Ui.Measure(S.Protect.Tech, Theme.Small).Width + Ui.Px(24), Ui.Px(22));

            int techY = y + Ui.Px(46);
            _tech.Bounds = new Rectangle(pad, techY, width, Ui.Px(140));

            int backupY = _techOpen ? techY + Ui.Px(150) : techY + Ui.Px(10);
            _backup.Bounds = new Rectangle(pad, backupY, width, Ui.Px(40));

            SetScrollHeight(backupY + Ui.Px(70));
        }
    }
}
