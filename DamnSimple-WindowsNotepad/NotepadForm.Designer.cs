#nullable enable
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    public partial class NotepadForm
    {
        private System.ComponentModel.IContainer? components = null;

        // Controls
        private CustomRichTextBox txtContent = null!;
        private MenuStrip menuStrip = null!;
        private StatusStrip statusStrip = null!;

        // Status Labels
        private ToolStripStatusLabel lblCursorPos = null!;
        private ToolStripStatusLabel lblZoom = null!;
        private ToolStripStatusLabel lblEncoding = null!;

        // Menu Items
        private ToolStripMenuItem statusItem = null!;
        private ToolStripMenuItem wordWrapItem = null!;
        private ToolStripMenuItem smoothScrollItem = null!;

        // Printing Components
        private PrintDocument printDocument = null!;
        private PrintDialog printDialog = null!;
        private PageSetupDialog pageSetupDialog = null!;
        private PrintPreviewDialog printPreviewDialog = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form)); // Needed for PrintPreview icon

            // Form Setup
            this.Text = "Untitled - Notepad";
            this.Size = new Size(900, 600);
            this.Icon = SystemIcons.Application;
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
            // Print Items
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

            var zoomMenu = new ToolStripMenuItem("&Zoom");
            zoomMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom &In", null, (s, e) => Zoom(0.1f)) { ShortcutKeys = Keys.Control | Keys.Oemplus });
            zoomMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom &Out", null, (s, e) => Zoom(-0.1f)) { ShortcutKeys = Keys.Control | Keys.OemMinus });
            zoomMenu.DropDownItems.Add(new ToolStripMenuItem("&Restore Default Zoom", null, (s, e) => { txtContent.ZoomFactor = 1.0f; UpdateStatusBar(); }) { ShortcutKeys = Keys.Control | Keys.D0 });

            viewMenu.DropDownItems.Add(zoomMenu);
            viewMenu.DropDownItems.Add(statusItem);
            viewMenu.DropDownItems.Add(smoothScrollItem);

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
            txtContent.TextChanged += (s, e) => { SetDirty(true); UpdateStatusBar(); };
            txtContent.SelectionChanged += (s, e) => UpdateStatusBar();
            txtContent.HandleCreatedAction = () => ApplyTheme();
            txtContent.MouseWheel += (s, e) => { if (Control.ModifierKeys == Keys.Control) UpdateStatusBar(); };

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

            printDialog = new PrintDialog();
            printDialog.Document = printDocument;
            printDialog.UseEXDialog = true; // Use modern Windows print dialog

            pageSetupDialog = new PageSetupDialog();
            pageSetupDialog.Document = printDocument;

            printPreviewDialog = new PrintPreviewDialog();
            printPreviewDialog.Document = printDocument;
            printPreviewDialog.StartPosition = FormStartPosition.CenterParent;
            // Attempt to set a standard icon for preview (optional)
            try { printPreviewDialog.Icon = (Icon)resources.GetObject("$this.Icon"); } catch { }

            // Add Controls
            this.Controls.Add(txtContent);
            this.Controls.Add(statusStrip);
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
        }

        // Custom control helper with Smooth Scrolling, Theming, and PRINTING support
        private class CustomRichTextBox : RichTextBox
        {
            public Action? HandleCreatedAction { get; set; }
            public bool EnableSmoothScrolling { get; set; } = true;

            // --- P/Invoke Constants ---
            private const int WM_USER = 0x0400;
            private const int EM_FORMATRANGE = WM_USER + 57;
            private const int WM_MOUSEWHEEL = 0x020A;
            private const int EM_LINESCROLL = 0x00B6;

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref STRUCT_FORMATRANGE lParam);

            // --- Structs for Printing ---
            [StructLayout(LayoutKind.Sequential)]
            private struct STRUCT_RECT
            {
                public int left; public int top; public int right; public int bottom;
            }
            [StructLayout(LayoutKind.Sequential)]
            private struct STRUCT_CHARRANGE
            {
                public int cpMin; public int cpMax;
            }
            [StructLayout(LayoutKind.Sequential)]
            private struct STRUCT_FORMATRANGE
            {
                public IntPtr hdc;
                public IntPtr hdcTarget;
                public STRUCT_RECT rc;
                public STRUCT_RECT rcPage;
                public STRUCT_CHARRANGE chrg;
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                HandleCreatedAction?.Invoke();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_MOUSEWHEEL && !EnableSmoothScrolling)
                {
                    long wParam = m.WParam.ToInt64();
                    int delta = (short)((wParam >> 16) & 0xFFFF);
                    int linesToScroll = -Math.Sign(delta) * 3;
                    SendMessage(this.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)linesToScroll);
                    return;
                }
                base.WndProc(ref m);
            }

            /// <summary>
            /// Renders the content of the RichTextBox to a Graphics object (Printer).
            /// </summary>
            public int FormatRange(bool measureOnly, PrintPageEventArgs e, int charFrom, int charTo)
            {
                // Convert .NET bounds (1/100 inch) to Twips (1/1440 inch)
                // 1 inch = 1440 Twips. 100 Display Units = 1 inch.
                // Therefore 1 Display Unit = 14.4 Twips.
                int w = (int)(e.MarginBounds.Width * 14.4);
                int h = (int)(e.MarginBounds.Height * 14.4);
                int l = (int)(e.MarginBounds.Left * 14.4);
                int t = (int)(e.MarginBounds.Top * 14.4);

                STRUCT_RECT rc = new STRUCT_RECT { left = l, top = t, right = l + w, bottom = t + h };
                STRUCT_RECT rcPage = new STRUCT_RECT { left = 0, top = 0, right = (int)(e.PageBounds.Width * 14.4), bottom = (int)(e.PageBounds.Height * 14.4) };

                IntPtr hdc = e.Graphics!.GetHdc();

                STRUCT_FORMATRANGE fmtRange;
                fmtRange.chrg.cpMin = charFrom;
                fmtRange.chrg.cpMax = charTo;
                fmtRange.hdc = hdc;
                fmtRange.hdcTarget = hdc;
                fmtRange.rc = rc;
                fmtRange.rcPage = rcPage;

                // Send the EM_FORMATRANGE message to the RichTextBox
                IntPtr res = SendMessage(this.Handle, EM_FORMATRANGE, measureOnly ? IntPtr.Zero : (IntPtr)1, ref fmtRange);

                e.Graphics.ReleaseHdc(hdc);

                return res.ToInt32();
            }

            public void FormatRangeDone()
            {
                SendMessage(this.Handle, EM_FORMATRANGE, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}