using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Represents the application's "About" dialog.
    /// </summary>
    /// <remarks>
    /// This implementation minimizes GDI object overhead and maximizes rendering performance
    /// by employing a data-oriented design for UI elements and custom drawing. This approach
    /// reduces framework abstractions, giving us more direct control over memory layout and rendering,
    /// which is critical for achieving low-latency UI updates. The background animation is designed
    /// with mechanical sympathy in mind, using cache-friendly data structures and a tight update loop.
    /// </remarks>
    public class AboutDialog : Form
    {
        /// <summary>
        /// Sets window attributes for non-client areas.
        /// </summary>
        /// <param name="hwnd">The handle to the window.</param>
        /// <param name="attr">The DWM window attribute to set.</param>
        /// <param name="attrValue">A pointer to the value of the attribute.</param>
        /// <param name="attrSize">The size of the attribute value.</param>
        /// <returns>An HRESULT indicating success or failure.</returns>
        /// <remarks>
        /// This P/Invoke call directly manipulates the Desktop Window Manager (DWM)
        /// to set the dark mode attribute for the non-client area (e.g., the title bar).
        /// This avoids framework abstractions for a direct, low-level system call, ensuring
        /// minimal overhead and immediate visual consistency with the application's theme.
        /// </remarks>
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>
        /// Defines a UI element using a data-oriented layout.
        /// </summary>
        /// <remarks>
        /// This struct aggregates all data required to render a static UI element.
        /// By laying out the data contiguously, we improve cache locality. When the render loop
        /// accesses an element's `Bounds`, its other properties (`Text`, `Font`, `Brush`) are
        /// likely to be in the same cache line, reducing memory access latency.
        /// </remarks>
        private struct UIElement
        {
            public Rectangle Bounds;
            public string Text;
            public Font Font;
            public Brush Brush;
            public StringFormat Format;
        }

        /// <summary>
        /// Defines a particle for the background starfield animation.
        /// </summary>
        /// <remarks>
        /// This struct follows a data-oriented design, grouping all state for a single star.
        /// The `_stars` array is an Array of Structures (AoS), which is efficient for this
        /// scenario. During the animation loop, iterating through this array results in
        /// predictable, linear memory access, which allows the CPU to effectively prefetch
        /// data for subsequent stars, keeping the execution pipeline full.
        /// </remarks>
        private struct Star
        {
            public PointF Position;
            public PointF Velocity;
            public Brush Brush;
            public float Size;
        }

        private readonly UIElement[] _elements;
        private readonly Button _okButton; // A real button for accessibility and standard dialog behavior.

        // Animation state
        private const int StarCount = 40;
        private readonly Star[] _stars;
        private readonly Brush[] _starBrushes;
        private readonly System.Windows.Forms.Timer _animationTimer;
        private readonly Random _random = new Random();

        /// <summary>
        /// Initializes a new instance of the <see cref="AboutDialog"/> class.
        /// </summary>
        /// <param name="isDarkMode">A flag indicating whether to apply the dark theme.</param>
        public AboutDialog(bool isDarkMode)
        {
            this.Text = "About Notepad";
            this.Size = new Size(400, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.AutoScaleMode = AutoScaleMode.Font;
            // Double-buffering prevents flicker by drawing to an off-screen buffer first,
            // then blitting the final image to the screen in one operation. This is a classic
            // technique to achieve smooth rendering by avoiding tearing.
            this.DoubleBuffered = true;

            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(300, 170),
                Width = 75,
                DialogResult = DialogResult.OK
            };

            this.Controls.Add(_okButton);
            this.AcceptButton = _okButton;

            // Pre-allocate all GDI objects and element data to avoid allocations during the render loop.
            _elements = new UIElement[]
            {
                new UIElement
                {
                    Bounds = new Rectangle(20, 20, 360, 30),
                    Text = "Damn Simple Notepad",
                    Font = new Font("Segoe UI", 12, FontStyle.Bold)
                },
                new UIElement
                {
                    Bounds = new Rectangle(20, 60, 360, 100),
                    Text = "Version 0.0.1\n© 2025 GenChadT\n\nThis product is licensed under the terms of the \nGNU General Public License v3.0." ,
                    Font = new Font("Segoe UI", 9)
                }
            };

            // Animation setup
            _starBrushes = new Brush[]
            {
                new SolidBrush(Color.FromArgb(150, 255, 255, 255)), // White
                new SolidBrush(Color.FromArgb(150, 255, 255, 220)), // Yellow
                new SolidBrush(Color.FromArgb(150, 220, 220, 255)), // Blue
            };
            _stars = new Star[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                _stars[i] = new Star
                {
                    Position = new PointF((float)(_random.NextDouble() * this.ClientSize.Width), (float)(_random.NextDouble() * this.ClientSize.Height)),
                    Velocity = new PointF((float)(_random.NextDouble() - 0.5) * 2, (float)(_random.NextDouble() - 0.5) * 2),
                    Brush = _starBrushes[_random.Next(_starBrushes.Length)],
                    Size = (float)(_random.NextDouble() * 1.5 + 1)
                };
            }

            ApplyTheme(isDarkMode);

            _animationTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
            _animationTimer.Tick += (sender, e) => this.Invalidate(true);
            _animationTimer.Start();
        }

        /// <summary>
        /// Paints the background and content of the control.
        /// </summary>
        /// <param name="e">A <see cref="PaintEventArgs"/> that contains the event data.</param>
        /// <remarks>
        /// This method orchestrates the rendering pipeline. The starfield animation is updated
        /// and drawn first, followed by the static UI elements. This layering ensures that
        /// dynamic elements do not have to be redrawn over static content, optimizing the blit.
        /// </remarks>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Update and render background starfield first
            UpdateAnimation(this.ClientSize);
            foreach (var star in _stars)
            {
                g.FillEllipse(star.Brush, star.Position.X, star.Position.Y, star.Size, star.Size);
            }

            // Render static elements on top
            foreach (var element in _elements)
            {
                g.DrawString(element.Text, element.Font, element.Brush, element.Bounds, element.Format);
            }
        }

        /// <summary>
        /// Updates the position of each star for the animation.
        /// </summary>
        /// <param name="clientSize">The size of the drawable client area.</param>
        /// <remarks>
        /// This is the hot loop for the animation. It iterates through the `_stars` array,
        /// updating positions based on velocity. The wrap-around logic creates a seamless,
        /// continuous starfield. The conditional checks are simple and highly predictable,
        /// minimizing the risk of branch misprediction and pipeline stalls.
        /// </remarks>
        private void UpdateAnimation(SizeF clientSize)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].Position.X += _stars[i].Velocity.X;
                _stars[i].Position.Y += _stars[i].Velocity.Y;

                // This wrap-around logic keeps the instruction pipeline saturated by using
                // simple, predictable branches.
                if (_stars[i].Position.X < -_stars[i].Size) _stars[i].Position.X = clientSize.Width + _stars[i].Size;
                if (_stars[i].Position.X > clientSize.Width + _stars[i].Size) _stars[i].Position.X = -_stars[i].Size;
                if (_stars[i].Position.Y < -_stars[i].Size) _stars[i].Position.Y = clientSize.Height + _stars[i].Size;
                if (_stars[i].Position.Y > clientSize.Height + _stars[i].Size) _stars[i].Position.Y = -_stars[i].Size;
            }
        }

        /// <summary>
        /// Applies the visual theme to the dialog and its controls.
        /// </summary>
        /// <param name="dark">A flag indicating if dark mode should be enabled.</param>
        /// <remarks>
        /// The ternary operator `dark ? 1 : 0` is a form of branch minimization that can often
        /// be compiled down to a conditional move (cmov) instruction on x86 architectures,
        /// avoiding a pipeline-flushing jump. This method directly sets colors and properties
        /// to align with the selected theme, ensuring a consistent look and feel.
        /// </remarks>
        private void ApplyTheme(bool dark)
        {
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));

            this.BackColor = dark ? Color.FromArgb(28, 28, 28) : SystemColors.Control;
            var textColor = dark ? Color.White : SystemColors.ControlText;

            for (int i = 0; i < _elements.Length; i++)
            {
                _elements[i].Brush = new SolidBrush(textColor);
                _elements[i].Format = new StringFormat();
            }

            // Theme the single real control
            _okButton.ForeColor = textColor;
            _okButton.BackColor = dark ? Color.FromArgb(63, 63, 70) : SystemColors.ButtonFace;
            _okButton.FlatStyle = FlatStyle.Flat;
            _okButton.FlatAppearance.BorderColor = dark ? Color.FromArgb(80, 80, 80) : SystemColors.ControlDark;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AboutDialog"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        /// <remarks>
        /// This method explicitly disposes of all allocated GDI objects. This is critical for
        // performance and stability, as it releases their unmanaged handles immediately rather
        // than waiting for the garbage collector's finalizer thread. Failure to do so can lead
        // to resource leaks and system instability.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
                foreach (var element in _elements)
                {
                    element.Font.Dispose();
                    element.Brush.Dispose();
                    element.Format.Dispose();
                }
                foreach (var brush in _starBrushes)
                {
                    brush.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}