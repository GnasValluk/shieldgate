// ShieldGate - màn hình Nhật ký: chia nhóm, chia trang để không nạp cả tệp một lượt.
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
        // Mỗi trang chỉ vẽ vài trăm dòng: cuộn mượt, không nhiễu, không đơ máy.
        private const int PageSize = 250;
        // Chỉ đọc phần cuối tệp nhật ký (tệp có thể tới 5 MB).
        private const int MaxReadBytes = 2 * 1024 * 1024;

        private readonly Label _title, _pageLabel;
        private readonly Segment _filter;
        private readonly LinkText _refresh, _export, _open, _newer, _older;
        private readonly Panel _box;
        private readonly RichTextBox _text;
        private readonly Notice _notice;

        private List<string> _raw = new List<string>();
        private List<string> _shown = new List<string>();
        private int _page;   // 0 = trang mới nhất

        public LogPage()
        {
            AutoScroll = false;

            _title = MakeHeading(S.Log.Title);
            Controls.Add(_title);

            _filter = new Segment();
            _filter.Options = new string[] { S.Log.All, S.Log.Warnings, S.Log.ErrorsOnly };
            _filter.SelectedChanged += delegate
            {
                _page = 0;
                ApplyFilter();
            };
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

            _newer = MakeLink(S.Log.Newer, "");
            _newer.Click += delegate { GoPage(-1); };
            Controls.Add(_newer);

            _older = MakeLink(S.Log.Older, "");
            _older.Click += delegate { GoPage(1); };
            Controls.Add(_older);

            _pageLabel = MakeLabel("", Theme.Small, Theme.InkMuted);
            _pageLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(_pageLabel);

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
            _raw = ReadTail(Paths.LogFile, MaxReadBytes);
            _page = 0;
            ApplyFilter();
        }

        // Lọc theo mức: tất cả / cảnh báo / lỗi. Giữ nguyên danh sách đã đọc,
        // chỉ lọc lại trong bộ nhớ nên đổi bộ lọc không cần đọc tệp lần nữa.
        private void ApplyFilter()
        {
            _shown = new List<string>();
            int mode = _filter.SelectedIndex;
            foreach (string line in _raw)
            {
                if (mode == 1 && !HasLevel(line, "[WARN]")) continue;
                if (mode == 2 && !HasLevel(line, "[ERROR]")) continue;
                _shown.Add(line);
            }
            RenderPage();
        }

        private static bool HasLevel(string line, string tag)
        {
            return line.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void GoPage(int delta)
        {
            _page += delta;
            RenderPage();
        }

        private void RenderPage()
        {
            int total = _shown.Count;
            int pages = Math.Max(1, (total + PageSize - 1) / PageSize);
            if (_page > pages - 1) _page = pages - 1;
            if (_page < 0) _page = 0;

            int end = total - _page * PageSize;
            int start = Math.Max(0, end - PageSize);
            int count = Math.Max(0, end - start);

            _text.SuspendLayout();
            _text.Clear();
            if (count == 0)
            {
                _text.SelectionColor = Theme.InkMuted;
                _text.Text = EmptyText();
                _text.SelectionStart = 0;
                _text.SelectionLength = 0;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < end; i++)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(_shown[i]);
                }
                _text.Text = sb.ToString();
                PaintLevels();
                _text.SelectionStart = _text.TextLength;
                _text.SelectionLength = 0;
                _text.ScrollToCaret();
            }
            _text.ResumeLayout();

            _pageLabel.Text = count == 0 ? S.Log.PageEmpty : S.Log.Page(_page + 1, pages, count);
            _newer.Visible = count > 0 && pages > 1;
            _older.Visible = _newer.Visible;
            _newer.Enabled = _page > 0;
            _older.Enabled = _page < pages - 1;
            _export.Enabled = _raw.Count > 0;

            LayoutPage();
        }

        private string EmptyText()
        {
            if (_filter.SelectedIndex == 1) return S.Log.NoWarnings;
            if (_filter.SelectedIndex == 2) return S.Log.NoErrors;
            return S.Log.Empty;
        }

        // Chỉ đọc phần cuối tệp: nhật ký dài không làm chậm màn hình.
        private static List<string> ReadTail(string path, int maxBytes)
        {
            List<string> list = new List<string>();
            try
            {
                if (!File.Exists(path)) return list;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = fs.Length;
                    long start = Math.Max(0, length - maxBytes);
                    fs.Seek(start, SeekOrigin.Begin);

                    byte[] buffer = new byte[length - start];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int n = fs.Read(buffer, read, buffer.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }

                    string text = Encoding.UTF8.GetString(buffer, 0, read);
                    if (start > 0)
                    {
                        // Bỏ dòng đầu bị cắt giữa chừng.
                        int cut = text.IndexOf('\n');
                        if (cut >= 0) text = text.Substring(cut + 1);
                        else text = "";
                    }

                    foreach (string raw in text.Split('\n'))
                    {
                        string line = raw.TrimEnd('\r');
                        if (line.Length > 0 && line[0] == '\uFEFF') line = line.Substring(1);
                        if (line.Length == 0) continue;
                        list.Add(line);
                    }
                }
            }
            catch
            {
                list.Clear();
            }
            return list;
        }

        // Tô màu ngày giờ, mức và nội dung theo từng dòng của trang đang hiện.
        private void PaintLevels()
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
                    Log.Write(S.Log.LogExported(d.FileName));
                }
                catch (Exception e)
                {
                    _notice.SetMessage(S.Err.Locked(d.FileName), true);
                    Log.Write(S.Log.LogExportFailed(e.Message), "ERROR");
                }
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                Paths.EnsureDirs();
                Process.Start(Native.ShellExe("explorer.exe"), "\"" + Paths.LogDir + "\"");
            }
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

            _title.Bounds = Ui.Rect(Theme.PagePad, 20, 320, 34);

            _filter.Bounds = new Rectangle(pad, Ui.Px(62), Ui.Px(320), Ui.Px(30));

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

            int feetY = ClientSize.Height - Ui.Px(36);
            _open.Bounds = new Rectangle(pad, feetY,
                Ui.Measure(S.Log.OpenFolder, Theme.Small).Width + Ui.Px(4), Ui.Px(22));

            int olderW = Ui.Measure(S.Log.Older, Theme.Small).Width + Ui.Px(4);
            int newerW = Ui.Measure(S.Log.Newer, Theme.Small).Width + Ui.Px(4);
            int labelW = Math.Max(Ui.Px(170), Ui.Measure(_pageLabel.Text, Theme.Small).Width + Ui.Px(8));

            _older.Bounds = new Rectangle(right - olderW, feetY, olderW, Ui.Px(22));
            _newer.Bounds = new Rectangle(_older.Left - Ui.Px(14) - newerW, feetY, newerW, Ui.Px(22));
            _pageLabel.Bounds = new Rectangle(_newer.Left - Ui.Px(14) - labelW, feetY, labelW, Ui.Px(22));
        }
    }
}
