#nullable enable
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    public partial class NotepadForm : Form
    {
        private string? _filePath = null;
        private bool _isDirty = false;
        private bool _isDarkMode = false;
        private FindReplaceDialog? _findReplaceDialog;
        private GoToDialog? _goToDialog;
        private int _lastPrintChar = 0;

        // P/Invoke for Immersive Dark Mode on Title Bar
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // Added Dark Mode Menu Item
        private ToolStripMenuItem darkModeItem;
        // Added Syntax Highlighting Menu Item
        private ToolStripMenuItem syntaxHighlightItem;

        public NotepadForm(string? filePath = null)
        {
            InitializeComponent();

            // Form Setup
            this.Text = "Untitled - Notepad";
            this.Size = new Size(900, 600);
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AllowDrop = true;

            this.FormClosing += NotepadForm_FormClosing;
            this.DragEnter += NotepadForm_DragEnter;
            this.DragDrop += NotepadForm_DragDrop;

            // --- Menu Construction ---
            menuStrip = new MenuStrip();

            // File Menu
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&New", null, (s, e) => FileNew()) { ShortcutKeys = Keys.Control | Keys.N });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Open...", null, (s, e) => FileOpen()) { ShortcutKeys = Keys.Control | Keys.O });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Save", null, (s, e) => FileSave()) { ShortcutKeys = Keys.Control | Keys.S });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save &As...", null, (s, e) => FileSaveAs()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Page Set&up...", null, (s, e) => FilePageSetup()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Print Pre&view", null, (s, e) => FilePrintPreview()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Print...", null, (s, e) => FilePrint()) { ShortcutKeys = Keys.Control | Keys.P });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (s, e) => Close()));

            // Edit Menu
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Undo", null, (s, e) => txtContent.Undo()) { ShortcutKeys = Keys.Control | Keys.Z });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Cu&t", null, (s, e) => txtContent.Cut()) { ShortcutKeys = Keys.Control | Keys.X });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Copy", null, (s, e) => txtContent.Copy()) { ShortcutKeys = Keys.Control | Keys.C });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Paste", null, (s, e) => txtContent.Paste()) { ShortcutKeys = Keys.Control | Keys.V });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Find...", null, (s, e) => ShowFindReplace(false)) { ShortcutKeys = Keys.Control | Keys.F });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Replace...", null, (s, e) => ShowFindReplace(true)) { ShortcutKeys = Keys.Control | Keys.H });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Go &To...", null, (s, e) => ShowGoTo()) { ShortcutKeys = Keys.Control | Keys.G });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Select &All", null, (s, e) => txtContent.SelectAll()) { ShortcutKeys = Keys.Control | Keys.A });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Time/&Date", null, (s, e) => txtContent.AppendText(DateTime.Now.ToString())) { ShortcutKeys = Keys.F5 });

            // Format Menu
            var formatMenu = new ToolStripMenuItem("&Format");
            wordWrapItem = new ToolStripMenuItem("&Word Wrap", null, (s, e) => ToggleWordWrap()) { CheckOnClick = true };
            formatMenu.DropDownItems.Add(wordWrapItem);
            formatMenu.DropDownItems.Add(new ToolStripMenuItem("&Font...", null, (s, e) => ChangeFont()));

            // View Menu
            var viewMenu = new ToolStripMenuItem("&View");
            statusItem = new ToolStripMenuItem("&Status Bar", null, (s, e) => ToggleStatusBar()) { CheckOnClick = true };
            smoothScrollItem = new ToolStripMenuItem("Smooth &Scrolling", null, (s, e) => ToggleSmoothScroll()) { CheckOnClick = true };
            // NEW: Syntax Highlighting Menu Item
            syntaxHighlightItem = new ToolStripMenuItem("Syntax &Highlighting", null, (s, e) => ToggleSyntaxHighlighting()) { CheckOnClick = true };
            darkModeItem = new ToolStripMenuItem("&Dark Mode", null, (s, e) => ToggleDarkMode()) { CheckOnClick = true };

            var zoomMenu = new ToolStripMenuItem("&Zoom");
            zoomMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom &In", null, (s, e) => Zoom(0.1f)) { ShortcutKeys = Keys.Control | Keys.Oemplus });
            zoomMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom &Out", null, (s, e) => Zoom(-0.1f)) { ShortcutKeys = Keys.Control | Keys.OemMinus });
            zoomMenu.DropDownItems.Add(new ToolStripMenuItem("&Restore Default Zoom", null, (s, e) => { txtContent.ZoomFactor = 1.0f; UpdateStatusBar(); }) { ShortcutKeys = Keys.Control | Keys.D0 });

            viewMenu.DropDownItems.Add(zoomMenu);
            viewMenu.DropDownItems.Add(statusItem);
            viewMenu.DropDownItems.Add(smoothScrollItem);
            viewMenu.DropDownItems.Add(syntaxHighlightItem);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(darkModeItem);

            // Help Menu
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&About", null, (s, e) => ShowAbout()));

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, formatMenu, viewMenu, helpMenu });

            // --- Editor ---
            txtContent = new CustomRichTextBox();
            txtContent.Multiline = true;
            txtContent.Dock = DockStyle.Fill;
            txtContent.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtContent.Font = new Font("Consolas", 11f);
            txtContent.BorderStyle = BorderStyle.None;
            txtContent.AcceptsTab = true;

            // Editor Events
            txtContent.TextChanged += OnContentChanged;
            txtContent.SelectionChanged += OnSelectionChanged;
            txtContent.HandleCreatedAction = () => ApplyTheme();
            txtContent.MouseWheel += OnEditorMouseWheel;

            // --- Status Strip ---
            statusStrip = new StatusStrip();
            lblCursorPos = new ToolStripStatusLabel { Text = "  Ln 1, Col 1", AutoSize = false, Width = 150, TextAlign = ContentAlignment.MiddleLeft };
            lblZoom = new ToolStripStatusLabel { Text = "100%", AutoSize = false, Width = 60 };
            lblEncoding = new ToolStripStatusLabel { Text = "UTF-8", AutoSize = false, Width = 100 };
            var spring = new ToolStripStatusLabel { Spring = true };

            statusStrip.Items.AddRange(new ToolStripItem[] { lblCursorPos, spring, lblZoom, lblEncoding });
            statusStrip.Visible = true;

            // --- Printing Initialization ---
            printDocument = new PrintDocument();
            printDocument.BeginPrint += PrintDocument_BeginPrint;
            printDocument.PrintPage += PrintDocument_PrintPage;
            printDocument.EndPrint += PrintDocument_EndPrint;

            printDialog = new PrintDialog();
            printDialog.Document = printDocument;
            printDialog.UseEXDialog = false;

            pageSetupDialog = new PageSetupDialog();
            pageSetupDialog.Document = printDocument;

            printPreviewDialog = new PrintPreviewDialog();
            printPreviewDialog.Document = printDocument;
            printPreviewDialog.StartPosition = FormStartPosition.CenterParent;
            try
            {
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotepadForm));
                var iconObj = resources.GetObject("$this.Icon");
                if (iconObj is Icon icon)
                {
                    printPreviewDialog.Icon = icon;
                }
            }
            catch { }

            // Add Controls
            this.Controls.Add(txtContent);
            this.Controls.Add(statusStrip);
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;

            // Final setup
            LoadSettings();

            if (filePath != null)
            {
                OpenFile(filePath);
            }
        }

        private void OnEditorMouseWheel(object? sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control) UpdateStatusBar();
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            UpdateStatusBar();
        }

        private void OnContentChanged(object? sender, EventArgs e)
        {
            SetDirty(true);
            UpdateStatusBar();
        }

        private void SetDirty(bool dirty)
        {
            if (_isDirty == dirty) return;
            _isDirty = dirty;
            this.Text = this.Text.EndsWith("*") ? this.Text.Substring(0, this.Text.Length - 1) : this.Text + "*";
        }

        private void UpdateStatusBar()
        {
            // Cursor Position
            int line = txtContent.GetLineFromCharIndex(txtContent.SelectionStart) + 1;
            int col = txtContent.SelectionStart - txtContent.GetFirstCharIndexOfCurrentLine() + 1;
            lblCursorPos.Text = $"  Ln {line}, Col {col}";

            // Zoom
            lblZoom.Text = $"{txtContent.ZoomFactor * 100:F0}%";
        }

        private void FileNew()
        {
            if (CheckDirty())
            {
                txtContent.Text = string.Empty;
                _filePath = null;
                txtContent.CurrentSyntaxMode = SyntaxMode.None; // Reset syntax
                this.Text = "Untitled - Notepad";
                SetDirty(false);
            }
        }

        private void FileOpen()
        {
            if (CheckDirty())
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        OpenFile(ofd.FileName);
                    }
                }
            }
        }

        private void FileSave()
        {
            if (_filePath == null)
            {
                FileSaveAs();
            }
            else
            {
                try
                {
                    File.WriteAllText(_filePath, txtContent.Text, Encoding.UTF8);
                    SetDirty(false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void FileSaveAs()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _filePath = sfd.FileName;
                    this.Text = Path.GetFileName(_filePath) + " - Notepad";
                    FileSave();

                    // Re-evaluate syntax mode based on new extension
                    OpenFile(_filePath);
                }
            }
        }

        private void FilePageSetup()
        {
            pageSetupDialog.ShowDialog();
        }

        private void FilePrintPreview()
        {
            _lastPrintChar = 0;
            printPreviewDialog.ShowDialog();
        }

        private void FilePrint()
        {
            if (printDialog.ShowDialog() == DialogResult.OK)
            {
                _lastPrintChar = 0;
                printDocument.Print();
            }
        }

        private void PrintDocument_BeginPrint(object? sender, PrintEventArgs e)
        {
            _lastPrintChar = 0;
        }

        private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
        {
            _lastPrintChar = txtContent.FormatRange(false, e, _lastPrintChar, txtContent.TextLength);
            e.HasMorePages = (_lastPrintChar < txtContent.TextLength);
        }

        private void PrintDocument_EndPrint(object? sender, PrintEventArgs e)
        {
            txtContent.FormatRangeDone();
        }

        private void ChangeFont()
        {
            using (var fd = new FontDialog())
            {
                fd.Font = txtContent.Font;
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    txtContent.Font = fd.Font;
                }
            }
        }

        private void ToggleWordWrap()
        {
            txtContent.WordWrap = wordWrapItem.Checked;
        }

        private void ToggleStatusBar()
        {
            statusStrip.Visible = statusItem.Checked;
        }

        private void ToggleSmoothScroll()
        {
            txtContent.EnableSmoothScrolling = smoothScrollItem.Checked;
        }

        private void ToggleSyntaxHighlighting()
        {
            txtContent.EnableSyntaxHighlighting = syntaxHighlightItem.Checked;
        }

        private void ToggleDarkMode()
        {
            _isDarkMode = darkModeItem.Checked;
            ApplyTheme();

            if (_findReplaceDialog != null && !_findReplaceDialog.IsDisposed)
            {
                _findReplaceDialog.UpdateTheme(_isDarkMode);
            }
        }

        private void Zoom(float delta)
        {
            float newZoom = txtContent.ZoomFactor + delta;
            if (newZoom >= 0.1f && newZoom <= 5.0f)
            {
                txtContent.ZoomFactor = newZoom;
                UpdateStatusBar();
            }
        }

        private void ShowFindReplace(bool showReplace)
        {
            if (_findReplaceDialog == null || _findReplaceDialog.IsDisposed)
            {
                _findReplaceDialog = new FindReplaceDialog(txtContent, showReplace, _isDarkMode);
            }
            else
            {
                _findReplaceDialog.SetMode(showReplace);
            }

            if (!_findReplaceDialog.Visible)
            {
                _findReplaceDialog.Show(this);
            }
            _findReplaceDialog.Focus();
        }

        private void ShowGoTo()
        {
            if (_goToDialog == null || _goToDialog.IsDisposed)
            {
                _goToDialog = new GoToDialog(txtContent, _isDarkMode);
            }
            _goToDialog.ShowDialog(this);
        }

        private void ShowAbout()
        {
            using (var about = new AboutDialog(_isDarkMode))
            {
                about.ShowDialog(this);
            }
        }

        private void NotepadForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!CheckDirty())
            {
                e.Cancel = true;
            }
            else
            {
                SaveSettings();
            }
        }

        private void NotepadForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void NotepadForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    OpenFile(files[0]);
                }
            }
        }

        private bool CheckDirty()
        {
            if (_isDirty)
            {
                var result = MessageBox.Show(
                    $"Do you want to save changes to {(_filePath ?? "Untitled")}?",
                    "Notepad",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    FileSave();
                    return !_isDirty;
                }
                else if (result == DialogResult.Cancel)
                {
                    return false;
                }
            }
            return true;
        }

        private void OpenFile(string path)
        {
            try
            {
                txtContent.Text = File.ReadAllText(path, Encoding.UTF8);
                _filePath = path;
                this.Text = Path.GetFileName(path) + " - Notepad";

                // FIX: Use null coalescing (??) to handle cases where GetExtension returns null
                string ext = Path.GetExtension(path)?.ToLower() ?? string.Empty;

                if (ext == ".log")
                {
                    txtContent.CurrentSyntaxMode = SyntaxMode.Logs;
                }
                else if (ext == ".ini" || ext == ".json" || ext == ".xml" || ext == ".config")
                {
                    txtContent.CurrentSyntaxMode = SyntaxMode.Config;
                }
                else
                {
                    txtContent.CurrentSyntaxMode = SyntaxMode.None;
                }

                SetDirty(false);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSettings()
        {
            var settings = AppSettings.Load();
            this.WindowState = settings.IsMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            if (this.WindowState == FormWindowState.Normal)
            {
                this.Location = settings.Location;
                this.Size = settings.Size;
            }

            wordWrapItem.Checked = settings.WordWrap;
            ToggleWordWrap();

            statusItem.Checked = settings.StatusBar;
            ToggleStatusBar();

            smoothScrollItem.Checked = settings.SmoothScrolling;
            ToggleSmoothScroll();

            // Load Syntax Highlighting Setting
            syntaxHighlightItem.Checked = settings.SyntaxHighlighting;
            ToggleSyntaxHighlighting();

            darkModeItem.Checked = settings.DarkMode;
            ToggleDarkMode();

            if (settings.Font != null)
            {
                txtContent.Font = settings.Font;
            }
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                IsMaximized = this.WindowState == FormWindowState.Maximized,
                Location = this.Location,
                Size = this.Size,
                WordWrap = wordWrapItem.Checked,
                StatusBar = statusItem.Checked,
                SmoothScrolling = smoothScrollItem.Checked,
                DarkMode = darkModeItem.Checked,
                SyntaxHighlighting = syntaxHighlightItem.Checked, // Save Setting
                Font = txtContent.Font
            };
            settings.Save();
        }

        private void ApplyTheme()
        {
            int useDark = _isDarkMode ? 1 : 0;

            // Apply Title Bar Theme
            try
            {
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            }
            catch { }

            // Notify Editor of Theme Change (Important for Syntax Colors)
            txtContent.IsDarkMode = _isDarkMode;

            if (_isDarkMode)
            {
                // Dark Theme Colors
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.White;

                txtContent.BackColor = Color.FromArgb(30, 30, 30);
                txtContent.ForeColor = Color.White;

                statusStrip.BackColor = Color.FromArgb(45, 45, 48);
                statusStrip.ForeColor = Color.White;

                menuStrip.BackColor = Color.FromArgb(45, 45, 48);
                menuStrip.ForeColor = Color.White;
                menuStrip.Renderer = new DarkModeRenderer();
            }
            else
            {
                // Light/Default Theme Colors
                this.BackColor = SystemColors.Control;
                this.ForeColor = SystemColors.ControlText;

                txtContent.BackColor = Color.White;
                txtContent.ForeColor = Color.Black;

                statusStrip.BackColor = SystemColors.Control;
                statusStrip.ForeColor = SystemColors.ControlText;

                menuStrip.BackColor = SystemColors.Control;
                menuStrip.ForeColor = SystemColors.ControlText;
                menuStrip.Renderer = new ToolStripProfessionalRenderer();
            }

            // Refresh controls
            this.Invalidate();
        }
    }
}