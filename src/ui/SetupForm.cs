// ShieldGate - thiết lập lần đầu: chọn thư mục dữ liệu và ngôn ngữ.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShieldGate
{
    internal sealed class SetupForm : Form
    {
        private readonly Label _title;
        private readonly Label _intro;
        private readonly Label _dataLabel;
        private readonly Label _dataHint;
        private readonly Label _languageLabel;
        private readonly Label _error;
        private readonly TextBox _home;
        private readonly FlatButton _browse;
        private readonly FlatButton _continue;
        private readonly RadioButton _vi;
        private readonly RadioButton _en;

        public string SelectedHome { get; private set; }
        public string SelectedLanguage { get; private set; }

        public SetupForm()
        {
            SelectedHome = "";
            SelectedLanguage = S.English ? Bootstrap.LanguageEn : Bootstrap.LanguageVi;

            Text = S.Setup.Title;
            BackColor = Theme.Surface;
            Font = Theme.Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.Px(600), Ui.Px(440));
            KeyPreview = true;

            _title = new Label();
            _title.Font = Theme.H1;
            _title.ForeColor = Theme.Ink;
            _title.AutoSize = false;
            _title.Bounds = Ui.Rect(32, 26, 536, 34);
            Controls.Add(_title);

            _intro = new Label();
            _intro.Font = Theme.Body;
            _intro.ForeColor = Theme.InkMuted;
            _intro.AutoSize = false;
            _intro.TextAlign = ContentAlignment.TopLeft;
            _intro.Bounds = Ui.Rect(32, 68, 536, 48);
            Controls.Add(_intro);

            _dataLabel = new Label();
            _dataLabel.Font = Theme.Section;
            _dataLabel.ForeColor = Theme.Ink;
            _dataLabel.AutoSize = false;
            _dataLabel.Bounds = Ui.Rect(32, 138, 300, 20);
            Controls.Add(_dataLabel);

            _home = new TextBox();
            _home.Font = Theme.Body;
            _home.BorderStyle = BorderStyle.FixedSingle;
            _home.Bounds = Ui.Rect(32, 164, 404, 30);
            _home.Text = Bootstrap.DefaultHome;
            Controls.Add(_home);

            _browse = new FlatButton();
            _browse.Kind = ButtonKind.Secondary;
            _browse.Bounds = Ui.Rect(448, 161, 120, 36);
            _browse.Click += delegate { Browse(); };
            Controls.Add(_browse);

            _dataHint = new Label();
            _dataHint.Font = Theme.Small;
            _dataHint.ForeColor = Theme.InkFaint;
            _dataHint.AutoSize = false;
            _dataHint.TextAlign = ContentAlignment.TopLeft;
            _dataHint.Bounds = Ui.Rect(32, 200, 536, 36);
            Controls.Add(_dataHint);

            _languageLabel = new Label();
            _languageLabel.Font = Theme.Section;
            _languageLabel.ForeColor = Theme.Ink;
            _languageLabel.AutoSize = false;
            _languageLabel.Bounds = Ui.Rect(32, 248, 200, 20);
            Controls.Add(_languageLabel);

            _vi = new RadioButton();
            _vi.Font = Theme.Body;
            _vi.ForeColor = Theme.Ink;
            _vi.Text = S.Setup.LanguageVi;
            _vi.Bounds = Ui.Rect(32, 274, 150, 24);
            _vi.Checked = !S.English;
            Controls.Add(_vi);

            _en = new RadioButton();
            _en.Font = Theme.Body;
            _en.ForeColor = Theme.Ink;
            _en.Text = S.Setup.LanguageEn;
            _en.Bounds = Ui.Rect(190, 274, 150, 24);
            _en.Checked = S.English;
            Controls.Add(_en);

            _vi.CheckedChanged += delegate
            {
                if (_vi.Checked) { S.SetLanguage(Bootstrap.LanguageVi); Relocalize(); }
            };
            _en.CheckedChanged += delegate
            {
                if (_en.Checked) { S.SetLanguage(Bootstrap.LanguageEn); Relocalize(); }
            };

            _error = new Label();
            _error.Font = Theme.Small;
            _error.ForeColor = Theme.Danger;
            _error.AutoSize = false;
            _error.TextAlign = ContentAlignment.TopLeft;
            _error.Bounds = Ui.Rect(32, 312, 536, 44);
            _error.Visible = false;
            Controls.Add(_error);

            _continue = new FlatButton();
            _continue.Kind = ButtonKind.Primary;
            // ClientSize đã nhân tỉ lệ DPI, nên chỉ trừ phần lề đã nhân; không bọc Ui.Rect nữa.
            _continue.Bounds = new Rectangle(
                ClientSize.Width - Ui.Px(32) - Ui.Px(150),
                ClientSize.Height - Ui.Px(28) - Ui.Px(Theme.ControlHeight),
                Ui.Px(150), Ui.Px(Theme.ControlHeight));
            _continue.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _continue.Click += delegate { Accept(); };
            Controls.Add(_continue);

            Relocalize();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                Accept();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private void Relocalize()
        {
            Text = S.Setup.Title;
            _title.Text = S.Setup.Welcome;
            _intro.Text = S.Setup.Intro;
            _dataLabel.Text = S.Setup.DataLabel;
            _dataHint.Text = S.Setup.DataHint;
            _languageLabel.Text = S.Setup.LanguageLabel;
            _browse.Text = S.Setup.Browse;
            _continue.Text = S.Setup.Continue;
            _error.Visible = false;
        }

        private void Browse()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = S.Setup.ChooseFolder;
                dialog.ShowNewFolderButton = true;
                try
                {
                    string current = _home.Text.Trim();
                    if (current.Length > 0 && Directory.Exists(current)) dialog.SelectedPath = current;
                }
                catch { }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _home.Text = dialog.SelectedPath;
                    _error.Visible = false;
                }
            }
        }

        private void Accept()
        {
            string path = _home.Text.Trim().Trim('"');
            if (path.Length == 0) path = Bootstrap.DefaultHome;

            try
            {
                path = Environment.ExpandEnvironmentVariables(path);
                path = Path.GetFullPath(path);
            }
            catch
            {
                ShowError();
                return;
            }

            string root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root)
                && string.Equals(path.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                ShowError();
                return;
            }

            if (!Bootstrap.CanWrite(path))
            {
                ShowError();
                return;
            }

            SelectedHome = path;
            SelectedLanguage = _en.Checked ? Bootstrap.LanguageEn : Bootstrap.LanguageVi;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowError()
        {
            _error.Text = S.Setup.InvalidPath;
            _error.Visible = true;
        }
    }
}
