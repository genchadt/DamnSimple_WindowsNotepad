using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing.Printing;

namespace DamnSimple_WindowsNotepad
{
    partial class NotepadForm
    {
        private IContainer components = null;

        // UI Controls declared here so they are accessible to NotepadForm.cs
        private MenuStrip menuStrip;
        private CustomRichTextBox txtContent;
        private StatusStrip statusStrip;

        // Menu Items referenced in code
        private ToolStripMenuItem wordWrapItem;
        private ToolStripMenuItem statusItem;
        private ToolStripMenuItem smoothScrollItem;

        // Status Labels
        private ToolStripStatusLabel lblCursorPos;
        private ToolStripStatusLabel lblZoom;
        private ToolStripStatusLabel lblEncoding;

        // Printing
        private PrintDocument printDocument;
        private PrintDialog printDialog;
        private PageSetupDialog pageSetupDialog;
        private PrintPreviewDialog printPreviewDialog;

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
            this.components = new Container();
            // Note: Implementation details are handled in the NotepadForm constructor 
            // as per the provided logic structure.
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Text = "Notepad";
        }
    }
}