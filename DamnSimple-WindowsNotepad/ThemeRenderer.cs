using System;
using System.Drawing;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Defines a static, pre-allocated color palette for the dark theme.
    /// </summary>
    /// <remarks>
    /// Storing colors as <c>static readonly</c> fields ensures that the color structures
    /// are created only once at type initialization. This avoids the overhead of
    /// <see cref="Color.FromArgb(int, int, int)"/> calls during render cycles,
    /// promoting stable performance by minimizing object allocations.
    /// </remarks>
    internal static class ThemeColors
    {
        public static readonly Color Background = Color.FromArgb(45, 45, 48);
        public static readonly Color Selection = Color.FromArgb(60, 60, 60);
        public static readonly Color Border = Color.FromArgb(100, 100, 100);
        public static readonly Color Text = Color.White;
        public static readonly Color TextDisabled = Color.Gray;
    }

    /// <summary>
    /// Provides a custom ToolStrip renderer to implement a dark theme.
    /// </summary>
    /// <remarks>
    /// This renderer optimizes drawing operations by caching GDI+ resources (<see cref="Brush"/> and <see cref="Pen"/>).
    /// This practice is critical for performance in UI rendering, as it prevents repeated resource allocation
    /// and disposal within frequently called paint events, thus reducing GC pressure and CPU overhead.
    /// The class implements <see cref="IDisposable"/> to ensure deterministic cleanup of these cached resources.
    /// </remarks>
    public sealed class DarkModeRenderer : ToolStripProfessionalRenderer, IDisposable
    {
        private readonly SolidBrush _selectionBackgroundBrush;
        private readonly Pen _selectionBorderPen;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DarkModeRenderer"/> class.
        /// </summary>
        public DarkModeRenderer() : base(new DarkModeColorTable())
        {
            _selectionBackgroundBrush = new SolidBrush(ThemeColors.Selection);
            _selectionBorderPen = new Pen(ThemeColors.Border);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="DarkModeRenderer"/> class.
        /// </summary>
        /// <remarks>
        /// The finalizer serves as a safeguard to release unmanaged GDI+ resources
        /// in case <see cref="Dispose()"/> is not explicitly called.
        /// </remarks>
        ~DarkModeRenderer()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged and, optionally, managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _selectionBackgroundBrush.Dispose();
                    _selectionBorderPen.Dispose();
                }
                _disposed = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
                e.Graphics.FillRectangle(_selectionBackgroundBrush, rc);
                e.Graphics.DrawRectangle(_selectionBorderPen, 0, 0, rc.Width - 1, rc.Height - 1);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        /// <inheritdoc/>
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? ThemeColors.Text : ThemeColors.TextDisabled;
            base.OnRenderItemText(e);
        }

        /// <inheritdoc/>
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? ThemeColors.Text : ThemeColors.TextDisabled;
            base.OnRenderArrow(e);
        }
    }

    /// <summary>
    /// Provides the dark theme color palette to the <see cref="ToolStripProfessionalRenderer"/>
    /// </summary>
    /// <remarks>
    /// This class overrides properties of <see cref="ProfessionalColorTable"/> to supply
    /// specific colors from the static <see cref="ThemeColors"/> palette. This flattens
    /// gradients and defines a consistent, solid-color dark appearance.
    /// </remarks>
    public sealed class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => ThemeColors.Border;
        public override Color MenuItemSelected => ThemeColors.Selection;
        public override Color MenuItemBorder => ThemeColors.Border;
        public override Color MenuStripGradientBegin => ThemeColors.Background;
        public override Color MenuStripGradientEnd => ThemeColors.Background;
        public override Color ImageMarginGradientBegin => ThemeColors.Background;
        public override Color ImageMarginGradientMiddle => ThemeColors.Background;
        public override Color ImageMarginGradientEnd => ThemeColors.Background;
        public override Color ToolStripDropDownBackground => ThemeColors.Background;
        public override Color MenuItemSelectedGradientBegin => ThemeColors.Selection;
        public override Color MenuItemSelectedGradientEnd => ThemeColors.Selection;
        public override Color MenuItemPressedGradientBegin => ThemeColors.Background;
        public override Color MenuItemPressedGradientMiddle => ThemeColors.Background;
        public override Color MenuItemPressedGradientEnd => ThemeColors.Background;
    }
}