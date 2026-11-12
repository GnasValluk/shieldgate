// ShieldGate - hộp thoại xác nhận, khôi phục tệp, kết quả kiểm tra máy.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ShieldGate
{
    // --------------------------------------------------------- hộp thoại chung

    internal sealed class ConfirmDialog : Form
    {
        public bool Accepted;

        private ConfirmDialog(string title, string body, string okText, string cancelText,
                              ButtonKind kind, bool defaultOk)
        {
            Text = S.App.Name;
            BackColor = Theme.Surface;
            Font = Theme.Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;

            int width = Ui.Px(540);
            int pad = Ui.Px(26);
            int inner = width - pad * 2;

            Size bodySize = TextRenderer.MeasureText(body ?? "", Theme.Body,
                new Size(inner, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            int titleH = Ui.Px(30);
            int bodyY = pad + titleH + Ui.Px(8);
            int buttonsY = bodyY + bodySize.Height + Ui.Px(26);
            int height = buttonsY + Ui.Px(Theme.ControlHeight) + pad;

            ClientSize = new Size(width, height);

            Label head = new Label();
            head.Text = title;
            head.Font = Theme.Hero;
            head.ForeColor = Theme.Ink;
            head.AutoSize = false;
            head.Bounds = new Rectangle(pad, pad, inner, titleH);
            head.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(head);

            Label text = new Label();
            text.Text = body ?? "";
            text.Font = Theme.Body;
            text.ForeColor = Theme.InkMuted;
            text.AutoSize = false;
            text.Bounds = new Rectangle(pad, bodyY, inner, bodySize.Height + Ui.Px(2));
            text.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(text);

            Size okSize = Ui.Measure(okText, Theme.BodyStrong);
            Size cancelSize = Ui.Measure(cancelText, Theme.Body);

            int okW = okSize.Width + Ui.Px(34);
            int cancelW = cancelSize.Width + Ui.Px(34);
            int right = width - pad;

            FlatButton ok = new FlatButton();
            ok.Text = okText;
            ok.Kind = kind;
            ok.Bounds = new Rectangle(right - okW, buttonsY, okW, Ui.Px(Theme.ControlHeight));
            ok.Click += delegate { Accepted = true; Close(); };
            Controls.Add(ok);

            FlatButton cancel = new FlatButton();
            cancel.Text = cancelText;
            cancel.Kind = ButtonKind.Secondary;
            cancel.Bounds = new Rectangle(right - okW - Ui.Px(8) - cancelW, buttonsY, cancelW,
                                          Ui.Px(Theme.ControlHeight));
            cancel.Click += delegate { Accepted = false; Close(); };
            Controls.Add(cancel);

            ActiveControl = defaultOk ? (Control)ok : cancel;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Accepted = false;
                Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        public static bool Ask(IWin32Window owner, string title, string body,
                               string okText, string cancelText, ButtonKind kind, bool defaultOk)
        {
            using (ConfirmDialog d = new ConfirmDialog(title, body, okText, cancelText, kind, defaultOk))
            {
                if (owner != null) d.ShowDialog(owner);
                else d.ShowDialog();
                return d.Accepted;
            }
        }
    }

    // ------------------------------------------------------------ chữ cho mục

    internal static class PcText
    {
        public static string Title(PcItem item)
        {
            switch (item.Kind)
            {
                case PcKind.RunValue: return S.Pc.RunValue(item.Arg1);
                case PcKind.WinlogonShell: return S.Pc.WinlogonShell;
                case PcKind.WinlogonUserinit: return S.Pc.WinlogonUserinit;
                case PcKind.AppInit: return S.Pc.AppInit;
                case PcKind.MountPoint: return S.Pc.MountPoint(item.Arg1);
                case PcKind.StartupFile: return S.Pc.StartupFile(item.Arg1);
                case PcKind.Task: return S.Pc.Task(item.Arg1);
                case PcKind.Process: return S.Pc.Process(item.Arg1);
                default: return S.Pc.Defender;
            }
        }

        public static string Severity(PcItem item)
        {
            return item.High ? S.Pc.SeverityHigh : S.Pc.SeverityMedium;
        }
    }

    // ------------------------------------------------------- danh sách kết quả

    internal sealed class FindingList : ScrollableControl
    {
        private readonly List<PcItem> _items = new List<PcItem>();

        public FindingList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoScroll = true;
            BackColor = Theme.Surface;
            TabStop = false;
            AccessibleRole = AccessibleRole.List;
        }

        public int RowHeight { get { return Ui.Px(74); } }

        public IList<PcItem> Items { get { return _items; } }

        public void SetItems(IList<PcItem> items)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            AutoScrollPosition = new Point(0, 0);
            AutoScrollMinSize = new Size(0, _items.Count * RowHeight);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            int oy = AutoScrollPosition.Y;
            int rh = RowHeight;
            int first = Math.Max(0, (-oy) / rh);
            int last = Math.Min(_items.Count - 1, (-oy + Height) / rh + 1);

            for (int i = first; i <= last; i++)
            {
                PcItem item = _items[i];
                Rectangle r = new Rectangle(0, oy + i * rh, Width, rh);

                string sev = PcText.Severity(item);
                Font chipFont = Theme.SmallStrong;
                Size sz = Ui.Measure(sev, chipFont);
                Rectangle chip = new Rectangle(0, r.Y + Ui.Px(12), sz.Width + Ui.Px(14), Ui.Px(22));
                Ui.Chip(g, sev, chipFont, chip,
                        item.High ? Theme.DangerSoft : Theme.WarnSoft,
                        item.High ? Theme.Danger : Theme.Warn);

                int x = chip.Right + Ui.Px(16);
                Ui.Text(g, PcText.Title(item), Theme.BodyStrong, Theme.Ink,
                    new Rectangle(x, r.Y + Ui.Px(12), Width - x - Ui.Px(8), Ui.Px(22)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Ui.Text(g, item.Detail, Theme.Small, Theme.InkMuted,
                    new Rectangle(x, r.Y + Ui.Px(36), Width - x - Ui.Px(8), Ui.Px(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
                if (!item.Fixable)
                    Ui.TextLeft(g, S.Pc.Manual(S.Pc.Defender), Theme.Tiny, Theme.InkFaint,
                        new Rectangle(x, r.Y + Ui.Px(56), Width - x - Ui.Px(8), Ui.Px(16)));

                Ui.HLine(g, 0, Width, r.Bottom - 1, Theme.LineSoft);
            }
        }
    }

    // ------------------------------------------------------ kết quả kiểm tra máy

    internal sealed class PcResultForm : Form
    {
        private readonly List<PcItem> _items;
        private FindingList _list;
        private FlatButton _fix;
        private Label _sub;
        private Notice _notice;
        private bool _busy;

        public PcResultForm(IList<PcItem> items)
        {            _items = new List<PcItem>(items);
            Text = S.App.Name + " — " + S.Pc.Title;
            BackColor = Theme.Surface;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            MinimizeBox = false;
            ClientSize = new Size(Ui.Px(760), Ui.Px(560));
            MinimumSize = new Size(Ui.Px(620), Ui.Px(460));

            Label title = new Label();
            title.Text = S.Pc.Title;
            title.Font = Theme.H1;
            title.ForeColor = Theme.Ink;
            title.AutoSize = false;
            title.Bounds = Ui.Rect(28, 20, 500, 34);
            Controls.Add(title);

            _sub = new Label();
            _sub.Font = Theme.Body;
            _sub.ForeColor = Theme.InkMuted;
            _sub.AutoSize = false;
            _sub.Bounds = Ui.Rect(28, 58, 600, 22);
            Controls.Add(_sub);

            _notice = new Notice();
            _notice.Bounds = new Rectangle(Ui.Px(28), Ui.Px(88),
                ClientSize.Width - Ui.Px(56), Ui.Px(38));
            Controls.Add(_notice);

            _list = new FindingList();
            _list.Bounds = new Rectangle(Ui.Px(28), Ui.Px(134),
                ClientSize.Width - Ui.Px(56), ClientSize.Height - Ui.Px(250));
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(_list);

            Label note = new Label();
            note.Text = S.Pc.FixNote;
            note.Font = Theme.Small;
            note.ForeColor = Theme.InkFaint;
            note.AutoSize = false;
            note.Bounds = new Rectangle(Ui.Px(28), ClientSize.Height - Ui.Px(100),
                ClientSize.Width - Ui.Px(340), Ui.Px(24));
            note.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(note);

            FlatButton close = new FlatButton();
            close.Text = S.Dlg.Close;
            close.Kind = ButtonKind.Secondary;
            close.Bounds = new Rectangle(ClientSize.Width - Ui.Px(28) - Ui.Px(96),
                ClientSize.Height - Ui.Px(56), Ui.Px(96), Ui.Px(Theme.ControlHeight));
            close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            _fix = new FlatButton();
            _fix.Kind = ButtonKind.Primary;
            _fix.Bounds = new Rectangle(ClientSize.Width - Ui.Px(28) - Ui.Px(96) - Ui.Px(12) - Ui.Px(160),
                ClientSize.Height - Ui.Px(56), Ui.Px(160), Ui.Px(Theme.ControlHeight));
            _fix.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _fix.Click += delegate { StartFix(); };
            Controls.Add(_fix);

            RefreshList();
            if (Fixable().Count < _items.Count)
                _notice.SetMessage(S.Pc.Manual(S.Pc.Defender), false);
        }

        private List<PcItem> Fixable()
        {
            List<PcItem> list = new List<PcItem>();
            foreach (PcItem i in _items) if (i.Fixable) list.Add(i);
            return list;
        }

        public IList<PcItem> Remaining { get { return _items; } }

        private void RefreshList()
        {
            _list.SetItems(_items);
            _sub.Text = S.Pc.Results(_items.Count);
            int fixable = Fixable().Count;
            _fix.Text = S.Pc.Fix(fixable);
            _fix.Visible = fixable > 0;
            _fix.Enabled = !_busy && fixable > 0;
        }

        private void StartFix()
        {
            List<PcItem> fixable = Fixable();
            if (fixable.Count == 0 || _busy) return;

            List<string> names = new List<string>();
            for (int i = 0; i < fixable.Count && i < 6; i++) names.Add(PcText.Title(fixable[i]));
            string body = string.Join("\n", names.ToArray());
            if (fixable.Count > names.Count) body += "\n…";

            if (!ConfirmDialog.Ask(this, S.Dlg.FixTitle(fixable.Count), S.Dlg.FixBody(body),
                                   S.Dlg.FixOk(fixable.Count), S.Dlg.Cancel, ButtonKind.Primary, true))
                return;

            _busy = true;
            _fix.Enabled = false;
            _notice.SetMessage(S.Pc.Fixing, false);
            _notice.Visible = true;

            Thread t = new Thread(delegate()
            {
                int ok, fail;
                string error = "";
                try
                {
                    PcScan.Fix(fixable, null, null, out ok, out fail);
                }
                catch (Exception e)
                {
                    ok = 0; fail = fixable.Count;
                    error = e.Message;
                    Log.Write("Lỗi khi sửa máy: " + e.Message, "ERROR");
                }
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { FinishFix(fixable, ok, fail, error); });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void FinishFix(List<PcItem> fixedItems, int ok, int fail, string error)
        {
            _busy = false;
            foreach (PcItem i in fixedItems) _items.Remove(i);

            int manual = _items.Count;
            _sub.Text = S.Pc.Results(_items.Count);
            if (_items.Count == 0)
                _notice.SetMessage(S.Pc.Done(ok, 0), false);
            else
                _notice.SetMessage(S.Pc.Done(ok, manual), manual > 0);

            if (error.Length > 0 && fail > 0 && ok == 0)
                _notice.SetMessage(S.Err.Unknown, true);

            RefreshList();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessDialogKey(keyData);
        }
    }

    // ------------------------------------------------------------- khôi phục

    internal sealed class RestoreForm : Form
    {
        private readonly ListBox _list;
        private readonly FlatButton _restore;
        private readonly Label _result;
        private List<BatchInfo> _batches = new List<BatchInfo>();

        public RestoreForm()
        {
            Text = S.App.Name + " — " + S.Restore.Title;
            BackColor = Theme.Surface;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            MinimizeBox = false;
            ClientSize = new Size(Ui.Px(680), Ui.Px(520));
            MinimumSize = new Size(Ui.Px(560), Ui.Px(440));

            Label title = new Label();
            title.Text = S.Restore.Title;
            title.Font = Theme.H1;
            title.ForeColor = Theme.Ink;
            title.AutoSize = false;
            title.Bounds = Ui.Rect(28, 20, 500, 34);
            Controls.Add(title);

            Label intro = new Label();
            intro.Text = S.Restore.Intro;
            intro.Font = Theme.Small;
            intro.ForeColor = Theme.InkMuted;
            intro.AutoSize = false;
            intro.Bounds = Ui.Rect(28, 58, ClientSize.Width - 56, 20);
            intro.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(intro);

            _list = new ListBox();
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Ui.Px(58);
            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = Theme.Surface;
            _list.ForeColor = Theme.Ink;
            _list.IntegralHeight = false;
            _list.Bounds = new Rectangle(Ui.Px(28), Ui.Px(92),
                ClientSize.Width - Ui.Px(56), ClientSize.Height - Ui.Px(238));
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _list.DrawItem += DrawBatch;
            _list.SelectedIndexChanged += delegate { UpdateButtons(); };
            _list.DoubleClick += delegate { DoRestore(); };
            _list.AccessibleName = S.Restore.Title;
            Controls.Add(_list);

            _result = new Label();
            _result.Font = Theme.Small;
            _result.ForeColor = Theme.InkMuted;
            _result.AutoSize = false;
            _result.Bounds = new Rectangle(Ui.Px(28), ClientSize.Height - Ui.Px(140),
                ClientSize.Width - Ui.Px(56), Ui.Px(36));
            _result.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(_result);

            LinkText open = new LinkText();
            open.Text = S.Restore.OpenFolder;
            open.Bounds = new Rectangle(Ui.Px(28), ClientSize.Height - Ui.Px(62), Ui.Px(200), Ui.Px(22));
            open.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            open.Click += delegate { OpenQuarantine(); };
            Controls.Add(open);

            FlatButton close = new FlatButton();
            close.Text = S.Dlg.Close;
            close.Kind = ButtonKind.Secondary;
            close.Bounds = new Rectangle(ClientSize.Width - Ui.Px(28) - Ui.Px(96),
                ClientSize.Height - Ui.Px(68), Ui.Px(96), Ui.Px(Theme.ControlHeight));
            close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            _restore = new FlatButton();
            _restore.Kind = ButtonKind.Primary;
            _restore.Text = S.Restore.Action;
            _restore.Bounds = new Rectangle(ClientSize.Width - Ui.Px(28) - Ui.Px(96) - Ui.Px(12) - Ui.Px(190),
                ClientSize.Height - Ui.Px(68), Ui.Px(190), Ui.Px(Theme.ControlHeight));
            _restore.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _restore.Click += delegate { DoRestore(); };
            Controls.Add(_restore);

            RefreshBatches();
        }

        private void RefreshBatches()
        {
            _batches = Quarantine.ListBatches();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (BatchInfo b in _batches) _list.Items.Add(b);
            _list.EndUpdate();

            if (_batches.Count == 0)
            {
                _list.Items.Add(S.Restore.Empty);
                _restore.Enabled = false;
            }
            else
            {
                _list.SelectedIndex = 0;
            }
            _result.Text = "";
            UpdateButtons();
        }

        private BatchInfo Selected()
        {
            if (_batches.Count == 0) return null;
            int i = _list.SelectedIndex;
            return (i >= 0 && i < _batches.Count) ? _batches[i] : null;
        }

        private void UpdateButtons()
        {
            _restore.Enabled = Selected() != null;
        }

        private void DrawBatch(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0) return;
            Graphics g = e.Graphics;

            Rectangle r = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Ui.Fill(g, r, selected ? Theme.AccentSoft : Theme.Surface);
            if (e.Index > 0) Ui.HLine(g, Ui.Px(12), r.Right - Ui.Px(12), r.Top, Theme.LineSoft);

            BatchInfo b = (e.Index < _batches.Count) ? _batches[e.Index] : null;
            if (b == null)
            {
                Ui.TextLeft(g, S.Restore.Empty, Theme.Body, Theme.InkMuted,
                    new Rectangle(r.X + Ui.Px(12), r.Y, r.Width - Ui.Px(24), r.Height));
                return;
            }

            Ui.TextLeft(g, b.WhenText, Theme.BodyStrong, Theme.Ink,
                new Rectangle(r.X + Ui.Px(12), r.Y + Ui.Px(9), r.Width - Ui.Px(24), Ui.Px(20)));
            Ui.TextLeft(g, S.Restore.Summary(b.Count, Ui.Size(b.Size)), Theme.Small, Theme.InkMuted,
                new Rectangle(r.X + Ui.Px(12), r.Y + Ui.Px(30), r.Width - Ui.Px(24), Ui.Px(18)));
        }

        private void DoRestore()
        {
            BatchInfo b = Selected();
            if (b == null)
            {
                _result.Text = S.Restore.NothingSelected;
                return;
            }
            if (!ConfirmDialog.Ask(this, S.Dlg.RestoreWarnTitle, S.Dlg.RestoreWarnBody,
                                   S.Dlg.RestoreOk, S.Dlg.Close, ButtonKind.Primary, false))
                return;

            int restored, skipped, missing;
            Quarantine.RestoreBatch(b.Path, out restored, out skipped, out missing);
            if (missing == 0) Quarantine.RemoveBatch(b.Path);

            string text = S.Restore.Done(restored, skipped);
            if (missing > 0) text += " " + S.Restore.Missing(missing);
            Log.Write("Khôi phục " + restored + " tệp từ " + b.Name + " (thiếu " + missing + ")");
            RefreshBatches();
            _result.Text = text;
        }

        private void OpenQuarantine()
        {
            try
            {
                Paths.EnsureDirs();
                System.Diagnostics.Process.Start("explorer.exe", "\"" + Paths.QuarDir + "\"");
            }
            catch { }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessDialogKey(keyData);
        }
    }
}
