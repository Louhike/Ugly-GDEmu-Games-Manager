using System.Collections.Generic;
using System.IO;

namespace GDEmuSdCardManager.DTO.GDI
{
    public class Gdi
    {
        public int NumberOfTracks { get; set; }
        public List<DiscTrack> Tracks { get; set; }

        public void SaveTo(string path, bool renameTrackName)
        {
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine(NumberOfTracks);
                foreach (var track in Tracks)
                {
                    string trackName = renameTrackName ?
                        track.StandardName :
                        track.FileName;

                    sw.WriteLine(track.TrackNumber + " " + track.Lba + " " + track.TrackType + " " + track.SectorSize + " " + trackName + " 0");
                }
            }
        }

        public void RenameTrackFiles(string path)
        {
            foreach(var track in Tracks)
            {
                File.Move(Path.Combine(path, track.FileName), Path.Combine(path, track.StandardName));
            }
        }
    }
}