using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    public class GoToDialog : Form
    {
        private TextBox _editor;

        // UI Controls
        private Label lblLine = new Label { Text = "Line number:", Location = new Point(10, 10), AutoSize = true };
        private TextBox txtLine = new TextBox { Location = new Point(10, 30), Width = 210 };
        private Button btnGo = new Button { Text = "Go To", Location = new Point(55, 65), Width = 80 };
        private Button btnCancel = new Button { Text = "Cancel", Location = new Point(140, 65), Width = 80 };

        // P/Invoke to handle Dark Mode title bar
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public GoToDialog(TextBox editor, bool isDarkMode)
        {
            _editor = editor;

            // Form Setup
            this.Text = "Go To Line";
            this.Size = new Size(250, 140);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // Add Controls
            this.Controls.AddRange(new Control[] { lblLine, txtLine, btnGo, btnCancel });

            // Set Default Actions
            this.AcceptButton = btnGo;
            this.CancelButton = btnCancel;

            // Events
            btnGo.Click += (s, e) => HandleGo();
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            // Apply Theme
            ApplyTheme(isDarkMode);
        }

        private void ApplyTheme(bool dark)
        {
            // 1. Force Title Bar Theme
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            // 2. Control Colors
            Color backColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            Color foreColor = dark ? Color.White : SystemColors.ControlText;
            Color inputBack = dark ? Color.FromArgb(30, 30, 30) : Color.White;

            this.BackColor = backColor;

            lblLine.ForeColor = foreColor;

            txtLine.BackColor = inputBack;
            txtLine.ForeColor = foreColor;
            txtLine.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;

            btnGo.BackColor = dark ? Color.FromArgb(63, 63, 70) : SystemColors.ButtonFace;
            btnGo.ForeColor = foreColor;
            btnGo.FlatStyle = FlatStyle.Flat;

            btnCancel.BackColor = dark ? Color.FromArgb(63, 63, 70) : SystemColors.ButtonFace;
            btnCancel.ForeColor = foreColor;
            btnCancel.FlatStyle = FlatStyle.Flat;
        }

        private void HandleGo()
        {
            if (int.TryParse(txtLine.Text, out _))
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid number.", "Notepad - Goto Line");
            }
        }

        // Property to pass the result back to the main form
        public int LineNumber => int.TryParse(txtLine.Text, out int n) ? n : -1;
    }
}