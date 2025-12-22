using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DamnSimple_WindowsNotepad
{
    public class NotepadForm : Form
    {
        private CustomTextBox txtContent = null!;
        private MenuStrip menuStrip = null!;
        private StatusStrip statusStrip = null!;
        private ToolStripStatusLabel lblStatus = null!;
        private System.ComponentModel.IContainer? components = null;

        private string? currentFilePath = null;
        private bool isDirty = false;

        // P/Invoke for Dark Mode Title Bar and Scrollbars
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

        public NotepadForm()
        {
            InitializeComponent();
            ApplyTheme();

            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General) ApplyTheme();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.Text = "Untitled - Notepad";
            this.Size = new Size(800, 600);
            this.MinimumSize = new Size(400, 300);
            this.Icon = SystemIcons.Application;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += NotepadForm_FormClosing;

            menuStrip = new MenuStrip();

            // --- File Menu ---
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&New", null, (s, e) => FileNew()) { ShortcutKeys = Keys.Control | Keys.N });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Open...", null, (s, e) => FileOpen()) { ShortcutKeys = Keys.Control | Keys.O });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Save", null, (s, e) => FileSave()) { ShortcutKeys = Keys.Control | Keys.S });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save &As...", null, (s, e) => FileSaveAs()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (s, e) => Close()));

            // --- Edit Menu ---
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Undo", null, (s, e) => txtContent.Undo()) { ShortcutKeys = Keys.Control | Keys.Z });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Cu&t", null, (s, e) => txtContent.Cut()) { ShortcutKeys = Keys.Control | Keys.X });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Copy", null, (s, e) => txtContent.Copy()) { ShortcutKeys = Keys.Control | Keys.C });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Paste", null, (s, e) => txtContent.Paste()) { ShortcutKeys = Keys.Control | Keys.V });
            editMenu.DropDownItems.Add(new ToolStripSeparator());

            // Integrated Find and Go To
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Find...", null, (s, e) => ShowFindReplace(false)) { ShortcutKeys = Keys.Control | Keys.F });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Replace...", null, (s, e) => ShowFindReplace(true)) { ShortcutKeys = Keys.Control | Keys.H });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Go &To...", null, (s, e) => ShowGoTo()) { ShortcutKeys = Keys.Control | Keys.G });

            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Select &All", null, (s, e) => txtContent.SelectAll()) { ShortcutKeys = Keys.Control | Keys.A });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Time/&Date", null, (s, e) => txtContent.AppendText(DateTime.Now.ToString())) { ShortcutKeys = Keys.F5 });

            // --- Format Menu ---
            var formatMenu = new ToolStripMenuItem("&Format");
            var wordWrapItem = new ToolStripMenuItem("&Word Wrap", null, (s, e) => ToggleWordWrap()) { Checked = true, CheckOnClick = true };
            formatMenu.DropDownItems.Add(wordWrapItem);
            formatMenu.DropDownItems.Add(new ToolStripMenuItem("&Font...", null, (s, e) => ChangeFont()));

            // --- View Menu ---
            var viewMenu = new ToolStripMenuItem("&View");
            var statusItem = new ToolStripMenuItem("&Status Bar", null, (s, e) => ToggleStatusBar()) { CheckOnClick = true };
            viewMenu.DropDownItems.Add(statusItem);

            // --- Help Menu ---
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&About", null, (s, e) => ShowAbout()));

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, formatMenu, viewMenu, helpMenu });

            // --- Editor Control ---
            txtContent = new CustomTextBox();
            txtContent.Multiline = true;
            txtContent.Dock = DockStyle.Fill;
            txtContent.ScrollBars = ScrollBars.Vertical;
            txtContent.Font = new Font("Lucida Console", 10f);
            txtContent.BorderStyle = BorderStyle.None;
            txtContent.WordWrap = true;
            txtContent.TextChanged += (s, e) => { isDirty = true; UpdateStatusBar(); };
            txtContent.KeyUp += (s, e) => UpdateStatusBar();
            txtContent.MouseUp += (s, e) => UpdateStatusBar();
            txtContent.HandleCreatedAction = () => ApplyTheme();

            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel("Ln 1, Col 1");
            statusStrip.Items.Add(lblStatus);
            statusStrip.Visible = false;

            this.Controls.Add(txtContent);
            this.Controls.Add(statusStrip);
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
        }

        // --- Helper Logic for New Forms ---
        private void ShowFindReplace(bool isReplace)
        {
            var dialog = new FindReplaceDialog(txtContent, isReplace, IsDarkMode());
            dialog.Show(this);
        }

        private void ShowAbout()
        {
            using var about = new AboutDialog(IsDarkMode());
            about.ShowDialog();
        }

        private void ShowGoTo()
        {
            using var dialog = new GoToDialog(txtContent, IsDarkMode());
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                int lineNum = dialog.LineNumber;
                if (lineNum > 0 && lineNum <= txtContent.Lines.Length)
                {
                    int index = txtContent.GetFirstCharIndexFromLine(lineNum - 1);
                    txtContent.Select(index, 0);
                    txtContent.ScrollToCaret();
                }
                else
                {
                    MessageBox.Show("The line number is beyond the total number of lines", "Notepad - Goto Line");
                }
            }
        }

        // --- UI Events and Theming ---
        private void UpdateStatusBar()
        {
            if (!statusStrip.Visible) return;
            int index = txtContent.SelectionStart;
            int line = txtContent.GetLineFromCharIndex(index);
            int column = index - txtContent.GetFirstCharIndexFromLine(line);
            lblStatus.Text = $"Ln {line + 1}, Col {column + 1}";
        }

        private void FileNew() { if (ConfirmSave()) { txtContent.Clear(); currentFilePath = null; isDirty = false; UpdateTitle(); } }
        private void FileOpen()
        {
            if (!ConfirmSave()) return;
            using var ofd = new OpenFileDialog { Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtContent.Text = File.ReadAllText(ofd.FileName);
                currentFilePath = ofd.FileName;
                isDirty = false;
                UpdateTitle();
            }
        }
        private void FileSave() { if (string.IsNullOrEmpty(currentFilePath)) FileSaveAs(); else { File.WriteAllText(currentFilePath, txtContent.Text); isDirty = false; } }
        private void FileSaveAs()
        {
            using var sfd = new SaveFileDialog { Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*" };
            if (sfd.ShowDialog() == DialogResult.OK) { currentFilePath = sfd.FileName; File.WriteAllText(currentFilePath, txtContent.Text); isDirty = false; UpdateTitle(); }
        }
        private bool ConfirmSave()
        {
            if (!isDirty) return true;
            var res = MessageBox.Show($"Save changes to {currentFilePath ?? "Untitled"}?", "Notepad", MessageBoxButtons.YesNoCancel);
            if (res == DialogResult.Yes) { FileSave(); return true; }
            return res == DialogResult.No;
        }
        private void NotepadForm_FormClosing(object? sender, FormClosingEventArgs e) => e.Cancel = !ConfirmSave();
        private void UpdateTitle() => this.Text = $"{(string.IsNullOrEmpty(currentFilePath) ? "Untitled" : Path.GetFileName(currentFilePath))} - Notepad";

        private void ToggleWordWrap()
        {
            txtContent.WordWrap = !txtContent.WordWrap;
            txtContent.ScrollBars = txtContent.WordWrap ? ScrollBars.Vertical : ScrollBars.Both;
            if (txtContent.WordWrap) statusStrip.Visible = false;
            UpdateStatusBar();
        }

        private void ToggleStatusBar()
        {
            if (!txtContent.WordWrap)
            {
                statusStrip.Visible = !statusStrip.Visible;
                UpdateStatusBar();
            }
        }

        private void ChangeFont()
        {
            using var fd = new FontDialog { Font = txtContent.Font };
            if (fd.ShowDialog() == DialogResult.OK) txtContent.Font = fd.Font;
        }

        public bool IsDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return (key?.GetValue("AppsUseLightTheme") as int?) == 0;
            }
            catch { return false; }
        }

        private void ApplyTheme()
        {
            bool dark = IsDarkMode();
            int useDark = dark ? 1 : 0;
            if (this.IsHandleCreated)
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            if (txtContent.IsHandleCreated)
                SetWindowTheme(txtContent.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);

            Color back = dark ? Color.FromArgb(30, 30, 30) : Color.White;
            Color fore = dark ? Color.FromArgb(220, 220, 220) : Color.Black;
            Color menuBack = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            Color menuFore = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;

            this.BackColor = back;
            txtContent.BackColor = back;
            txtContent.ForeColor = fore;
            menuStrip.BackColor = menuBack;
            menuStrip.ForeColor = menuFore;
            menuStrip.Renderer = dark ? new DarkModeRenderer() : new ToolStripProfessionalRenderer();
            statusStrip.BackColor = menuBack;
            statusStrip.ForeColor = menuFore;

            foreach (ToolStripItem item in menuStrip.Items) UpdateMenuItemColors(item, menuBack, menuFore);
        }

        private void UpdateMenuItemColors(ToolStripItem item, Color back, Color fore)
        {
            item.BackColor = back;
            item.ForeColor = fore;
            if (item is ToolStripMenuItem mi) foreach (ToolStripItem sub in mi.DropDownItems) UpdateMenuItemColors(sub, back, fore);
        }

        // Subclass to handle handle-recreation (e.g. WordWrap toggle)
        private class CustomTextBox : TextBox
        {
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Action? HandleCreatedAction { get; set; }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                HandleCreatedAction?.Invoke();
            }
        }
    }
}