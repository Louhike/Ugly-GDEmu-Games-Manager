using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDEmuSdCardManager.DTO.GDI
{
    public class DiscTrack
    {
        /// <summary>
        /// First column.
        /// </summary>
        public uint TrackNumber { get; set; }

        /// <summary>
        /// Location of extent. Second column
        /// </summary>
        public uint Lba { get; set; }

        /// <summary>
        /// Either 2048 or 2352. Third column.
        /// </summary>
        public int SectorSize { get; set; }

        /// <summary>
        /// Define the type of the file. Third column.
        /// 0 is audio (raw) and 4 is data (bin or iso).
        /// </summary>
        public byte TrackType { get; set; }

        /// <summary>
        /// Name of the file. Fourth column
        /// </summary>
        public string FileName { get; set; }

        public string FileExtension
        {
            get
            {
                return Path.GetExtension(FileName);
            }
        }

        public string StandardName
        {
            get
            {
                return "track" + TrackNumber.ToString("D2") + FileExtension;
            }
        }
    }
}
