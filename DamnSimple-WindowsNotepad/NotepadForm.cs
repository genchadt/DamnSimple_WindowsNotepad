#nullable enable
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DamnSimple_WindowsNotepad
{
    public partial class NotepadForm : Form
    {
        #region Fields

        private string? _currentFilePath = null;
        private bool _isDirty = false;
        private Encoding _currentEncoding = Encoding.UTF8;
        private AppSettings _settings;
        private FindReplaceDialog? _findDialog = null;

        // Printing State
        private int _checkPrint;

        // P/Invoke for Dark Mode
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

        #endregion

        #region Constructor & Initialization

        public NotepadForm()
        {
            _settings = AppSettings.Load();
            InitializeComponent();
            ApplySettings();
            ApplyTheme();

            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General) ApplyTheme();
            };
        }

        private void ApplySettings()
        {
            if (_settings.IsWindowVisible())
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(_settings.WindowX, _settings.WindowY);
                this.Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
            }

            if (_settings.IsMaximized) this.WindowState = FormWindowState.Maximized;

            try { txtContent.Font = new Font(_settings.FontFamily, _settings.FontSize, (FontStyle)_settings.FontStyle); }
            catch { txtContent.Font = new Font("Consolas", 11f); }

            txtContent.WordWrap = _settings.WordWrap;
            txtContent.EnableSmoothScrolling = _settings.SmoothScrolling;
            statusStrip.Visible = _settings.StatusBarVisible;

            wordWrapItem.Checked = _settings.WordWrap;
            smoothScrollItem.Checked = _settings.SmoothScrolling;
            statusItem.Checked = _settings.StatusBarVisible;

            UpdateStatusBar();
        }

        #endregion

        #region File Operations

        private void FileNew()
        {
            if (ConfirmSave()) { txtContent.Clear(); _currentFilePath = null; _currentEncoding = Encoding.UTF8; SetDirty(false); UpdateTitle(); UpdateStatusBar(); }
        }

        private void FileOpen()
        {
            if (!ConfirmSave()) return;
            using var ofd = new OpenFileDialog { Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*", Title = "Open" };
            if (ofd.ShowDialog() == DialogResult.OK) LoadFile(ofd.FileName);
        }

        private void LoadFile(string path)
        {
            try
            {
                using (var reader = new StreamReader(path, true))
                {
                    txtContent.Text = reader.ReadToEnd();
                    _currentEncoding = reader.CurrentEncoding;
                }
                _currentFilePath = path; SetDirty(false); UpdateTitle(); UpdateStatusBar();
                txtContent.SelectionStart = 0; txtContent.ScrollToCaret();
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Notepad", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void FileSave() { if (string.IsNullOrEmpty(_currentFilePath)) FileSaveAs(); else SaveFile(_currentFilePath); }

        private void FileSaveAs()
        {
            using var sfd = new SaveFileDialog { Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*", Title = "Save As" };
            if (sfd.ShowDialog() == DialogResult.OK) SaveFile(sfd.FileName);
        }

        private void SaveFile(string path)
        {
            try { File.WriteAllText(path, txtContent.Text, _currentEncoding); _currentFilePath = path; SetDirty(false); UpdateTitle(); }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Notepad", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private bool ConfirmSave()
        {
            if (!_isDirty) return true;
            string fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
            var res = MessageBox.Show($"Save changes to {fileName}?", "Notepad", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Yes) { FileSave(); return !_isDirty; }
            return res == DialogResult.No;
        }

        #endregion

        #region Printing Logic

        private void FilePageSetup()
        {
            pageSetupDialog.PageSettings = printDocument.DefaultPageSettings;
            if (pageSetupDialog.ShowDialog() == DialogResult.OK)
            {
                printDocument.DefaultPageSettings = pageSetupDialog.PageSettings;
            }
        }

        private void FilePrintPreview()
        {
            printPreviewDialog.ShowDialog();
        }

        private void FilePrint()
        {
            if (printDialog.ShowDialog() == DialogResult.OK)
            {
                printDocument.Print();
            }
        }

        private void PrintDocument_BeginPrint(object sender, PrintEventArgs e)
        {
            _checkPrint = 0; // Reset character counter
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            // Print the content using the CustomRichTextBox's FormatRange method
            // This renders the text inside the margins defined by PageSetup
            _checkPrint = txtContent.FormatRange(false, e, _checkPrint, txtContent.TextLength);

            // If we haven't reached the end of the text, we need another page
            if (_checkPrint < txtContent.TextLength)
            {
                e.HasMorePages = true;
            }
            else
            {
                e.HasMorePages = false;
                txtContent.FormatRangeDone(); // Cleanup
            }
        }

        #endregion

        #region Editor Logic

        private void Zoom(float delta)
        {
            float newZoom = txtContent.ZoomFactor + delta;
            if (newZoom >= 0.1f && newZoom <= 5.0f) { txtContent.ZoomFactor = newZoom; UpdateStatusBar(); }
        }

        private void ShowFindReplace(bool isReplace)
        {
            if (_findDialog != null && !_findDialog.IsDisposed) { _findDialog.BringToFront(); return; }
            _findDialog = new FindReplaceDialog(txtContent, isReplace, IsDarkMode());
            _findDialog.Show(this);
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
                    txtContent.Select(index, 0); txtContent.ScrollToCaret();
                }
            }
        }

        private void ShowAbout() { using var about = new AboutDialog(IsDarkMode()); about.ShowDialog(); }

        private void ChangeFont()
        {
            using var fd = new FontDialog { Font = txtContent.Font, ShowEffects = false };
            if (fd.ShowDialog() == DialogResult.OK) txtContent.Font = fd.Font;
        }

        private void ToggleWordWrap() { txtContent.WordWrap = !txtContent.WordWrap; wordWrapItem.Checked = txtContent.WordWrap; UpdateStatusBar(); }
        private void ToggleSmoothScroll() { txtContent.EnableSmoothScrolling = !txtContent.EnableSmoothScrolling; smoothScrollItem.Checked = txtContent.EnableSmoothScrolling; }
        private void ToggleStatusBar() { statusStrip.Visible = !statusStrip.Visible; statusItem.Checked = statusStrip.Visible; UpdateStatusBar(); }

        #endregion

        #region UI Updates & Theming

        private void SetDirty(bool dirty) { if (_isDirty != dirty) { _isDirty = dirty; UpdateTitle(); } }

        private void UpdateTitle()
        {
            string fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
            this.Text = $"{(_isDirty ? "*" : "")}{fileName} - Notepad";
        }

        private void UpdateStatusBar()
        {
            if (!statusStrip.Visible) return;
            int index = txtContent.SelectionStart;
            int line = txtContent.GetLineFromCharIndex(index);
            int column = index - txtContent.GetFirstCharIndexFromLine(line);
            lblCursorPos.Text = $"  Ln {line + 1}, Col {column + 1}";
            lblZoom.Text = $"{(int)(txtContent.ZoomFactor * 100)}%";
            lblEncoding.Text = _currentEncoding.BodyName.ToUpper();
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
            if (this.IsHandleCreated) DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            if (txtContent.IsHandleCreated) SetWindowTheme(txtContent.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);

            Color back = dark ? Color.FromArgb(30, 30, 30) : Color.White;
            Color fore = dark ? Color.FromArgb(220, 220, 220) : Color.Black;
            Color menuBack = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            Color menuFore = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;

            this.BackColor = back; txtContent.BackColor = back; txtContent.ForeColor = fore;
            menuStrip.BackColor = menuBack; menuStrip.ForeColor = menuFore;
            menuStrip.Renderer = dark ? new DarkModeRenderer() : new ToolStripProfessionalRenderer();
            menuStrip.Invalidate();
            statusStrip.BackColor = menuBack; statusStrip.ForeColor = menuFore;
            foreach (ToolStripItem item in menuStrip.Items) UpdateMenuItemColors(item, menuBack, menuFore);
        }

        private void UpdateMenuItemColors(ToolStripItem item, Color back, Color fore)
        {
            item.BackColor = back; item.ForeColor = fore;
            if (item is ToolStripMenuItem mi) foreach (ToolStripItem sub in mi.DropDownItems) UpdateMenuItemColors(sub, back, fore);
        }

        #endregion

        #region Events

        private void NotepadForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConfirmSave()) { e.Cancel = true; return; }
            _settings.IsMaximized = this.WindowState == FormWindowState.Maximized;
            if (this.WindowState == FormWindowState.Normal) { _settings.WindowX = this.Location.X; _settings.WindowY = this.Location.Y; _settings.WindowWidth = this.Size.Width; _settings.WindowHeight = this.Size.Height; }
            else { _settings.WindowX = this.RestoreBounds.X; _settings.WindowY = this.RestoreBounds.Y; _settings.WindowWidth = this.RestoreBounds.Width; _settings.WindowHeight = this.RestoreBounds.Height; }
            _settings.FontFamily = txtContent.Font.FontFamily.Name; _settings.FontSize = txtContent.Font.Size; _settings.FontStyle = (int)txtContent.Font.Style;
            _settings.WordWrap = txtContent.WordWrap; _settings.StatusBarVisible = statusStrip.Visible; _settings.SmoothScrolling = txtContent.EnableSmoothScrolling;
            _settings.Save();
        }

        private void NotepadForm_DragEnter(object? sender, DragEventArgs e) { if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }
        private void NotepadForm_DragDrop(object? sender, DragEventArgs e) { if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) { string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop); if (files != null && files.Length > 0 && ConfirmSave()) LoadFile(files[0]); } }

        #endregion
    }
}