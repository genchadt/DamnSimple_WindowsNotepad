using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// A dialog for finding and replacing text within the RichTextBox.
    /// Supports both plain text and Regex (if extended).
    /// </summary>
    public class FindReplaceDialog : Form
    {
        private readonly RichTextBox _editor;
        private readonly bool _isReplaceMode;

        // UI Controls
        private Label lblFind = new Label { Text = "Find what:", AutoSize = true };
        private TextBox txtFind = new TextBox { Width = 180 };
        private Label lblReplace = new Label { Text = "Replace with:", AutoSize = true };
        private TextBox txtReplace = new TextBox { Width = 180 };
        private Button btnFindNext = new Button { Text = "Find Next", Width = 90 };
        private Button btnReplace = new Button { Text = "Replace", Width = 90 };
        private Button btnReplaceAll = new Button { Text = "Replace All", Width = 90 };
        private Button btnCancel = new Button { Text = "Cancel", Width = 90 };
        private CheckBox chkMatchCase = new CheckBox { Text = "Match case", AutoSize = true };

        // P/Invoke for Dark Mode
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public FindReplaceDialog(RichTextBox editor, bool isReplaceMode, bool isDarkMode)
        {
            _editor = editor;
            _isReplaceMode = isReplaceMode;

            this.Text = isReplaceMode ? "Replace" : "Find";
            this.Size = new Size(410, isReplaceMode ? 210 : 160);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.AutoScaleMode = AutoScaleMode.Font; // High DPI support

            SetupLayout();
            ApplyTheme(isDarkMode);
            WireEvents();
        }

        private void SetupLayout()
        {
            lblFind.Location = new Point(10, 15);
            txtFind.Location = new Point(100, 12);
            btnFindNext.Location = new Point(295, 10);
            btnCancel.Location = new Point(295, 40);
            chkMatchCase.Location = new Point(10, _isReplaceMode ? 140 : 90);

            this.Controls.AddRange(new Control[] { lblFind, txtFind, btnFindNext, btnCancel, chkMatchCase });

            if (_isReplaceMode)
            {
                lblReplace.Location = new Point(10, 45);
                txtReplace.Location = new Point(100, 42);
                btnReplace.Location = new Point(295, 75);
                btnReplaceAll.Location = new Point(295, 105);
                this.Controls.AddRange(new Control[] { lblReplace, txtReplace, btnReplace, btnReplaceAll });
            }

            this.AcceptButton = btnFindNext;
            this.CancelButton = btnCancel;
        }

        private void WireEvents()
        {
            btnFindNext.Click += (s, e) => FindNext();
            btnReplace.Click += (s, e) => Replace();
            btnReplaceAll.Click += (s, e) => ReplaceAll();
            btnCancel.Click += (s, e) => this.Close();
        }

        private void ApplyTheme(bool dark)
        {
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            Color back = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            Color fore = dark ? Color.White : SystemColors.ControlText;
            Color inputBack = dark ? Color.FromArgb(30, 30, 30) : Color.White;

            this.BackColor = back;

            foreach (Control c in this.Controls)
            {
                c.ForeColor = fore;

                if (c is TextBox t)
                {
                    t.BackColor = inputBack;
                    t.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                }

                if (c is Button b)
                {
                    b.BackColor = dark ? Color.FromArgb(63, 63, 70) : SystemColors.ButtonFace;
                    b.FlatStyle = FlatStyle.Flat;
                }
            }
        }

        private void FindNext()
        {
            string find = txtFind.Text;
            if (string.IsNullOrEmpty(find)) return;

            RichTextBoxFinds options = RichTextBoxFinds.None;
            if (chkMatchCase.Checked) options |= RichTextBoxFinds.MatchCase;

            // Start search from current cursor position
            int startPos = _editor.SelectionStart + _editor.SelectionLength;
            int index = _editor.Find(find, startPos, options);

            // Wrap around if not found
            if (index == -1) index = _editor.Find(find, 0, options);

            if (index != -1)
            {
                _editor.Focus();
                _editor.Select(index, find.Length);
            }
            else
            {
                MessageBox.Show($"Cannot find \"{find}\"", "Notepad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Replace()
        {
            // Only replace if the currently selected text matches the find query
            if (_editor.SelectedText.Equals(txtFind.Text, chkMatchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                _editor.SelectedText = txtReplace.Text;
            }
            FindNext();
        }

        private void ReplaceAll()
        {
            string find = txtFind.Text;
            string replace = txtReplace.Text;
            if (string.IsNullOrEmpty(find)) return;

            if (chkMatchCase.Checked)
            {
                _editor.Text = _editor.Text.Replace(find, replace);
            }
            else
            {
                _editor.Text = Regex.Replace(_editor.Text, Regex.Escape(find), replace, RegexOptions.IgnoreCase);
            }
        }
    }
}