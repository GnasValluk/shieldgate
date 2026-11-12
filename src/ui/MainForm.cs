// ShieldGate - cửa sổ chính: dải điều hướng bên trái, năm màn hình, chân trang.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class MainForm : Form
    {
        private readonly RailPanel _rail;
        private readonly Panel _host;
        private readonly FooterBar _footer;
        private readonly PageBase[] _pages = new PageBase[5];
        private PageId _current = (PageId)(-1);
        private bool _syncing;

        public MainForm()
        {
            Text = S.App.WindowTitle;
            Font = Theme.Body;
            BackColor = Theme.Surface;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            DoubleBuffered = true;
            ClientSize = new Size(Ui.Px(1000), Ui.Px(680));
            MinimumSize = new Size(Ui.Px(880), Ui.Px(600));
            try { Icon = MakeIcon(); } catch { }

            _host = new Panel();
            _host.Dock = DockStyle.Fill;
            _host.BackColor = Theme.Surface;
            Controls.Add(_host);

            _footer = new FooterBar();
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = Ui.Px(44);
            Controls.Add(_footer);

            _rail = new RailPanel();
            _rail.Dock = DockStyle.Left;
            _rail.Width = Ui.Px(Theme.RailWidth);
            _rail.Navigate += delegate { ShowPage(_rail.Selected); };
            Controls.Add(_rail);

            ShowPage(PageId.Overview);

#if SCREENSHOT
            // Chỉ bản dựng để chụp giao diện: mở thẳng một trang theo biến môi trường.
            string start = Environment.GetEnvironmentVariable("SHIELDGATE_START_PAGE");
            if (!string.IsNullOrEmpty(start))
            {
                try { ShowPage((PageId)Enum.Parse(typeof(PageId), start, true)); }
                catch { }
            }
#endif
        }

        private static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Icons.DrawLogo(g, new RectangleF(2, 2, 28, 28), Theme.Accent);
                }
                IntPtr h = bmp.GetHicon();
                return Icon.FromHandle(h);
            }
        }

        // ---------------------------------------------------------- điều hướng

        private void ShowPage(PageId id)
        {
            int index = (int)id;
            if (index < 0 || index >= _pages.Length) return;

            PageBase page = _pages[index];
            if (page == null)
            {
                page = CreatePage(id);
                if (page == null) return;
                _pages[index] = page;
                page.Dock = DockStyle.Fill;
                page.NavigateRequested += delegate(PageId target) { ShowPage(target); };
                page.UsbRequested += OpenUsb;
                _host.Controls.Add(page);
            }

            if (_current == id)
            {
                if (!_syncing) page.OnShown();
                return;
            }

            for (int i = 0; i < _pages.Length; i++)
                if (_pages[i] != null) _pages[i].Visible = _pages[i] == page;

            page.Visible = true;
            page.BringToFront();
            _current = id;

            _syncing = true;
            try { _rail.Select(id); }
            finally { _syncing = false; }

            page.OnShown();
        }

        private void OpenUsb(string drive)
        {
            ShowPage(PageId.Usb);
            UsbPage page = _pages[(int)PageId.Usb] as UsbPage;
            if (page != null && !string.IsNullOrEmpty(drive)) page.FocusDrive(drive);
        }

        private PageBase CreatePage(PageId id)
        {
            switch (id)
            {
                case PageId.Overview: return new OverviewPage();
                case PageId.Usb: return new UsbPage();
                case PageId.Protect: return new ProtectPage();
                case PageId.Log: return new LogPage();
                case PageId.About: return new AboutPage();
                default: return null;
            }
        }

        private PageBase CurrentPage
        {
            get
            {
                int i = (int)_current;
                return (i >= 0 && i < _pages.Length) ? _pages[i] : null;
            }
        }

        // --------------------------------------------------------------- phím

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData >= (Keys.Control | Keys.D1) && keyData <= (Keys.Control | Keys.D5))
            {
                ShowPage((PageId)(keyData - (Keys.Control | Keys.D1)));
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // --------------------------------------------------------------- đóng

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PageBase page = CurrentPage;
            if (page != null) page.OnShown();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;

            PageBase page = CurrentPage;
            if (page == null || !page.Busy) return;

            bool quit = ConfirmDialog.Ask(this, S.Dlg.CloseRunningTitle, S.Dlg.CloseRunningBody,
                                          S.Dlg.CloseRunningOk, S.Dlg.Stay, ButtonKind.Danger, false);
            if (!quit)
            {
                e.Cancel = true;
                return;
            }
            page.CancelWork();
        }
    }
}
