using System.Drawing;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Custom renderer for ToolStrips/MenuStrips to support Dark Mode aesthetics.
    /// </summary>
    public class DarkModeRenderer : ToolStripProfessionalRenderer
    {
        public DarkModeRenderer() : base(new DarkModeColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(60, 60, 60)))
                {
                    e.Graphics.FillRectangle(brush, rc);
                }
                using (Pen pen = new Pen(Color.FromArgb(100, 100, 100)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, rc.Width - 1, rc.Height - 1);
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }

    /// <summary>
    /// Defines the color palette for the Dark Mode renderer.
    /// </summary>
    public class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
        public override Color MenuItemBorder => Color.FromArgb(100, 100, 100);

        // Gradient overrides (Flattened to solid dark colors)
        public override Color MenuStripGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuStripGradientEnd => Color.FromArgb(45, 45, 48);

        public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 48);

        public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);

        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);

        public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuItemPressedGradientMiddle => Color.FromArgb(45, 45, 48);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(45, 45, 48);
    }
}