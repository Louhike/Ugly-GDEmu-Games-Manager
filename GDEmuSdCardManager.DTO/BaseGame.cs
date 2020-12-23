using GDEmuSdCardManager.DTO.CDI;
using GDEmuSdCardManager.DTO.GDI;

namespace GDEmuSdCardManager.DTO
{
    public class BaseGame
    {
        public string FullPath { get; set; }
        public string Path { get; set; }
        public long? Size { get; set; }
        public string FormattedSize { get; set; }
        public string Hwid { get; set; }
        public string Maker { get; set; }
        public string Crc { get; set; }
        public string Disc { get; set; }
        public string FormattedDiscNumber { get { return Disc.Substring(6); } }
        public string Region { get; set; }
        public string Perif { get; set; }
        public string ProductN { get; set; }
        public string ProductV { get; set; }
        public string ReleaseDate { get; set; }
        public string BootFile { get; set; }
        public string Producer { get; set; }
        public string GameName { get; set; }
        public Gdi GdiInfo { get; set; }
        public Cdi CdiInfo { get; set; }
        public bool IsGdi { get; set; }
    }
}