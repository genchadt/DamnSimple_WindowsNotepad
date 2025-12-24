#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    public enum SyntaxMode { None, Logs, Config }

    public class CustomRichTextBox : RichTextBox
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action? HandleCreatedAction { get; set; }

        [DefaultValue(true)]
        public bool EnableSmoothScrolling { get; set; } = true;

        // --- Syntax Highlighting Properties ---

        // FIX: Hidden prevents "does not configure code serialization" warnings
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        private bool _enableSyntaxHighlighting = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnableSyntaxHighlighting
        {
            get => _enableSyntaxHighlighting;
            set
            {
                _enableSyntaxHighlighting = value;
                if (value) TriggerSyntaxHighlighting();
                else ClearFormatting();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        private SyntaxMode _syntaxMode = SyntaxMode.None;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SyntaxMode CurrentSyntaxMode
        {
            get => _syntaxMode;
            set
            {
                _syntaxMode = value;
                if (_enableSyntaxHighlighting) TriggerSyntaxHighlighting();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        private bool _isDarkMode = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                _isDarkMode = value;
                if (_enableSyntaxHighlighting) TriggerSyntaxHighlighting();
            }
        }

        // FIX: Explicitly use System.Windows.Forms.Timer to avoid ambiguity
        private System.Windows.Forms.Timer _debounceTimer;

        // --- P/Invoke Constants ---
        private const int WM_USER = 0x0400;
        private const int EM_FORMATRANGE = WM_USER + 57;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int EM_LINESCROLL = 0x00B6;
        private const int WM_SETREDRAW = 0x000B;
        private const double TWIPS_PER_INCH = 1440.0;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref StructFormatRange lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct PageRectangle { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct CharacterRange { public int CpMin; public int CpMax; }
        [StructLayout(LayoutKind.Sequential)]
        private struct StructFormatRange { public IntPtr hdc; public IntPtr hdcTarget; public PageRectangle rc; public PageRectangle rcPage; public CharacterRange chrg; }

        public CustomRichTextBox()
        {
            // FIX: Explicit instantiation
            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = 500;
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ApplySyntaxHighlighting();
            };
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_enableSyntaxHighlighting && _syntaxMode != SyntaxMode.None)
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
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

        public int FormatRange(bool measureOnly, PrintPageEventArgs e, int charFrom, int charTo)
        {
            if (e.Graphics == null) return 0;

            int w = (int)(e.MarginBounds.Width / 100.0 * TWIPS_PER_INCH);
            int h = (int)(e.MarginBounds.Height / 100.0 * TWIPS_PER_INCH);
            int l = (int)(e.MarginBounds.Left / 100.0 * TWIPS_PER_INCH);
            int t = (int)(e.MarginBounds.Top / 100.0 * TWIPS_PER_INCH);

            PageRectangle rc = new PageRectangle { Left = l, Top = t, Right = l + w, Bottom = t + h };
            PageRectangle rcPage = new PageRectangle { Left = 0, Top = 0, Right = (int)(e.PageBounds.Width / 100.0 * TWIPS_PER_INCH), Bottom = (int)(e.PageBounds.Height / 100.0 * TWIPS_PER_INCH) };

            IntPtr hdc = e.Graphics.GetHdc();

            StructFormatRange fmtRange;
            fmtRange.chrg.CpMin = charFrom;
            fmtRange.chrg.CpMax = charTo;
            fmtRange.hdc = hdc;
            fmtRange.hdcTarget = hdc;
            fmtRange.rc = rc;
            fmtRange.rcPage = rcPage;

            IntPtr res = SendMessage(this.Handle, EM_FORMATRANGE, measureOnly ? IntPtr.Zero : (IntPtr)1, ref fmtRange);

            e.Graphics.ReleaseHdc(hdc);

            return res.ToInt32();
        }

        public void FormatRangeDone()
        {
            SendMessage(this.Handle, EM_FORMATRANGE, IntPtr.Zero, IntPtr.Zero);
        }

        private void TriggerSyntaxHighlighting()
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(ApplySyntaxHighlighting));
            else
                ApplySyntaxHighlighting();
        }

        private void ClearFormatting()
        {
            if (string.IsNullOrEmpty(this.Text)) return;

            int originalIndex = this.SelectionStart;
            int originalLength = this.SelectionLength;

            BeginUpdate();
            this.SelectAll();
            this.SelectionColor = _isDarkMode ? Color.White : Color.Black;
            this.Select(originalIndex, originalLength);
            EndUpdate();
        }

        private void ApplySyntaxHighlighting()
        {
            if (!_enableSyntaxHighlighting || _syntaxMode == SyntaxMode.None || string.IsNullOrWhiteSpace(this.Text)) return;

            int originalIndex = this.SelectionStart;
            int originalLength = this.SelectionLength;

            BeginUpdate();

            this.SelectAll();
            this.SelectionColor = _isDarkMode ? Color.White : Color.Black;

            Color colorString = _isDarkMode ? Color.LightGoldenrodYellow : Color.Maroon;
            Color colorKey = _isDarkMode ? Color.LightSkyBlue : Color.DarkBlue;
            Color colorComment = _isDarkMode ? Color.LightGreen : Color.Green;
            Color colorError = _isDarkMode ? Color.Salmon : Color.Red;
            Color colorInfo = _isDarkMode ? Color.Cyan : Color.Blue;
            Color colorWarn = _isDarkMode ? Color.Orange : Color.DarkOrange;

            string text = this.Text;

            if (_syntaxMode == SyntaxMode.Config)
            {
                HighlightMatches(text, "\".*?\"", colorString);
                HighlightMatches(text, @"(#|;|//).*?$", colorComment);
                HighlightMatches(text, @"^[\w\s\-]+(?=\=|:)", colorKey);
            }
            else if (_syntaxMode == SyntaxMode.Logs)
            {
                HighlightMatches(text, @"\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}", colorKey);
                HighlightMatches(text, @"\b(ERROR|FATAL|CRITICAL)\b", colorError);
                HighlightMatches(text, @"\b(WARN|WARNING)\b", colorWarn);
                HighlightMatches(text, @"\b(INFO|DEBUG|TRACE)\b", colorInfo);
                HighlightMatches(text, @"([a-zA-Z]:\\|/)[a-zA-Z0-9_./\\]+", colorString);
            }

            this.Select(originalIndex, originalLength);
            EndUpdate();
        }

        private void HighlightMatches(string text, string pattern, Color color)
        {
            try
            {
                MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.Multiline);
                foreach (Match m in matches)
                {
                    this.Select(m.Index, m.Length);
                    this.SelectionColor = color;
                }
            }
            catch { }
        }

        public void BeginUpdate()
        {
            SendMessage(this.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        public void EndUpdate()
        {
            SendMessage(this.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            this.Invalidate();
        }
    }
}