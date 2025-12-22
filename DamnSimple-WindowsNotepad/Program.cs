using DamnSimple_WindowsNotepad;
using System;
using System.Windows.Forms;

namespace XPNotepad
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NotepadForm());
        }
    }
}