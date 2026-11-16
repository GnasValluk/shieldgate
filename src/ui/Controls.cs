// ShieldGate - điều hướng, chân trang, danh sách có hộp chọn, công tắc.
// Tất cả là control thật: tab được, bấm phím được, có tên cho trình đọc màn hình.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShieldGate
{
    internal enum PageId { Overview = 0, Usb = 1, Protect = 2, Log = 3, About = 4 }

    // ------------------------------------------------------------ điều hướng

    internal sealed class NavButton : Control
    {
        private bool _active;
        private bool _hot;
        private string _icon = "grid";

        public NavButton(string text, string icon)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            _icon = icon;
            Font = Theme.Body;
            Height = Ui.Px(Theme.NavHeight);
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Theme.Canvas;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = text;
        }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                _active = value;
                Font = value ? Theme.BodyStrong : Theme.Body;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Canvas);
            Rectangle r = new Rectangle(0, 0, Width, Height);

            if (_active) Ui.RoundFill(g, r, Ui.Px(3), Theme.Surface);
            else if (_hot) Ui.RoundFill(g, r, Ui.Px(3), Theme.Hover);

            if (_active)
            {
                Rectangle bar = new Rectangle(0, Ui.Px(10), Ui.Px(2), Height - Ui.Px(20));
                Ui.Fill(g, bar, Theme.Accent);
            }

            Color fore = _active ? Theme.Ink : Theme.InkMuted;
            int iconSize = Ui.Px(18);
            Icons.Draw(g, _icon, new Rectangle(Ui.Px(12), (Height - iconSize) / 2, iconSize, iconSize),
                       _active ? Theme.Accent : Theme.InkMuted);
            Ui.TextLeft(g, Text, Font, fore,
                new Rectangle(Ui.Px(40), 0, Width - Ui.Px(40), Height));

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -Ui.Px(3), -Ui.Px(3)),
                    Theme.Ink, _active ? Theme.Surface : Theme.Canvas);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
                OnClick(EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            return (keyData & Keys.KeyCode) == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter || keyData == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }

    internal sealed class RailPanel : Panel
    {
        private readonly List<NavButton> _buttons = new List<NavButton>();
        private readonly List<PageId> _ids = new List<PageId>();
        private readonly NavButton _about;
        private int _selected;
        private PageId _selectedPage = PageId.Overview;

        public RailPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Canvas;
            Width = Ui.Px(Theme.RailWidth);

            Add("grid", S.Nav.Overview, PageId.Overview);
            Add("usb", S.Nav.Usb, PageId.Usb);
            Add("shield", S.Nav.Protect, PageId.Protect);
            Add("list", S.Nav.Log, PageId.Log);

            _about = new NavButton(S.Nav.About, "info");
            _about.Click += delegate { Select(PageId.About); };
            Controls.Add(_about);

            _selected = 0;
            _buttons[0].Active = true;
        }

        private void Add(string icon, string label, PageId id)
        {
            NavButton b = new NavButton(label, icon);
            b.Click += delegate { Select(id); };
            _buttons.Add(b);
            _ids.Add(id);
            Controls.Add(b);
        }

        public PageId Selected { get { return _selectedPage; } }

        public event EventHandler Navigate;

        public void Select(PageId id)
        {
            if (id == PageId.About)
            {
                for (int i = 0; i < _buttons.Count; i++) _buttons[i].Active = false;
                _about.Active = true;
                _selected = -1;
            }
            else
            {
                int idx = _ids.IndexOf(id);
                if (idx < 0) return;
                for (int i = 0; i < _buttons.Count; i++) _buttons[i].Active = i == idx;
                _about.Active = false;
                _selected = idx;
            }
            _selectedPage = id;
            if (Navigate != null) Navigate(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_about == null) return;
            int y = Ui.Px(96);
            int w = Width - Ui.Px(32);
            foreach (NavButton b in _buttons)
            {
                b.Bounds = new Rectangle(Ui.Px(16), y, w, Ui.Px(Theme.NavHeight));
                y += Ui.Px(Theme.NavHeight + 2);
            }
            _about.Bounds = new Rectangle(Ui.Px(16), Height - Ui.Px(92), w, Ui.Px(Theme.NavHeight));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Canvas);
            Ui.HLine(g, Width - 1, Width, 0, Theme.Line);
            Ui.Fill(g, new Rectangle(Width - 1, 0, 1, Height), Theme.Line);

            int logo = Ui.Px(26);
            Icons.DrawLogo(g, new Rectangle(Ui.Px(24), Ui.Px(24), logo, logo), Theme.Accent);
            Ui.TextLeft(g, S.App.Name, Theme.Brand, Theme.Ink,
                new Rectangle(Ui.Px(58), Ui.Px(20), Width - Ui.Px(66), Ui.Px(24)));
            Ui.Text(g, S.App.Tagline, Theme.Tiny, Theme.InkMuted,
                new Rectangle(Ui.Px(58), Ui.Px(45), Width - Ui.Px(66), Ui.Px(30)),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);

            int sepY = Height - Ui.Px(100);
            Ui.HLine(g, Ui.Px(16), Width - Ui.Px(16), sepY, Theme.Line);
        }
    }

    // ------------------------------------------------------------- chân trang

    internal sealed class FooterBar : Control
    {
        public FooterBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Height = Ui.Px(44);
            TabStop = false;
            AccessibleRole = AccessibleRole.StaticText;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            int pad = Ui.Px(Theme.PagePad);
            Ui.HLine(g, pad, Width - pad, 0, Theme.LineSoft);
            Ui.TextLeft(g, S.Footer.Line(AppInfo.Version), Theme.Tiny, Theme.InkFaint,
                new Rectangle(pad, 0, Width - pad * 2, Height));
        }
    }

    // ---------------------------------------------------- danh sách có hộp chọn

    internal sealed class UsbListControl : ScrollableControl
    {
        private sealed class Row
        {
            public UsbItem Item;
            public bool Checked = true;
        }

        private readonly List<Row> _rows = new List<Row>();
        private int _focus = -1;
        private int _hot = -1;
        private string _empty = "";

        public UsbListControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoScroll = true;
            BackColor = Theme.Surface;
            TabStop = true;
            AccessibleRole = AccessibleRole.List;
            AccessibleName = S.Usb.ListName;
        }

        public int RowHeight { get { return Ui.Px(70); } }

        public string EmptyText
        {
            get { return _empty; }
            set { _empty = value ?? ""; Invalidate(); }
        }

        public void SetItems(IList<UsbItem> items)
        {
            _rows.Clear();
            if (items != null)
            {
                foreach (UsbItem i in items)
                {
                    Row r = new Row();
                    r.Item = i;
                    r.Checked = true;
                    _rows.Add(r);
                }
            }
            _focus = _rows.Count > 0 ? 0 : -1;
            AutoScrollPosition = new Point(0, 0);
            UpdateScroll();
            Invalidate();
        }

        public void Clear()
        {
            SetItems(null);
        }

        private void UpdateScroll()
        {
            AutoScrollMinSize = new Size(0, _rows.Count * RowHeight);
        }

        public int ItemCount { get { return _rows.Count; } }

        public int CheckedCount
        {
            get
            {
                int n = 0;
                foreach (Row r in _rows) if (r.Checked) n++;
                return n;
            }
        }

        public List<UsbItem> CheckedItems()
        {
            List<UsbItem> list = new List<UsbItem>();
            foreach (Row r in _rows) if (r.Checked) list.Add(r.Item);
            return list;
        }

        public void SetAll(bool value)
        {
            foreach (Row r in _rows) r.Checked = value;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public event EventHandler SelectionChanged;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);

            if (_rows.Count == 0)
            {
                if (_empty.Length > 0)
                    Ui.Text(g, _empty, Theme.Body, Theme.InkMuted,
                        new Rectangle(Ui.Px(4), Ui.Px(20), Width - Ui.Px(8), Ui.Px(40)),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                return;
            }

            int oy = AutoScrollPosition.Y;
            int rh = RowHeight;
            int first = Math.Max(0, (-oy) / rh);
            int last = Math.Min(_rows.Count - 1, (-oy + Height) / rh + 1);

            for (int i = first; i <= last; i++)
            {
                Row row = _rows[i];
                Rectangle r = new Rectangle(0, oy + i * rh, Width, rh);

                if (i == _hot) Ui.Fill(g, new Rectangle(0, r.Y, Width, rh), Theme.HoverSoft);
                if (Focused && i == _focus)
                {
                    using (Pen pen = new Pen(Theme.LineSoft, 1))
                        g.DrawRectangle(pen, new Rectangle(1, r.Y + 1, Width - 3, rh - 3));
                }

                DrawCheck(g, new Rectangle(Ui.Px(4), r.Y + (rh - Ui.Px(16)) / 2, Ui.Px(16), Ui.Px(16)),
                          row.Checked);

                int textX = Ui.Px(32);
                int right = Width - Ui.Px(12);
                int sizeW = Ui.Px(72);
                Ui.Text(g, row.Item.Rel,
                    Theme.BodyStrong, Theme.Ink,
                    new Rectangle(textX, r.Y + Ui.Px(12), right - textX - sizeW, Ui.Px(22)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
                Ui.Text(g, row.Item.ReasonText, Theme.Small, Theme.InkMuted,
                    new Rectangle(textX, r.Y + Ui.Px(36), right - textX - sizeW, Ui.Px(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Ui.TextRight(g, Ui.Size(row.Item.Size), Theme.Small, Theme.InkFaint,
                    new Rectangle(right - sizeW, r.Y + Ui.Px(12), sizeW, Ui.Px(22)));

                Ui.HLine(g, 0, Width, r.Bottom - 1, Theme.LineSoft);
            }
        }

        private void DrawCheck(Graphics g, Rectangle box, bool on)
        {
            if (on)
            {
                Ui.RoundFill(g, box, Ui.Px(3), Theme.Accent);
                Icons.Draw(g, "check", Rectangle.Inflate(box, -Ui.Px(3), -Ui.Px(3)), Color.White);
            }
            else
            {
                Ui.RoundFill(g, box, Ui.Px(3), Theme.Surface);
                Ui.RoundStroke(g, box, Ui.Px(3), Theme.BorderStrong, 1f);
            }
        }

        private int HitRow(Point p)
        {
            int idx = (p.Y - AutoScrollPosition.Y) / RowHeight;
            return (idx >= 0 && idx < _rows.Count) ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitRow(e.Location);
            if (idx != _hot)
            {
                _hot = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Focus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = HitRow(e.Location);
            if (idx < 0) return;
            _rows[idx].Checked = !_rows[idx].Checked;
            _focus = idx;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Up || k == Keys.Down || k == Keys.Space ||
                k == Keys.Home || k == Keys.End || k == Keys.A) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_rows.Count == 0) return;
            if (e.Control && e.KeyCode == Keys.A)
            {
                bool anyOff = false;
                foreach (Row r in _rows) if (!r.Checked) { anyOff = true; break; }
                SetAll(anyOff);
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Up) { MoveFocus(_focus - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { MoveFocus(_focus + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Home) { MoveFocus(0); e.Handled = true; }
            else if (e.KeyCode == Keys.End) { MoveFocus(_rows.Count - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Space)
            {
                if (_focus >= 0 && _focus < _rows.Count)
                {
                    _rows[_focus].Checked = !_rows[_focus].Checked;
                    Invalidate();
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
        }

        private void MoveFocus(int index)
        {
            if (_rows.Count == 0) return;
            int v = Math.Max(0, Math.Min(_rows.Count - 1, index));
            _focus = v;
            int top = -AutoScrollPosition.Y;
            int want = v * RowHeight;
            if (want < top) AutoScrollPosition = new Point(0, want);
            else if (want + RowHeight > top + Height)
                AutoScrollPosition = new Point(0, want + RowHeight - Height);
            Invalidate();
        }
    }

    // ------------------------------------------------------------- công tắc

    internal sealed class SwitchRow : Control
    {
        private bool _checked;
        private bool _hot;
        private string _detail = "";

        public SwitchRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Font = Theme.Body;
            Height = Ui.Px(76);
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public string Detail
        {
            get { return _detail; }
            set { _detail = value ?? ""; AccessibleDescription = _detail; Invalidate(); }
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler CheckedChanged;

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            Rectangle r = ClientRectangle;

            if (_hot && Enabled) Ui.Fill(g, r, Theme.HoverSoft);

            Color title = Enabled ? Theme.Ink : Theme.InkFaint;
            Color detailColor = Enabled ? Theme.InkMuted : Theme.InkFaint;

            int trackW = Ui.Px(38), trackH = Ui.Px(20);
            int trackX = Width - trackW - Ui.Px(4);
            int trackY = (Height - trackH) / 2;
            Rectangle track = new Rectangle(trackX, trackY, trackW, trackH);

            Color trackColor;
            if (!Enabled) trackColor = Theme.Line;
            else if (_checked) trackColor = _hot ? Theme.AccentHover : Theme.Accent;
            else trackColor = _hot ? Theme.Disabled : Theme.BorderStrong;

            Ui.RoundFill(g, track, trackH / 2, trackColor);
            int knob = trackH - Ui.Px(4);
            int knobX = _checked ? track.Right - knob - Ui.Px(2) : track.X + Ui.Px(2);
            using (SolidBrush b = new SolidBrush(Theme.Surface))
                g.FillEllipse(b, knobX, trackY + Ui.Px(2), knob, knob);

            Ui.Text(g, Text, Theme.BodyStrong, title,
                new Rectangle(0, Ui.Px(12), trackX - Ui.Px(16), Ui.Px(22)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Ui.Text(g, _detail, Theme.Small, detailColor,
                new Rectangle(0, Ui.Px(34), Width - Ui.Px(8), Ui.Px(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            Ui.HLine(g, 0, Width, Height - 1, Theme.LineSoft);

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(Ui.Px(2), Ui.Px(2),
                    Width - Ui.Px(4), Height - Ui.Px(5)), Theme.Ink, Theme.Surface);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Enabled) return;
            _hot = true;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            if (ClientRectangle.Contains(e.Location)) Checked = !Checked;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Space || k == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }
    }

    // --------------------------------------------------------- hộp chọn

    internal sealed class SelectBox : Control
    {
        private string[] _items = new string[0];
        private int _selected = -1;
        private bool _hot;
        private ContextMenuStrip _menu;

        public SelectBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Surface;
            Font = Theme.Body;
            Height = Ui.Px(30);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ComboBox;
        }

        public string[] Items
        {
            get { return _items; }
            set
            {
                _items = value ?? new string[0];
                if (_selected >= _items.Length) _selected = _items.Length - 1;
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int v = _items.Length == 0 ? -1 : Math.Max(0, Math.Min(_items.Length - 1, value));
                if (v == _selected) return;
                _selected = v;
                AccessibleName = v >= 0 ? _items[v] : "";
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler SelectedChanged;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Surface : Parent.BackColor);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.RoundFill(g, r, Ui.Px(3), Theme.Surface);
            Ui.RoundStroke(g, r, Ui.Px(3),
                _hot || Focused ? Theme.InkFaint : Theme.BorderStrong, 1f);

            string text = _selected >= 0 && _selected < _items.Length ? _items[_selected] : "";
            Ui.TextLeft(g, text, Font, Theme.Ink,
                new Rectangle(Ui.Px(10), 0, Width - Ui.Px(32), Height));

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Theme.InkMuted, 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int cx = Width - Ui.Px(16);
                int cy = Height / 2 - Ui.Px(1);
                g.DrawLine(pen, cx - Ui.Px(4), cy - Ui.Px(2), cx, cy + Ui.Px(2));
                g.DrawLine(pen, cx, cy + Ui.Px(2), cx + Ui.Px(4), cy - Ui.Px(2));
            }

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(Ui.Px(2), Ui.Px(2),
                    Width - Ui.Px(4), Height - Ui.Px(4)), Theme.Ink, Theme.Surface);
        }

        private void OpenMenu()
        {
            if (_items.Length == 0) return;
            CloseMenu();

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.Font = Theme.Body;
            _menu.BackColor = Theme.Surface;
            _menu.MinimumSize = new Size(Width, 0);
            for (int i = 0; i < _items.Length; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.Checked = i == _selected;
                item.Click += delegate { SelectedIndex = index; };
                _menu.Items.Add(item);
            }
            _menu.Closed += delegate { _menu = null; Invalidate(); };
            _menu.Show(this, new Point(0, Height));
        }

        private void CloseMenu()
        {
            if (_menu != null)
            {
                ContextMenuStrip old = _menu;
                _menu = null;
                old.Close();
                old.Dispose();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            OpenMenu();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Up || k == Keys.Down || k == Keys.Space || k == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up) { SelectedIndex = _selected - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { SelectedIndex = _selected + 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OpenMenu();
                e.Handled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) CloseMenu();
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------- danh sách ổ USB

    internal sealed class DriveRow
    {
        public string Drive;
        public string Title;
        public string Sub;
        public bool Attention;
        public string Chip;
        public Color ChipColor = Theme.Safe;
        public Color ChipBack = Theme.SafeSoft;
    }

    internal sealed class DriveList : Control
    {
        private readonly List<DriveRow> _rows = new List<DriveRow>();
        private int _hot = -1;

        public DriveList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            TabStop = false;
            AccessibleRole = AccessibleRole.List;
        }

        public int RowHeight { get { return Ui.Px(Theme.RowHeight); } }

        public IList<DriveRow> Rows { get { return _rows; } }

        public event Action<string> RowClicked;

        public void SetRows(IList<DriveRow> rows)
        {
            _rows.Clear();
            if (rows != null) _rows.AddRange(rows);
            Height = Math.Max(0, _rows.Count * RowHeight);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            for (int i = 0; i < _rows.Count; i++)
            {
                DriveRow row = _rows[i];
                Rectangle r = new Rectangle(0, i * RowHeight, Width, RowHeight);
                if (i == _hot) Ui.Fill(g, new Rectangle(0, r.Y, Width, r.Height), Theme.HoverSoft);

                int iconSize = Ui.Px(18);
                Icons.Draw(g, "usb", new Rectangle(0, r.Y + (r.Height - iconSize) / 2, iconSize, iconSize),
                           row.Attention ? Theme.Warn : Theme.InkMuted);
                Ui.TextLeft(g, row.Title, Theme.BodyStrong, Theme.Ink,
                    new Rectangle(Ui.Px(28), r.Y + Ui.Px(9), Width - Ui.Px(190), Ui.Px(20)));
                Ui.TextLeft(g, row.Sub, Theme.Small, Theme.InkMuted,
                    new Rectangle(Ui.Px(28), r.Y + Ui.Px(29), Width - Ui.Px(190), Ui.Px(18)));

                Font chipFont = Theme.SmallStrong;
                Size sz = Ui.Measure(row.Chip, chipFont);
                int chipW = sz.Width + Ui.Px(16);
                Rectangle chip = new Rectangle(Width - Ui.Px(26) - chipW, r.Y + Ui.Px(17), chipW, Ui.Px(22));
                Ui.Chip(g, row.Chip, chipFont, chip, row.ChipBack, row.ChipColor);

                Icons.Draw(g, "chevron", new Rectangle(Width - Ui.Px(16), r.Y + Ui.Px(21), Ui.Px(14), Ui.Px(14)),
                           i == _hot ? Theme.Accent : Theme.InkFaint);

                if (i < _rows.Count - 1) Ui.HLine(g, 0, Width, r.Bottom - 1, Theme.LineSoft);
            }
        }

        private int HitRow(Point p)
        {
            int idx = p.Y / RowHeight;
            return (idx >= 0 && idx < _rows.Count) ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitRow(e.Location);
            if (idx != _hot)
            {
                _hot = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = HitRow(e.Location);
            if (idx >= 0 && RowClicked != null) RowClicked(_rows[idx].Drive);
        }
    }

    // --------------------------------------------------------------- thông báo

    internal sealed class Notice : Label
    {
        public Notice()
        {
            AutoSize = false;
            Font = Theme.Small;
            BackColor = Theme.DangerSoft;
            ForeColor = Theme.Danger;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(Ui.Px(10), 0, Ui.Px(10), 0);
            Height = Ui.Px(38);
            Visible = false;
            AccessibleRole = AccessibleRole.StaticText;
        }

        public void SetMessage(string text, bool isError)
        {
            Text = text ?? "";
            BackColor = isError ? Theme.DangerSoft : Theme.AccentSoft;
            ForeColor = isError ? Theme.Danger : Theme.Accent;
            Visible = Text.Length > 0;
        }

        public void ClearMessage()
        {
            Visible = false;
            Text = "";
        }
    }
}
