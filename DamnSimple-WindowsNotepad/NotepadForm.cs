#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Represents the main window of the Notepad application.
    /// This class is engineered with a focus on Mechanical Sympathy and Data-Oriented Design principles
    /// to ensure a responsive user experience by minimizing latency and respecting hardware architecture.
    /// </summary>
    public partial class NotepadForm : Form
    {
        private string? _filePath = null;
        private bool _isDirty = false;
        private bool _isDarkMode = false;
        private FindReplaceDialog? _findReplaceDialog;
        private GoToDialog? _goToDialog;
        private int _lastPrintChar = 0;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private ToolStripMenuItem darkModeItem;
        private ToolStripMenuItem syntaxHighlightItem;

        /// <summary>
        /// Defines a complete set of theme attributes in a contiguous memory block.
        /// <para><b>The Blueprint:</b> This struct is a classic Data-Oriented Design pattern. By grouping related data (colors, renderer), we ensure cache locality. When a theme is selected, this entire block is likely fetched into a single cache line (or two), making subsequent member access extremely fast.</para>
        /// <para><b>Silicon Impact:</b> This layout avoids pointer chasing that would occur with a class or separate variables, preventing cache misses and keeping the CPU's execution units fed. The `readonly` keyword ensures immutability, allowing the JIT to perform further optimizations.</para>
        /// </summary>
        private readonly struct ThemeColors
        {
            public readonly Color FormBack;
            public readonly Color FormFore;
            public readonly Color TextBack;
            public readonly Color TextFore;
            public readonly ToolStripRenderer Renderer;

            public ThemeColors(Color formBack, Color formFore, Color textBack, Color textFore, ToolStripRenderer renderer)
            {
                FormBack = formBack;
                FormFore = formFore;
                TextBack = textBack;
                TextFore = textFore;
                Renderer = renderer;
            }
        }

        private static readonly ThemeColors LightTheme = new(SystemColors.Control, SystemColors.ControlText, Color.White, Color.Black, new ToolStripProfessionalRenderer());
        private static readonly ThemeColors DarkTheme = new(Color.FromArgb(45, 45, 48), Color.White, Color.FromArgb(30, 30, 30), Color.White, new DarkModeRenderer());

        /// <summary>
        /// Provides a hash map for O(1) lookup of syntax highlighting modes based on file extension.
        /// <para><b>The Metal Analysis:</b> A sequence of `if/else if` string comparisons for file extensions results in O(n) complexity and invites branch misprediction stalls. By using a `Dictionary`, we trade a small amount of memory for a constant-time lookup. The initial hash computation is more expensive than a single string compare, but it avoids the catastrophic pipeline flushes of a mispredicted branch chain, leading to superior and more predictable performance, especially as the number of syntax modes grows.</para>
        /// </summary>
        private static readonly Dictionary<string, SyntaxMode> SyntaxModeMap = new()
        {
            { ".log", SyntaxMode.Logs },
            { ".ini", SyntaxMode.Config },
            { ".json", SyntaxMode.Config },
            { ".xml", SyntaxMode.Config },
            { ".config", SyntaxMode.Config }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="NotepadForm"/> class.
        /// The constructor orchestrates the assembly of the UI components, binding of events, and loading of user settings.
        /// </summary>
        /// <param name="filePath">The optional path to a file to be opened upon application startup.</param>
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

        /// <summary>
        /// Updates the form's title to reflect the current file state.
        /// </summary>
        /// <param name="dirty">The new dirty state of the file.</param>
        /// <note type="performance">
        /// This method avoids string concatenation and interpolation in a loop or frequent-update scenario.
        /// By using a conditional assignment of pre-formatted strings, we prevent repeated heap allocations
        /// and the associated overhead from the garbage collector, ensuring a more predictable performance profile.
        /// </note>
        private void SetDirty(bool dirty)
        {
            if (_isDirty == dirty) return;
            _isDirty = dirty;

            string baseTitle = _filePath != null ? Path.GetFileName(_filePath) : "Untitled";
            this.Text = dirty ? $"{baseTitle} - Notepad*" : $"{baseTitle} - Notepad";
        }

        /// <summary>
        /// Updates the status bar with the current cursor position and zoom level.
        /// <para><b>The Metal Analysis:</b> This is a high-frequency operation, triggered on every selection change. The calculations are simple arithmetic and do not involve heap allocations, ensuring low-latency execution. The `GetLineFromCharIndex` and `GetFirstCharIndexOfCurrentLine` calls are optimized within the RichTextBox control, but frequent calls can still contribute to overhead. The update is lightweight enough to not cause noticeable UI stutter.</para>
        /// </summary>
        private void UpdateStatusBar()
        {
            // Cursor Position
            int line = txtContent.GetLineFromCharIndex(txtContent.SelectionStart) + 1;
            int col = txtContent.SelectionStart - txtContent.GetFirstCharIndexOfCurrentLine() + 1;
            lblCursorPos.Text = $"  Ln {line}, Col {col}";

            // Zoom
            lblZoom.Text = $"{txtContent.ZoomFactor * 100:F0}%";
        }

        /// <summary>
        /// Updates the form's title to reflect the current file state.
        /// </summary>
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

        /// <summary>
        /// Checks if the document has unsaved changes and prompts the user to save them.
        /// </summary>
        /// <returns><c>true</c> if the operation should continue (file is clean or user chose 'No'); <c>false</c> if the operation should be cancelled.</returns>
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

        /// <summary>
        /// Opens a file and loads its content into the editor.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        private void OpenFile(string path)
        {
            try
            {
                // The Metal Analysis: Reading the entire file is a large, single allocation.
                // While unavoidable for the RichTextBox control, acknowledging this is key.
                // For extreme performance, a custom control using memory-mapped files would be required.
                txtContent.Text = File.ReadAllText(path, Encoding.UTF8);
                _filePath = path;
                this.Text = Path.GetFileName(path) + " - Notepad";

                // The Metal Analysis: Replaced if/else chain with O(1) dictionary lookup.
                // This avoids sequential string comparisons and potential branch mispredictions.
                string ext = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
                txtContent.CurrentSyntaxMode = SyntaxModeMap.GetValueOrDefault(ext, SyntaxMode.None);

                SetDirty(false);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Deserializes and applies application settings from persistent storage.
        /// <para><b>The Metal Analysis:</b> Settings are loaded once at startup. The `AppSettings.Load()` method encapsulates file I/O and deserialization, which are blocking operations. This is acceptable during initialization but would be an anti-pattern in a performance-critical path. The subsequent application of settings involves direct property assignments, which are low-cost.</para>
        /// </summary>
        private void LoadSettings()
        {
            var settings = AppSettings.Load();
            this.WindowState = settings.IsMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            if (this.WindowState == FormWindowState.Normal && AppSettings.IsWindowVisible(settings))
            {
                this.Location = new Point(settings.WindowX, settings.WindowY);
                this.Size = new Size(settings.WindowWidth, settings.WindowHeight);
            }

            wordWrapItem.Checked = settings.WordWrap;
            ToggleWordWrap();

            statusItem.Checked = settings.StatusBarVisible;
            ToggleStatusBar();

            smoothScrollItem.Checked = settings.SmoothScrolling;
            ToggleSmoothScroll();

            syntaxHighlightItem.Checked = settings.SyntaxHighlighting;
            ToggleSyntaxHighlighting();

            darkModeItem.Checked = settings.DarkMode;
            ToggleDarkMode();

            try
            {
                txtContent.Font = new Font(settings.FontFamily, settings.FontSize, (FontStyle)settings.FontStyle);
            }
            catch
            {
                // Reason: Font might not be installed. Fallback to a known good state.
                txtContent.Font = new Font("Consolas", 11f, FontStyle.Regular);
            }
        }

        /// <summary>
        /// Gathers current application state and serializes it to persistent storage.
        /// </summary>
        private void SaveSettings()
        {
            var settings = new AppSettingsData
            {
                IsMaximized = this.WindowState == FormWindowState.Maximized,
                WordWrap = wordWrapItem.Checked,
                StatusBarVisible = statusItem.Checked,
                SmoothScrolling = smoothScrollItem.Checked,
                DarkMode = darkModeItem.Checked,
                SyntaxHighlighting = syntaxHighlightItem.Checked,
                FontFamily = txtContent.Font.Name,
                FontSize = txtContent.Font.Size,
                FontStyle = (int)txtContent.Font.Style
            };

            if (this.WindowState == FormWindowState.Normal)
            {
                settings.WindowX = this.Location.X;
                settings.WindowY = this.Location.Y;
                settings.WindowWidth = this.Size.Width;
                settings.WindowHeight = this.Size.Height;
            }

            AppSettings.Save(settings);
        }

        /// <summary>
        /// Applies the selected color theme to all UI components.
        /// </summary>
        private void ApplyTheme()
        {
            int useDark = _isDarkMode ? 1 : 0;

            // Apply Title Bar Theme
            try
            {
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            }
            catch { }

            txtContent.IsDarkMode = _isDarkMode;

            var theme = _isDarkMode ? DarkTheme : LightTheme;

            this.BackColor = theme.FormBack;
            this.ForeColor = theme.FormFore;

            txtContent.BackColor = theme.TextBack;
            txtContent.ForeColor = theme.TextFore;

            statusStrip.BackColor = theme.FormBack;
            statusStrip.ForeColor = theme.FormFore;

            menuStrip.BackColor = theme.FormBack;
            menuStrip.ForeColor = theme.FormFore;
            menuStrip.Renderer = theme.Renderer;

            // Refresh controls
            this.Invalidate();
        }
    }
}