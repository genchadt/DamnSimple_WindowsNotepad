using DamnSimple_WindowsNotepad;
using System;
using System.Windows.Forms;

namespace XPNotepad
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // improved visual styles for high DPI screens
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NotepadForm());
        }

        // Import for DPI awareness to prevent blurry text on scaling > 100%
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}