#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Defines the syntax highlighting modes available in the text box.
    /// </summary>
    public enum SyntaxMode { None, Logs, Config }

    /// <summary>
    /// A RichTextBox control optimized for performance and custom rendering.
    /// It incorporates debounced syntax highlighting, smooth scrolling control, and direct Win32 API integration for flicker-free updates and printing.
    /// </summary>
    public class CustomRichTextBox : RichTextBox
    {
        /// <summary>
        /// Gets or sets an action to be executed when the control's handle is created.
        /// This provides a hook for post-initialization logic without subclassing.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action? HandleCreatedAction { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the default Windows smooth scrolling behavior.
        /// When false, the control reverts to a line-by-line scroll, which can feel more responsive on some systems.
        /// </summary>
        [DefaultValue(true)]
        public bool EnableSmoothScrolling { get; set; } = true;

        // --- Syntax Highlighting Properties ---

        // FIX: Hidden prevents "does not configure code serialization" warnings
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        private bool _enableSyntaxHighlighting = false;

        /// <summary>
        /// Gets or sets a value indicating whether syntax highlighting is active.
        /// Setting this property will trigger a full re-highlight or clear all formatting.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the current syntax mode (e.g., Logs, Config).
        /// Changing the mode will trigger a re-highlight if syntax highlighting is enabled.
        /// </summary>
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

        /// <summary>
        /// Gets or sets a value indicating whether dark mode is enabled.
        /// This will swap the active color palette and trigger a re-highlight.
        /// </summary>
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
        private readonly System.Windows.Forms.Timer _debounceTimer;

        // --- P/Invoke Constants ---
        private const int WM_USER = 0x0400;
        private const int EM_FORMATRANGE = WM_USER + 57;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int EM_LINESCROLL = 0x00B6;
        private const int WM_SETREDRAW = 0x000B;
        private const double TWIPS_PER_INCH = 1440.0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref StructFormatRange lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct PageRectangle { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct CharacterRange { public int CpMin; public int CpMax; }
        [StructLayout(LayoutKind.Sequential)]
        private struct StructFormatRange { public IntPtr hdc; public IntPtr hdcTarget; public PageRectangle rc; public PageRectangle rcPage; public CharacterRange chrg; }

        /// <summary>
        /// Defines a syntax highlighting rule, mapping a regular expression to a color.
        /// This struct is readonly to enforce immutability, ensuring that rules are defined at initialization and not modified at runtime.
        /// </summary>
        private readonly struct HighlightingRule
        {
            public readonly Regex Expression;
            public readonly Color Color;

            public HighlightingRule(Regex expression, Color color)
            {
                Expression = expression;
                Color = color;
            }
        }

        private Dictionary<SyntaxMode, HighlightingRule[]> _lightModeRules;
        private Dictionary<SyntaxMode, HighlightingRule[]> _darkModeRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomRichTextBox"/> class.
        /// Sets up the debounce timer for syntax highlighting and pre-compiles regex rules.
        /// </summary>
        public CustomRichTextBox()
        {
            // FIX: Explicit instantiation
            _debounceTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ApplySyntaxHighlighting();
            };

            InitializeHighlightingRules();
        }

        /// <summary>
        /// Initializes and pre-compiles all regular expressions for syntax highlighting.
        /// @note This is a critical performance optimization. By using RegexOptions.Compiled, the regex engine converts patterns to MSIL at startup.
        /// This shifts the high cost of parsing from the text-changed event to the application's initialization phase, minimizing input latency.
        /// </summary>
        private void InitializeHighlightingRules()
        {
            // Pre-compile Regex for performance. This shifts the parsing cost to initialization.
            const RegexOptions options = RegexOptions.Compiled | RegexOptions.Multiline;

            _lightModeRules = new Dictionary<SyntaxMode, HighlightingRule[]>
            {
                [SyntaxMode.Config] = new[]
                {
                    new HighlightingRule(new Regex("\".*?\"", options), Color.Maroon),
                    new HighlightingRule(new Regex(@"(#|;|//).*?$", options), Color.Green),
                    new HighlightingRule(new Regex(@"^[\w\s\-]+(?=\=|:)", options), Color.DarkBlue)
                },
                [SyntaxMode.Logs] = new[]
                {
                    new HighlightingRule(new Regex(@"\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}", options), Color.DarkBlue),
                    new HighlightingRule(new Regex(@"\b(ERROR|FATAL|CRITICAL)\b", options), Color.Red),
                    new HighlightingRule(new Regex(@"\b(WARN|WARNING)\b", options), Color.DarkOrange),
                    new HighlightingRule(new Regex(@"\b(INFO|DEBUG|TRACE)\b", options), Color.Blue),
                    new HighlightingRule(new Regex(@"([a-zA-Z]:\\|/)[a-zA-Z0-9_./\\]+", options), Color.Maroon)
                }
            };

            _darkModeRules = new Dictionary<SyntaxMode, HighlightingRule[]>
            {
                [SyntaxMode.Config] = new[]
                {
                    new HighlightingRule(new Regex("\".*?\"", options), Color.LightGoldenrodYellow),
                    new HighlightingRule(new Regex(@"(#|;|//).*?$", options), Color.LightGreen),
                    new HighlightingRule(new Regex(@"^[\w\s\-]+(?=\=|:)", options), Color.LightSkyBlue)
                },
                [SyntaxMode.Logs] = new[]
                {
                    new HighlightingRule(new Regex(@"\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}", options), Color.LightSkyBlue),
                    new HighlightingRule(new Regex(@"\b(ERROR|FATAL|CRITICAL)\b", options), Color.Salmon),
                    new HighlightingRule(new Regex(@"\b(WARN|WARNING)\b", options), Color.Orange),
                    new HighlightingRule(new Regex(@"\b(INFO|DEBUG|TRACE)\b", options), Color.Cyan),
                    new HighlightingRule(new Regex(@"([a-zA-Z]:\\|/)[a-zA-Z0-9_./\\]+", options), Color.LightGoldenrodYellow)
                }
            };
        }

        /// <summary>
        /// Overrides the base OnTextChanged method to trigger debounced syntax highlighting.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_enableSyntaxHighlighting && _syntaxMode != SyntaxMode.None)
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        /// <summary>
        /// Overrides the base OnHandleCreated method to invoke the custom handle creation action.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            HandleCreatedAction?.Invoke();
        }

        /// <summary>
        /// Intercepts Windows messages to implement custom scrolling behavior.
        /// @note This method directly handles the WM_MOUSEWHEEL message to bypass the default smooth-scrolling logic,
        /// which can introduce perceived latency. By sending EM_LINESCROLL messages, we achieve a deterministic, line-based scroll.
        /// </summary>
        /// <param name="m">The Windows Message to process.</param>
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
        /// Formats a range of text for a printing device.
        /// @note This is a wrapper around the EM_FORMATRANGE message, which allows the RichTextBox control to render its content
        /// to a device context (DC) such as a printer. This is the most efficient way to print, as it leverages the control's
        /// own layout and rendering engine.
        /// </summary>
        /// <param name="measureOnly">If true, measures but does not render the text.</param>
        /// <param name="e">Print page event arguments containing the graphics context and margins.</param>
        /// <param name="charFrom">The starting character index to format.</param>
        /// <param name="charTo">The ending character index to format.</param>
        /// <returns>The index of the last character that fits on the page.</returns>
        public int FormatRange(bool measureOnly, PrintPageEventArgs e, int charFrom, int charTo)
        {
            if (e.Graphics == null) return 0;

            // Convert GDI+ measurements to TWIPS (1/1440 inch), the unit used by RichTextBox for printing.
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

            // The 'wParam' parameter determines whether to render (1) or measure (0).
            IntPtr res = SendMessage(this.Handle, EM_FORMATRANGE, measureOnly ? IntPtr.Zero : (IntPtr)1, ref fmtRange);

            e.Graphics.ReleaseHdc(hdc);

            return res.ToInt32();
        }

        /// <summary>
        /// Releases the device context and formatting information held by the RichTextBox after printing.
        /// @note This must be called after a printing job is complete to free GDI resources.
        /// </summary>
        public void FormatRangeDone()
        {
            SendMessage(this.Handle, EM_FORMATRANGE, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Triggers the syntax highlighting process, marshalling the call to the UI thread if necessary.
        /// </summary>
        private void TriggerSyntaxHighlighting()
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(ApplySyntaxHighlighting));
            else
                ApplySyntaxHighlighting();
        }

        /// <summary>
        /// Clears all custom color formatting, resetting the text to the default foreground color.
        /// </summary>
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

        /// <summary>
        /// Applies syntax highlighting rules to the entire document based on the current mode and theme.
        /// @note This method operates on the entire text. For very large documents, this can be a bottleneck.
        /// It is optimized by wrapping the updates in BeginUpdate/EndUpdate to prevent UI flicker.
        /// </summary>
        private void ApplySyntaxHighlighting()
        {
            if (!_enableSyntaxHighlighting || _syntaxMode == SyntaxMode.None || string.IsNullOrWhiteSpace(this.Text)) return;

            int originalIndex = this.SelectionStart;
            int originalLength = this.SelectionLength;

            BeginUpdate();

            // 1. Reset all text to the default color in a single operation.
            this.SelectAll();
            this.SelectionColor = _isDarkMode ? Color.White : Color.Black;

            // 2. Apply rules.
            var ruleset = _isDarkMode ? _darkModeRules : _lightModeRules;
            if (ruleset.TryGetValue(_syntaxMode, out var rules))
            {
                string text = this.Text;
                foreach (var rule in rules)
                {
                    HighlightMatches(text, rule.Expression, rule.Color);
                }
            }

            // 3. Restore original selection.
            this.Select(originalIndex, originalLength);
            EndUpdate();
        }

        /// <summary>
        /// Finds all matches for a given regex and applies the specified color.
        /// </summary>
        /// <param name="text">The text to search within.</param>
        /// <param name="regex">The compiled regular expression to match.</param>
        /// <param name="color">The color to apply to matches.</param>
        private void HighlightMatches(string text, Regex regex, Color color)
        {
            // The Regex.Matches method is highly optimized internally.
            MatchCollection matches = regex.Matches(text);
            foreach (Match m in matches)
            {
                this.Select(m.Index, m.Length);
                this.SelectionColor = color;
            }
        }

        /// <summary>
        /// Suspends the control's painting to prevent flicker during bulk updates.
        /// @note This method sends the WM_SETREDRAW message with wParam=0. This is a classic Win32 optimization that tells the window
        /// to stop processing paint messages. It is more efficient than manually managing invalidation rectangles for bulk operations.
        /// </summary>
        public void BeginUpdate()
        {
            // P/Invoke to suspend control redrawing, preventing flicker during bulk updates.
            SendMessage(this.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Resumes the control's painting and forces an immediate repaint.
        /// @note This sends WM_SETREDRAW with wParam=1 and then calls Invalidate to force a WM_PAINT message,
        /// ensuring all batched changes are drawn at once.
        /// </summary>
        public void EndUpdate()
        {
            // Re-enable redrawing and force an immediate repaint.
            SendMessage(this.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            this.Invalidate();
        }
    }
}