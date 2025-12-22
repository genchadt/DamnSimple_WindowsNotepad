#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

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
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Verify write permissions or handle corrupt JSON silently by falling back to defaults
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that the saved window position is actually visible on the current screens.
        /// Prevents the app from opening off-screen if monitors changed.
        /// </summary>
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