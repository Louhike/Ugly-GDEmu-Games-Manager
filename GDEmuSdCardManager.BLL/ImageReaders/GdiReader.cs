using GDEmuSdCardManager.BLL.Extensions;
using GDEmuSdCardManager.DTO;
using GDEmuSdCardManager.DTO.GDI;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GDEmuSdCardManager.BLL.ImageReaders
{
    public class GdiReader : BaseImageReader
    {
        public static Gdi GetGdiFromFile(string path)
        {
            var gdiContent = File.ReadAllLines(path);
            return GetGdiFromStringContent(gdiContent);
        }

        /// <summary>
        /// Extract game information from a folder
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public BaseGame ExtractGameData(string imagePath)
        {
            string folderPath = new FileInfo(imagePath).DirectoryName;
            var game = new BaseGame
            {
                FullPath = folderPath,
                Path = folderPath.Split(Path.DirectorySeparatorChar).Last(),
                Size = FileManager.GetDirectorySize(folderPath),
                FormattedSize = FileManager.GetDirectoryFormattedSize(folderPath)
            };

            game.GdiInfo = GetGdiFromFile(imagePath);
            game.IsGdi = true;
            var track3 = game.GdiInfo.Tracks.Single(t => t.TrackNumber == 3);
            if (track3.Lba != 45000)
            {
                throw new Exception("Bad track03.bin LBA");
            }

            bool isRawMode = track3.SectorSize == 2352; // 2352/RAW mode or 2048

            using (var fs = File.OpenRead(Path.Combine(folderPath, track3.FileName)))
            {
                if (isRawMode)
                {
                    // We ignore the first line
                    byte[] dummyBuffer = new byte[16];
                    fs.Read(dummyBuffer, 0, 16);
                }

                ReadGameInfoFromBinaryData(game, fs);
            }

            return game;
        }

        public BaseGame ExtractGameDataFromArchive(string archivePath, IArchive archive)
        {
            var archiveFileInfo = new FileInfo(archivePath);
            var game = new BaseGame
            {
                FullPath = archivePath,
                Path = archivePath.Split(Path.DirectorySeparatorChar).Last(),
                Size = archiveFileInfo.Length,
                FormattedSize = FileManager.FormatSize(archiveFileInfo.Length)
            };

            IArchiveEntry gdiEntry = ArchiveManager.RetreiveUniqueFileFromArchiveEndingWith(archive, ".gdi");

            if (gdiEntry != null)
            {
                string gdiContentInSingleLine;
                using (var ms = new MemoryStream())
                {
                    gdiEntry.WriteTo(ms);
                    gdiContentInSingleLine = Encoding.UTF8.GetString(ms.ToArray());
                }

                var gdiContent = new List<string>(
                        gdiContentInSingleLine.Split(new string[] { "\r\n", "\n" },
                        StringSplitOptions.RemoveEmptyEntries));

                game.GdiInfo = GetGdiFromStringContent(gdiContent);
                game.IsGdi = true;
                var track3 = game.GdiInfo.Tracks.Single(t => t.TrackNumber == 3);
                if (track3.Lba != 45000)
                {
                    throw new Exception("Bad track03.bin LBA");
                }

                bool isRawMode = track3.SectorSize == 2352; // 2352/RAW mode or 2048

                var track3Entry = ArchiveManager.RetreiveUniqueFileFromArchiveEndingWith(archive, track3.FileName);
                using (var track3Stream = track3Entry.OpenEntryStream())
                {
                    if (isRawMode)
                    {
                        // We ignore the first line
                        byte[] dummyBuffer = new byte[16];
                        track3Stream.Read(dummyBuffer, 0, 16);
                    }

                    ReadGameInfoFromBinaryData(game, track3Stream);
                }
            }

            return game;
        }

        private static Gdi GetGdiFromStringContent(IEnumerable<string> gdiContent)
        {
            gdiContent = gdiContent
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => l.RemoveSpacesInSuccession());

            var gdi = new Gdi();

            int numberOfTracks;
            if (!int.TryParse(gdiContent.First(), out numberOfTracks))
            {
                throw new FormatException("The GDI file should have the number of tracks in its first line.");
            }

            if (numberOfTracks != (gdiContent.Count() - 1))
            {
                throw new FormatException("The number of tracks defined in the GDI file should match the number in the first line.");
            }

            gdi.NumberOfTracks = numberOfTracks;

            gdi.Tracks = new List<DiscTrack>();
            foreach (var line in gdiContent.Skip(1))
            {
                var lineSplittedBySpace = line.Split(" ");
                var discTrack = new DiscTrack()
                {
                    TrackNumber = uint.Parse(lineSplittedBySpace.First()),
                    Lba = uint.Parse(lineSplittedBySpace.ElementAt(1)),
                    TrackType = byte.Parse(lineSplittedBySpace.ElementAt(2)),
                    SectorSize = int.Parse(lineSplittedBySpace.ElementAt(3)),
                    FileName = string.Join(" ", lineSplittedBySpace.Skip(4).SkipLast(1)).Replace("\"", string.Empty)
                };
                gdi.Tracks.Add(discTrack);
            }

            return gdi;
        }
    }
}