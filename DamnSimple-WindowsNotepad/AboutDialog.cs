using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    public class AboutDialog : Form
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public AboutDialog(bool isDarkMode)
        {
            this.Text = "About Notepad";
            this.Size = new Size(400, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblTitle = new Label
            {
                Text = "Windows XP Notepad Clone",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            Label lblDetails = new Label
            {
                Text = "Version 1.0\n© 2025 Your Name\n\nThis product is licensed under the terms of the \nMIT License for open-source software development.",
                Location = new Point(20, 60),
                AutoSize = true
            };

            Button btnOk = new Button
            {
                Text = "OK",
                Location = new Point(300, 170),
                Width = 75,
                DialogResult = DialogResult.OK
            };

            this.Controls.AddRange(new Control[] { lblTitle, lblDetails, btnOk });
            this.AcceptButton = btnOk;

            ApplyTheme(isDarkMode);
        }

        private void ApplyTheme(bool dark)
        {
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));

            this.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            foreach (Control c in this.Controls)
            {
                c.ForeColor = dark ? Color.White : SystemColors.ControlText;
                if (c is Button b)
                {
                    b.BackColor = dark ? Color.FromArgb(63, 63, 70) : SystemColors.ButtonFace;
                    b.FlatStyle = FlatStyle.Flat;
                }
            }
        }
    }
}