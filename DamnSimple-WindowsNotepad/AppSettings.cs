using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace DamnSimple_WindowsNotepad
{
    public class AppSettings
    {
        // --- Properties with Defaults ---
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 900;
        public int WindowHeight { get; set; } = 600;
        public bool IsMaximized { get; set; } = false;

        public bool WordWrap { get; set; } = true;
        public bool StatusBarVisible { get; set; } = true;
        public bool SmoothScrolling { get; set; } = true;

        // Font Settings
        public string FontFamily { get; set; } = "Consolas";
        public float FontSize { get; set; } = 11f;
        public int FontStyle { get; set; } = (int)System.Drawing.FontStyle.Regular;

        // --- Logic ---
        // We use XML now so you don't need System.Text.Json
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                    using (StreamReader reader = new StreamReader(SettingsPath))
                    {
                        return (AppSettings)serializer.Deserialize(reader);
                    }
                }
            }
            catch
            {
                // Verify write permissions or handle corrupt XML silently by falling back to defaults
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (StreamWriter writer = new StreamWriter(SettingsPath))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}");
            }
        }

        public bool IsWindowVisible()
        {
            if (WindowX == -1 && WindowY == -1) return false;

            Rectangle windowRect = new Rectangle(WindowX, WindowY, WindowWidth, WindowHeight);
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(windowRect)) return true;
            }
            return false;
        }
    }
}