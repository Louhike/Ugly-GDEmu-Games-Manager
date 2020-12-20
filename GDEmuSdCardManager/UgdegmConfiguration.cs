using System.IO;
using System.Text.Json;

namespace GDEmuSdCardManager
{
    /// <summary>
    /// Project configuration loaded from JSON
    /// </summary>
    public class UgdegmConfiguration
    {
        public string PcDefaultPath { get; set; } = @"F:\Roms\Sega - Dreamcast";
        public string SdDefaultDrive { get; set; } = @"H:\";
        public string VersionUrl { get; set; } = "https://raw.githubusercontent.com/Louhike/Ugly-GDEmu-Games-Manager/master/GDEmuSdCardManager/VERSION";
        public string ReleasesUrl { get; set; } = "https://github.com/Louhike/Ugly-GDEmu-Games-Manager/releases";
        public string IssuesUrl { get; set; } = "https://github.com/Louhike/Ugly-GDEmu-Games-Manager/issues";

        /// <summary>
        /// Load the configuration from the JSON file
        /// </summary>
        /// <param name="jsonFilePath"></param>
        /// <returns></returns>
        public static UgdegmConfiguration LoadConfiguration(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    return JsonSerializer.Deserialize<UgdegmConfiguration>(File.ReadAllText(jsonFilePath));
                }
                catch
                {
                    return new UgdegmConfiguration();
                }
            }
            else
            {
                return new UgdegmConfiguration();
            }
        }

        /// <summary>
        /// Save the configuration in the JSON file
        /// </summary>
        /// <param name="jsonFilePath"></param>
        public void Save(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                File.Create(jsonFilePath).Close();
            }

            File.WriteAllText(jsonFilePath, JsonSerializer.Serialize<UgdegmConfiguration>(this));
        }
    }
}