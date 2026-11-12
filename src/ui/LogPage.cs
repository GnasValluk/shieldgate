// ShieldGate - màn hình Nhật ký: xem, lọc lỗi, xuất tệp.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class LogPage : PageBase
    {
        private const int MaxLines = 2000;

        private readonly Label _title;
        private readonly Segment _filter;
        private readonly LinkText _refresh, _export, _open;
        private readonly Panel _box;
        private readonly RichTextBox _text;
        private readonly Notice _notice;

        public LogPage()
        {
            AutoScroll = false;

            _title = MakeHeading(S.Log.Title);
            Controls.Add(_title);

            _filter = new Segment();
            _filter.Options = new string[] { S.Log.All, S.Log.ErrorsOnly };
            _filter.SelectedChanged += delegate { LoadLog(); };
            Controls.Add(_filter);

            _refresh = MakeLink(S.Log.Refresh, "refresh");
            _refresh.Click += delegate { LoadLog(); };
            Controls.Add(_refresh);

            _export = MakeLink(S.Log.Export, "");
            _export.Click += delegate { DoExport(); };
            Controls.Add(_export);

            _notice = new Notice();
            Controls.Add(_notice);

            _box = new Panel();
            _box.BackColor = Theme.Surface;
            _box.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, _box.Width - 1, _box.Height - 1);
            };
            Controls.Add(_box);

            _text = new RichTextBox();
            _text.ReadOnly = true;
            _text.BorderStyle = BorderStyle.None;
            _text.BackColor = Theme.Surface;
            _text.ForeColor = Theme.Ink;
            _text.Font = Theme.Mono;
            _text.WordWrap = false;
            _text.ScrollBars = RichTextBoxScrollBars.Vertical;
            _text.DetectUrls = false;
            _text.AccessibleName = S.Log.Title;
            _text.ShortcutsEnabled = true;
            _box.Controls.Add(_text);

            _open = MakeLink(S.Log.OpenFolder, "");
            _open.Click += delegate { OpenLogFolder(); };
            Controls.Add(_open);

            LoadLog();
            LayoutPage();
        }

        public override void OnShown()
        {
            LoadLog();
        }

        // ---------------------------------------------------------------- đọc

        private void LoadLog()
        {
            _notice.ClearMessage();

            string[] all = new string[0];
            try
            {
                if (File.Exists(Paths.LogFile))
                    all = File.ReadAllLines(Paths.LogFile, Encoding.UTF8);
            }
            catch
            {
                all = new string[0];
            }

            bool errorsOnly = _filter.SelectedIndex == 1;
            List<string> pick = new List<string>();
            foreach (string raw in all)
            {
                if (raw == null) continue;
                string line = raw.TrimEnd('\r', '\n');
                if (line.Length == 0) continue;
                if (errorsOnly && line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                pick.Add(line);
            }

            int skip = Math.Max(0, pick.Count - MaxLines);
            StringBuilder sb = new StringBuilder();
            for (int i = skip; i < pick.Count; i++)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(pick[i]);
            }

            _text.SuspendLayout();
            _text.Clear();
            if (sb.Length == 0)
            {
                _text.SelectionColor = Theme.InkMuted;
                _text.Text = errorsOnly ? S.Log.NoErrors : S.Log.Empty;
                _text.SelectionStart = 0;
            }
            else
            {
                _text.Text = sb.ToString();
                PaintLevels(skip);
                _text.SelectionStart = _text.TextLength;
                _text.SelectionLength = 0;
                _text.ScrollToCaret();
            }
            _text.ResumeLayout();

            _export.Enabled = all.Length > 0;
        }

        // Tô màu ngày giờ, mức và nội dung theo từng dòng.
        private void PaintLevels(int firstShown)
        {
            string[] lines = _text.Text.Split('\n');
            int offset = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int len = line.Length;

                int a = line.IndexOf('[');
                int b = a >= 0 ? line.IndexOf(']', a) : -1;
                string level = (a >= 0 && b > a) ? line.Substring(a + 1, b - a - 1) : "";
                Color levelColor = Theme.InkMuted;
                if (string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase)) levelColor = Theme.Danger;
                else if (string.Equals(level, "WARN", StringComparison.OrdinalIgnoreCase)) levelColor = Theme.Warn;

                int dateLen = Math.Min(19, len);
                Select(offset, dateLen, Theme.InkFaint);
                if (a >= 0 && b > a && a < len)
                    Select(offset + a, Math.Min(b - a + 1, len - a), levelColor);
                if (levelColor != Theme.InkMuted && b >= 0 && b + 1 < len)
                    Select(offset + b + 1, len - b - 1, Theme.Ink);

                offset += len + 1;
            }
        }

        private void Select(int start, int length, Color color)
        {
            if (length <= 0 || start < 0 || start + length > _text.TextLength) return;
            _text.Select(start, length);
            _text.SelectionColor = color;
        }

        // --------------------------------------------------------------- xuất

        private void DoExport()
        {
            if (!File.Exists(Paths.LogFile))
            {
                _notice.SetMessage(S.Log.Empty, false);
                return;
            }

            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = S.Log.ExportTitle;
                d.Filter = S.Log.FilterName + "|*.txt";
                d.FileName = "shieldgate-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
                d.AddExtension = true;
                d.OverwritePrompt = true;

                IWin32Window owner = FindForm();
                DialogResult r = owner != null ? d.ShowDialog(owner) : d.ShowDialog();
                if (r != DialogResult.OK) return;

                try
                {
                    File.Copy(Paths.LogFile, d.FileName, true);
                    _notice.SetMessage(S.Log.Exported(d.FileName), false);
                    Log.Write("Xuất nhật ký: " + d.FileName);
                }
                catch (Exception e)
                {
                    _notice.SetMessage(S.Err.Locked(d.FileName), true);
                    Log.Write("Không xuất được nhật ký: " + e.Message, "ERROR");
                }
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                Paths.EnsureDirs();
                Process.Start("explorer.exe", "\"" + Paths.LogDir + "\"");
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
            int right = ClientSize.Width - pad;
            int width = right - pad;

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            _filter.Bounds = new Rectangle(pad, Ui.Px(62), Ui.Px(180), Ui.Px(30));

            int exportW = Ui.Measure(S.Log.Export, Theme.Small).Width + Ui.Px(4);
            int refreshW = Ui.Measure(S.Log.Refresh, Theme.Small).Width + Ui.Px(24);
            _refresh.Bounds = new Rectangle(right - exportW - Ui.Px(20) - refreshW, Ui.Px(66),
                refreshW, Ui.Px(22));
            _export.Bounds = new Rectangle(right - exportW, Ui.Px(66), exportW, Ui.Px(22));

            _notice.Bounds = new Rectangle(pad, Ui.Px(100), width, Ui.Px(38));

            int top = _notice.Visible ? Ui.Px(146) : Ui.Px(104);
            int bottom = ClientSize.Height - Ui.Px(48);

            _box.Bounds = new Rectangle(pad, top, width, Math.Max(Ui.Px(120), bottom - top));
            _text.Bounds = new Rectangle(Ui.Px(12), Ui.Px(10),
                Math.Max(Ui.Px(60), _box.Width - Ui.Px(24)),
                Math.Max(Ui.Px(80), _box.Height - Ui.Px(20)));

            _open.Bounds = new Rectangle(pad, ClientSize.Height - Ui.Px(36),
                Ui.Measure(S.Log.OpenFolder, Theme.Small).Width + Ui.Px(4), Ui.Px(22));
        }
    }
}
