// ShieldGate - màn hình Giới thiệu: phiên bản, giấy phép, dữ liệu, dấu vết tệp.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class AboutPage : PageBase
    {
        private readonly Label _title, _name, _version, _license;
        private readonly LogoView _logo;
        private readonly LinkText _update, _openData;
        private readonly Label _sourceLabel, _dataLabel;
        private readonly Label _sourceNote, _updateNote, _dataPath, _hashLabel, _noNetwork;
        private readonly TextBox _hash;
        private readonly Panel _hashBox;
        private int _sepY;

        public AboutPage()
        {
            AutoScroll = true;

            _title = MakeHeading(S.About.Title);
            Controls.Add(_title);

            _logo = new LogoView();
            Controls.Add(_logo);

            _name = MakeLabel(S.App.Name, Theme.Hero, Theme.Ink);
            Controls.Add(_name);

            _version = MakeLabel(S.About.Version(AppInfo.Version), Theme.Small, Theme.InkMuted);
            Controls.Add(_version);

            _license = MakeLabel(S.About.License, Theme.Small, Theme.InkMuted);
            Controls.Add(_license);

            _sourceLabel = MakeLabel(S.About.Source, Theme.Section, Theme.Ink);
            Controls.Add(_sourceLabel);

            _sourceNote = MakeLabel(S.About.SourceSoon, Theme.Body, Theme.InkMuted);
            Controls.Add(_sourceNote);

            _update = MakeLink(S.About.Update, "refresh");
            _update.IconSize = 13;
            _update.Click += delegate
            {
                _updateNote.Text = S.About.UpdateSoon;
                _updateNote.Visible = true;
            };
            Controls.Add(_update);

            _updateNote = MakeLabel("", Theme.Small, Theme.InkMuted);
            _updateNote.Visible = false;
            Controls.Add(_updateNote);

            _dataLabel = MakeLabel(S.About.Data, Theme.Section, Theme.Ink);
            Controls.Add(_dataLabel);

            _dataPath = MakeLabel(Paths.Root, Theme.Body, Theme.InkMuted);
            _dataPath.AutoEllipsis = false;
            _dataPath.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(_dataPath);

            _openData = MakeLink(S.About.OpenFolder, "usb");
            _openData.IconSize = 13;
            _openData.Click += delegate { OpenDataFolder(); };
            Controls.Add(_openData);

            _hashLabel = MakeLabel(S.About.Hash, Theme.Section, Theme.Ink);
            Controls.Add(_hashLabel);

            _hashBox = new Panel();
            _hashBox.BackColor = Theme.Surface;
            _hashBox.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, _hashBox.Width - 1, _hashBox.Height - 1);
            };
            Controls.Add(_hashBox);

            _hash = new TextBox();
            _hash.ReadOnly = true;
            _hash.BorderStyle = BorderStyle.None;
            _hash.BackColor = Theme.Surface;
            _hash.ForeColor = Theme.Ink;
            _hash.Font = Theme.Mono;
            _hash.Text = "…";
            _hash.AccessibleName = S.About.Hash;
            _hashBox.Controls.Add(_hash);

            _noNetwork = MakeLabel(S.About.NoNetwork, Theme.Small, Theme.InkFaint);
            Controls.Add(_noNetwork);

            LayoutPage();
            LoadHash();
        }

        public override void OnShown()
        {
            if (_hash.Text == "…" || _hash.Text.Length == 0) LoadHash();
        }

        // ------------------------------------------------------------- dấu vết

        private void LoadHash()
        {
            Thread t = new Thread(delegate()
            {
                string hash = "";
                try { hash = Native.Sha256(Application.ExecutablePath); }
                catch { }
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _hash.Text = hash.Length > 0 ? hash : S.Err.Unknown;
                        _hash.SelectionStart = 0;
                        _hash.SelectionLength = 0;
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void OpenDataFolder()
        {
            try
            {
                Paths.EnsureDirs();
                Process.Start("explorer.exe", "\"" + Paths.Root + "\"");
            }
            catch { }
        }

        // --------------------------------------------------------------- bố cục

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutPage();
        }

        private void LayoutPage()
        {
            if (ClientSize.Width <= 0) return;

            int pad = Pad;
            int width = ClientSize.Width - pad * 2;

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            _logo.Bounds = Ui.Rect(Theme.PagePad, 76, 40, 40);
            _name.Bounds = new Rectangle(pad + Ui.Px(56), Ui.Px(70), Ui.Px(320), Ui.Px(30));
            _version.Bounds = new Rectangle(pad + Ui.Px(56), Ui.Px(100), Ui.Px(320), Ui.Px(20));
            _license.Bounds = new Rectangle(pad + Ui.Px(56), Ui.Px(120), Ui.Px(320), Ui.Px(20));

            _sepY = Ui.Px(160);

            int y = Ui.Px(184);
            _sourceLabel.Bounds = new Rectangle(pad, y, Ui.Px(280), Ui.Px(20));
            _sourceNote.Bounds = new Rectangle(pad, y + Ui.Px(26), width, Ui.Px(22));
            _update.Bounds = new Rectangle(pad, y + Ui.Px(56),
                Ui.Measure(S.About.Update, Theme.Small).Width + Ui.Px(24), Ui.Px(22));
            _updateNote.Bounds = new Rectangle(pad + _update.Width + Ui.Px(14), y + Ui.Px(56),
                width - _update.Width - Ui.Px(14), Ui.Px(22));

            y += Ui.Px(96);
            _dataLabel.Bounds = new Rectangle(pad, y, Ui.Px(280), Ui.Px(20));
            _dataPath.Bounds = new Rectangle(pad, y + Ui.Px(26), width, Ui.Px(24));
            _openData.Bounds = new Rectangle(pad, y + Ui.Px(56),
                Ui.Measure(S.About.OpenFolder, Theme.Small).Width + Ui.Px(24), Ui.Px(22));

            y += Ui.Px(96);
            _hashLabel.Bounds = new Rectangle(pad, y, Ui.Px(280), Ui.Px(20));
            _hashBox.Bounds = new Rectangle(pad, y + Ui.Px(26), Math.Min(width, Ui.Px(560)), Ui.Px(30));
            _hash.Bounds = new Rectangle(Ui.Px(10), Ui.Px(7),
                Math.Max(Ui.Px(80), _hashBox.Width - Ui.Px(20)), Ui.Px(18));

            _noNetwork.Bounds = new Rectangle(pad, y + Ui.Px(92), width, Ui.Px(22));

            AutoScrollMinSize = new Size(0, y + Ui.Px(130));
        }

        protected override void PaintPage(Graphics g)
        {
            int pad = Pad;
            Ui.HLine(g, pad, ClientSize.Width - pad, _sepY, Theme.Line);
        }
    }
}
