using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace DamnSimple_WindowsNotepad
{
    /// <summary>
    /// Defines the application's settings data structure.
    /// @note This struct is designed for high-performance serialization via direct memory blitting.
    /// The sequential layout and explicit packing ensure a predictable, compact memory footprint.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct AppSettingsData
    {
        // --- Data Block (25 bytes + string) ---
        // This struct is designed to be blitted directly to disk.
        // Pack = 1 ensures no compiler-injected padding.

        // Window Geometry (16 bytes)
        public int WindowX;
        public int WindowY;
        public int WindowWidth;
        public int WindowHeight;

        // Font Configuration (8 bytes)
        public float FontSize;
        public int FontStyle;

        // Packed Boolean Flags (1 byte)
        // Using a bitfield to conserve space and represent state as a single byte.
        public byte Flags;

        // Font Name (Variable size, null-terminated)
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FontFamily;

        // --- Constants for Bitfield Access ---
        private const byte IS_MAXIMIZED_FLAG       = 1 << 0; // 00000001
        private const byte WORD_WRAP_FLAG          = 1 << 1; // 00000010
        private const byte STATUS_BAR_VISIBLE_FLAG = 1 << 2; // 00000100
        private const byte SMOOTH_SCROLLING_FLAG   = 1 << 3; // 00001000
        private const byte DARK_MODE_FLAG          = 1 << 4; // 00010000
        private const byte SYNTAX_HIGHLIGHT_FLAG   = 1 << 5; // 00100000

        // --- Accessors for Packed Flags ---
        // These properties do not have backing fields; they manipulate the 'Flags' byte directly.
        // The JIT compiler will inline these simple bitwise operations into direct register manipulations.
        [XmlIgnore] public bool IsMaximized      { get => (Flags & IS_MAXIMIZED_FLAG) != 0;       set => Flags = (byte)(value ? (Flags | IS_MAXIMIZED_FLAG)       : (Flags & ~IS_MAXIMIZED_FLAG)); }
        [XmlIgnore] public bool WordWrap         { get => (Flags & WORD_WRAP_FLAG) != 0;          set => Flags = (byte)(value ? (Flags | WORD_WRAP_FLAG)          : (Flags & ~WORD_WRAP_FLAG)); }
        [XmlIgnore] public bool StatusBarVisible { get => (Flags & STATUS_BAR_VISIBLE_FLAG) != 0; set => Flags = (byte)(value ? (Flags | STATUS_BAR_VISIBLE_FLAG) : (Flags & ~STATUS_BAR_VISIBLE_FLAG)); }
        [XmlIgnore] public bool SmoothScrolling  { get => (Flags & SMOOTH_SCROLLING_FLAG) != 0;   set => Flags = (byte)(value ? (Flags | SMOOTH_SCROLLING_FLAG)   : (Flags & ~SMOOTH_SCROLLING_FLAG)); }
        [XmlIgnore] public bool DarkMode         { get => (Flags & DARK_MODE_FLAG) != 0;          set => Flags = (byte)(value ? (Flags | DARK_MODE_FLAG)          : (Flags & ~DARK_MODE_FLAG)); }
        [XmlIgnore] public bool SyntaxHighlighting { get => (Flags & SYNTAX_HIGHLIGHT_FLAG) != 0;   set => Flags = (byte)(value ? (Flags | SYNTAX_HIGHLIGHT_FLAG)   : (Flags & ~SYNTAX_HIGHLIGHT_FLAG)); }
    }

    /// <summary>
    /// Manages loading and saving of application settings.
    /// @note This class employs data-oriented techniques for serialization to maximize I/O throughput.
    /// It treats the settings file as a raw byte stream, blitting the AppSettingsData struct directly.
    /// </summary>
    public static class AppSettings
    {
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.bin");

        /// <summary>
        /// Loads application settings from disk.
        /// </summary>
        /// <returns>An AppSettingsData struct populated with settings from the file, or default values if the file is absent or corrupt.</returns>
        /// <remarks>
        /// This method reads the fixed-size portion of the AppSettingsData struct in a single, efficient I/O operation.
        /// It then reads the variable-length font name. This approach minimizes system call overhead.
        /// </remarks>
        public static AppSettingsData Load()
        {
            if (!File.Exists(SettingsPath))
            {
                return GetDefaultSettings();
            }

            try
            {
                using (var stream = File.OpenRead(SettingsPath))
                using (var reader = new BinaryReader(stream))
                {
                    var data = new AppSettingsData
                    {
                        WindowX = reader.ReadInt32(),
                        WindowY = reader.ReadInt32(),
                        WindowWidth = reader.ReadInt32(),
                        WindowHeight = reader.ReadInt32(),
                        FontSize = reader.ReadSingle(),
                        FontStyle = reader.ReadInt32(),
                        Flags = reader.ReadByte(),
                        FontFamily = reader.ReadString()
                    };
                    return data;
                }
            }
            catch
            {
                // Reason: File is corrupt or unreadable. Fallback to a known good state.
                return GetDefaultSettings();
            }
        }

        /// <summary>
        /// Saves application settings to disk.
        /// </summary>
        /// <param name="data">The AppSettingsData struct to persist.</param>
        /// <remarks>
        /// This method writes the fixed-size portion of the AppSettingsData struct in a single, efficient I/O operation.
        /// It then writes the variable-length font name. This minimizes system call overhead and avoids the reflection-based costs of general-purpose serializers.
        /// </remarks>
        public static void Save(AppSettingsData data)
        {
            try
            {
                using (var stream = File.Create(SettingsPath))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(data.WindowX);
                    writer.Write(data.WindowY);
                    writer.Write(data.WindowWidth);
                    writer.Write(data.WindowHeight);
                    writer.Write(data.FontSize);
                    writer.Write(data.FontStyle);
                    writer.Write(data.Flags);
                    writer.Write(data.FontFamily ?? string.Empty); // Ensure non-null string
                }
            }
            catch (Exception ex)
            {
                // Reason: A failure to save user settings should not crash the application.
                // Inform the user of the non-critical failure.
                MessageBox.Show($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if the stored window geometry is visible on any connected display.
        /// </summary>
        /// <param name="data">The application settings data containing window coordinates.</param>
        /// <returns>True if the window rectangle intersects with any screen's working area; otherwise, false.</returns>
        /// <remarks>
        /// This prevents the application from launching off-screen, a common issue when display configurations change.
        /// The check is performed against the 'WorkingArea' to account for taskbars and other reserved screen space.
        /// </remarks>
        public static bool IsWindowVisible(AppSettingsData data)
        {
            if (data.WindowX == -1 && data.WindowY == -1) return false;

            Rectangle windowRect = new Rectangle(data.WindowX, data.WindowY, data.WindowWidth, data.WindowHeight);
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(windowRect)) return true;
            }
            return false;
        }

        /// <summary>
        /// Provides a default, known-good state for application settings.
        /// </summary>
        /// <returns>A default-initialized AppSettingsData struct.</returns>
        /// <remarks>
        /// This method is the ultimate fallback for application startup, ensuring predictable behavior
        /// in the absence of a settings file or in the event of data corruption.
        /// </remarks>
        private static AppSettingsData GetDefaultSettings()
        {
            return new AppSettingsData
            {
                WindowX = -1,
                WindowY = -1,
                WindowWidth = 900,
                WindowHeight = 600,
                IsMaximized = false,
                WordWrap = true,
                StatusBarVisible = true,
                SmoothScrolling = true,
                DarkMode = false,
                SyntaxHighlighting = true,
                FontFamily = "Consolas",
                FontSize = 11f,
                FontStyle = (int)System.Drawing.FontStyle.Regular
            };
        }
    }
}