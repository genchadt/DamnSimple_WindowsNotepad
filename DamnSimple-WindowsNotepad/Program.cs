using DamnSimple_WindowsNotepad;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Defines the application entry point and core process settings.
    /// </summary>
    /// <remarks>
    /// This class is static and serves as a lightweight bootstrap. It contains no
    /// instance data, ensuring a minimal memory footprint before the main application
    /// loop is initialized.
    /// </remarks>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// This method orchestrates the initial setup of the application environment.
        /// It configures process-level settings for DPI awareness and visual rendering
        /// before handing control to the Windows Forms message loop.
        /// </summary>
        /// <remarks>
        /// The [STAThread] attribute is critical. It configures the COM threading model
        /// for the main thread to be a Single-Threaded Apartment. This is a hard
        /// requirement for interoperability with many Windows components, including
        /// the common dialogs and clipboard, preventing race conditions at the OS level.
        /// </remarks>
        [STAThread]
        static void Main()
        {
            // Set the process to be DPI-aware. This must be done before any UI elements
            // are created to ensure proper scaling on high-DPI displays.
            SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NotepadForm());
        }

        /// <summary>
        /// P/Invoke declaration for the native Win32 SetProcessDPIAware function.
        /// </summary>
        /// <returns>Non-zero if the function succeeds, zero if it fails.</returns>
        /// <remarks>
        /// This is a direct call into unmanaged user32.dll. It is used for its
        /// low-level control over process behavior, bypassing potential framework
        /// abstractions. Incorrectly invoking this can lead to rendering artifacts.
        /// </remarks>
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}