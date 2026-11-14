using System.Text.Json;

namespace DBZKit
{
    /// <summary>
    /// Preferences kept between runs, in %LOCALAPPDATA%\DBZKit\settings.json. A missing or unreadable file just means the defaults.
    /// </summary>
    internal sealed class AppSettings
    {
        /// <summary>Show each asset's ROM address in the lists (for working alongside IDA). Off by default.</summary>
        public bool ShowAddresses { get; set; }

        /// <summary>The last ROM opened, offered again by File > Open Recent.</summary>
        public string? LastRom { get; set; }

        private static readonly string File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DBZKit", "settings.json");

        public static AppSettings Current { get; } = Load();

        /// <summary>Raised after a setting changes, so open pages can redraw.</summary>
        public static event System.Action? Changed;

        private static AppSettings Load()
        {
            try { return System.IO.File.Exists(File) ? JsonSerializer.Deserialize<AppSettings>(System.IO.File.ReadAllText(File)) ?? new() : new(); }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File)!);
                System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* not saved this time; still works */ }
            Changed?.Invoke();
        }
    }
}
