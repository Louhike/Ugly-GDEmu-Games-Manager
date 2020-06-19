using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace GDEmuSdCardManager
{
    /// <summary>
    /// Project configuration loaded from JSON
    /// </summary>
    public class UGDEBConfiguration
    {
        public string PcDefaultPath { get; set; } = "F:\\Roms\\Sega - Dreamcast";
        public string SdDefaultDrive { get; set; } = "H:\\";

        /// <summary>
        /// Load the configuration from the JSON file
        /// </summary>
        /// <param name="jsonFilePath"></param>
        /// <returns></returns>
        public static UGDEBConfiguration LoadConfiguration(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    return JsonSerializer.Deserialize<UGDEBConfiguration>(File.ReadAllText(jsonFilePath));

                }
                catch
                {
                    return new UGDEBConfiguration();
                }
            }
            else
            {
                return new UGDEBConfiguration();
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

            File.WriteAllText(jsonFilePath, JsonSerializer.Serialize<UGDEBConfiguration>(this));
        }
    }
}