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
        private readonly Label _title, _intro, _backup, _tech, _adminNote;
        private readonly SwitchRow _autorun, _ini, _wsh, _registry, _guard;
        private readonly LinkText _techLink, _reload;
        private readonly FlatButton _toggle;
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
                + S.Tech.Registry + "\r\n" + S.Tech.Guard,
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
            _loading = false;

            _toggle.Text = _status.FullyProtected ? S.Protect.Off : S.Protect.On;
            _toggle.Kind = _status.FullyProtected ? ButtonKind.Secondary : ButtonKind.Primary;

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
            _toggle.Enabled = _status.Admin && !_busy;
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

            Thread t = new Thread(delegate()
            {
                bool ok = false;
                try { ok = Guard.Configure(autorun, ini, wsh, registry, guard, null); }
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

            if (!_status.FullyProtected)
            {
                int changes = 0;
                if (!_status.Autorun) changes++;
                if (!_status.Ini) changes++;
                if (!_status.Registry) changes++;
                if (!_status.Running) changes++;
                if (!ConfirmDialog.Ask(FindForm(), S.Dlg.ProtectOnTitle,
                                       S.Dlg.ProtectOnBody(changes), S.Dlg.ProtectOnOk,
                                       S.Dlg.Cancel, ButtonKind.Primary, true))
                    return;

                _loading = true;
                _autorun.Checked = true;
                _ini.Checked = true;
                _registry.Checked = true;
                _guard.Checked = true;
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

        private void LayoutPage()
        {
            if (ClientSize.Width <= 0) return;

            int pad = Pad;
            int right = ClientSize.Width - pad;
            int width = right - pad;

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            int toggleW = Math.Max(TextWidth(S.Protect.On, Theme.BodyStrong), Ui.Px(140));
            _toggle.Bounds = new Rectangle(right - toggleW, Ui.Px(58), toggleW, Ui.Px(Theme.ControlHeight));

            _intro.Bounds = new Rectangle(pad, Ui.Px(62), width - toggleW - Ui.Px(28), Ui.Px(44));

            _adminNote.Bounds = new Rectangle(pad, Ui.Px(112), Ui.Px(320), Ui.Px(22));
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

            _techLink.Bounds = new Rectangle(pad, y + Ui.Px(14),
                Ui.Measure(S.Protect.Tech, Theme.Small).Width + Ui.Px(24), Ui.Px(22));

            int techY = y + Ui.Px(46);
            _tech.Bounds = new Rectangle(pad, techY, width, Ui.Px(140));

            int backupY = _techOpen ? techY + Ui.Px(150) : techY + Ui.Px(10);
            _backup.Bounds = new Rectangle(pad, backupY, width, Ui.Px(40));

            AutoScrollMinSize = new Size(0, backupY + Ui.Px(70));
        }
    }
}
