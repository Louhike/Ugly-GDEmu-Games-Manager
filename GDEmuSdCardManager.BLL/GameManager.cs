using GDEmuSdCardManager.BLL.Extensions;
using GDEmuSdCardManager.DTO;
using GDEmuSdCardManager.DTO.CDI;
using GDEmuSdCardManager.DTO.GDI;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GDEmuSdCardManager.BLL
{
    public static class GameManager
    {
        /// <summary>
        /// Extract game information from a PC folder
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static GameOnPc ExtractPcGameData(string folderPath)
        {
            var game = ExtractGameData(folderPath);
            return ConvertBaseGameToGameOnPc(game);
        }

        /// <summary>
        /// Extract game information from a PC archive file
        /// </summary>
        /// <param name="archivePath"></param>
        /// <param name="archive"></param>
        /// <returns></returns>
        public static GameOnPc ExtractPcGameDataFromArchive(string archivePath, IArchive archive)
        {
            var game = ConvertBaseGameToGameOnPc(ExtractGameDataFromArchive(archivePath, archive));
            if (game != null)
            {
                game.IsCompressed = true;
            }

            return game;
        }

        /// <summary>
        /// Convert a <see cref="BaseGame"/> instance to a <see cref="GameOnPc"/> instance
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private static GameOnPc ConvertBaseGameToGameOnPc(BaseGame game)
        {
            if (game == null)
            {
                return null;
            }

            return new GameOnPc
            {
                BootFile = game.BootFile,
                Crc = game.Crc,
                Disc = game.Disc,
                FormattedSize = game.FormattedSize,
                FullPath = game.FullPath,
                GameName = game.GameName,
                GdiInfo = game.GdiInfo,
                Hwid = game.Hwid,
                IsGdi = game.IsGdi,
                Maker = game.Maker,
                Path = game.Path,
                Perif = game.Perif,
                Producer = game.Producer,
                ProductN = game.ProductN,
                ProductV = game.ProductV,
                Region = game.Region,
                ReleaseDate = game.ReleaseDate
            };
        }

        public static GameOnSd ExtractSdGameData(string folderPath)
        {
            var game = ExtractGameData(folderPath);
            if (game == null)
            {
                return null;
            }

            return new GameOnSd
            {
                BootFile = game.BootFile,
                Crc = game.Crc,
                Disc = game.Disc,
                FormattedSize = game.FormattedSize,
                FullPath = game.FullPath,
                GameName = game.GameName,
                GdiInfo = game.GdiInfo,
                Hwid = game.Hwid,
                IsGdi = game.IsGdi,
                Maker = game.Maker,
                Path = game.Path,
                Perif = game.Perif,
                Producer = game.Producer,
                ProductN = game.ProductN,
                ProductV = game.ProductV,
                Region = game.Region,
                ReleaseDate = game.ReleaseDate
            };
        }

        /// <summary>
        /// Extract game information from a folder
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static BaseGame ExtractGameData(string folderPath)
        {
            var game = new BaseGame
            {
                FullPath = folderPath,
                Path = folderPath.Split(Path.DirectorySeparatorChar).Last(),
                Size = FileManager.GetDirectorySize(folderPath),
                FormattedSize = FileManager.GetDirectoryFormattedSize(folderPath)
            };

            string gdiPath = Directory.EnumerateFiles(folderPath).FirstOrDefault(f => System.IO.Path.GetExtension(f) == ".gdi");
            if (!string.IsNullOrEmpty(gdiPath))
            {
                game.GdiInfo = GetGdiFromFile(gdiPath);
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
            }
            else
            {
                string cdiPath = Directory.EnumerateFiles(folderPath).FirstOrDefault(f => System.IO.Path.GetExtension(f) == ".cdi");
                if (!string.IsNullOrEmpty(cdiPath))
                {
                    using (var fs = File.OpenRead(cdiPath))
                    {
                        game.CdiInfo = GetCdiFromFile(fs);
                        RetrieveTrack3DataFromCdiStream(game, fs);
                    }
                }
            }

            if (game.GameName == "GDMENU")
            {
                return null;
            }

            return game;
        }

        private static BaseGame ExtractGameDataFromArchive(string archivePath, IArchive archive)
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
                    //track3Entry.WriteTo(track3Stream);
                    if (isRawMode)
                    {
                        // We ignore the first line
                        byte[] dummyBuffer = new byte[16];
                        track3Stream.Read(dummyBuffer, 0, 16);
                    }

                    ReadGameInfoFromBinaryData(game, track3Stream);
                }
            }
            //else
            //{
            //    var cdiEntry = archive.Entries.FirstOrDefault(f => f.Key.EndsWith(".cdi", StringComparison.InvariantCultureIgnoreCase));
            //    if (cdiEntry != null)
            //    {
            //        using (var ms = new MemoryStream())
            //        {
            //            cdiEntry.WriteTo(ms);
            //            RetrieveTrack3DataFromCdiStream(game, ms);
            //        }
            //    }
            //}

            if (game.GameName == "GDMENU")
            {
                return null;
            }

            return game;
        }

        private static void RetrieveTrack3DataFromCdiStream(BaseGame game, Stream fs)
        {
            var track3 = game.CdiInfo.Sessions[1].Tracks[0];

            bool isRawMode = track3.SectorSize == 2352; // 2352/RAW mode or 2048
            long startingPosition = track3.Position + (track3.PregapLength * (long)track3.SectorSize);
            fs.Seek(startingPosition, SeekOrigin.Begin);

            byte[] emptyBuffer = new byte[1];
            do
            {
                fs.Read(emptyBuffer, 0, 1);
            } while (emptyBuffer[0] == 0);

            fs.Seek(fs.Position - 1, SeekOrigin.Begin);

            if (isRawMode)
            {
                // We ignore the first line
                byte[] dummyBuffer = new byte[16];
                fs.Read(dummyBuffer, 0, 16);
            }

            ReadGameInfoFromBinaryData(game, fs);
        }

        private static void ReadGameInfoFromBinaryData(BaseGame game, Stream fs)
        {
            byte[] hwidBuffer = new byte[16];
            fs.Read(hwidBuffer, 0, 16);
            game.Hwid = Encoding.UTF8.GetString(hwidBuffer).Replace('\0', ' ').Trim();

            byte[] makerBuffer = new byte[16];
            fs.Read(makerBuffer, 0, 16);
            game.Maker = Encoding.UTF8.GetString(makerBuffer).Replace('\0', ' ').Trim();

            byte[] crcBuffer = new byte[5];
            fs.Read(crcBuffer, 0, 5);
            game.Crc = Encoding.UTF8.GetString(crcBuffer).Replace('\0', ' ').Trim();

            byte[] discBuffer = new byte[11];
            fs.Read(discBuffer, 0, 11);
            game.Disc = Encoding.UTF8.GetString(discBuffer).Replace('\0', ' ').Trim();

            byte[] regionBuffer = new byte[8];
            fs.Read(regionBuffer, 0, 8);
            game.Region = Encoding.UTF8.GetString(regionBuffer).Replace('\0', ' ').Trim();

            byte[] perifBuffer = new byte[8];
            fs.Read(perifBuffer, 0, 8);
            game.Perif = Encoding.UTF8.GetString(perifBuffer).Replace('\0', ' ').Trim();

            byte[] productNBuffer = new byte[10];
            fs.Read(productNBuffer, 0, 10);
            game.ProductN = Encoding.UTF8.GetString(productNBuffer).Replace('\0', ' ').Trim();

            byte[] productVBuffer = new byte[6];
            fs.Read(productVBuffer, 0, 6);
            game.ProductV = Encoding.UTF8.GetString(productVBuffer).Replace('\0', ' ').Trim();

            byte[] releaseDateBuffer = new byte[16];
            fs.Read(releaseDateBuffer, 0, 16);
            game.ReleaseDate = Encoding.UTF8.GetString(releaseDateBuffer).Replace('\0', ' ').Trim();

            byte[] bootFileBuffer = new byte[16];
            fs.Read(bootFileBuffer, 0, 16);
            game.BootFile = Encoding.UTF8.GetString(bootFileBuffer).Replace('\0', ' ').Trim();

            byte[] producerBuffer = new byte[16];
            fs.Read(producerBuffer, 0, 16);
            game.Producer = Encoding.UTF8.GetString(producerBuffer).Replace('\0', ' ').Trim();

            byte[] gameNameBuffer = new byte[128];
            fs.Read(gameNameBuffer, 0, 128);
            game.GameName = CleanName(Encoding.UTF8.GetString(gameNameBuffer));
        }

        public static string CleanName(string name)
        {
            string newName = name.Replace('\0', ' ');
            newName = Regex.Replace(
                Regex.Replace(
                    Regex.Replace(newName, @"\p{C}+", string.Empty),
                    @"\p{Po}+", string.Empty),
                @"\p{S}+", string.Empty).Trim();

            return newName;
        }

        public static Gdi GetGdiFromFile(string path)
        {
            var gdiContent = File.ReadAllLines(path);
            return GetGdiFromStringContent(gdiContent);
        }

        public static Gdi GetGdiFromStringContent(IEnumerable<string> gdiContent)
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

        /// <summary>
        /// Read a CDI file.
        /// Most of the code is based on cdirip.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Cdi GetCdiFromFile(Stream cdiStream)
        {
            var cdi = new Cdi();

            long length = cdiStream.Seek(0L, SeekOrigin.End);
            cdiStream.Seek(length - 8, SeekOrigin.Begin);

            long globalTrackPosition = 0;
            byte[] buffer1 = new byte[1];
            byte[] buffer2 = new byte[2];
            byte[] buffer4 = new byte[4];

            cdiStream.Read(buffer4, 0, 4);
            uint cdiVersion = BitConverter.ToUInt32(buffer4);

            cdiStream.Read(buffer4, 0, 4);
            uint headerOffset = BitConverter.ToUInt32(buffer4);

            cdiStream.Seek(length - headerOffset, SeekOrigin.Begin);

            cdiStream.Read(buffer2, 0, 2);
            ushort numberOfSessions = BitConverter.ToUInt16(buffer2);

            cdi.Sessions = new List<CdiSession>();
            for (int i = 0; i < numberOfSessions; i++)
            {
                var session = new CdiSession();
                session.Tracks = new List<CdiTrack>();

                cdiStream.Read(buffer2, 0, 2);
                ushort numberOfTracks = BitConverter.ToUInt16(buffer2);
                for (int j = 0; j < numberOfTracks; j++)
                {
                    var track = new CdiTrack();
                    track.Number = j + 1;

                    byte[] trackStartMark = { 0, 0, 0x01, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF };
                    byte[] trackStartMarkBuffer = new byte[10];

                    cdiStream.Read(buffer4, 0, 4);
                    if (BitConverter.ToUInt32(buffer4) != 0)
                    {
                        cdiStream.Seek(8, SeekOrigin.Current);
                    }

                    for (int k = 0; k < 2; k++)
                    {
                        cdiStream.Read(trackStartMarkBuffer, 0, 10);
                        if (!trackStartMarkBuffer.SequenceEqual(trackStartMark))
                        {
                            throw new Exception("Bad CDI format. Incorrect track start mark.");
                        }
                    }

                    cdiStream.Seek(4, SeekOrigin.Current);
                    cdiStream.Read(buffer1, 0, 1);
                    track.FilenameLength = buffer1[0];
                    cdiStream.Seek(track.FilenameLength, SeekOrigin.Current);

                    cdiStream.Seek(11, SeekOrigin.Current);
                    cdiStream.Seek(4, SeekOrigin.Current);
                    cdiStream.Seek(4, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    if (BitConverter.ToUInt32(buffer4) == 0x80000000)
                    {
                        cdiStream.Seek(8, SeekOrigin.Current);
                    }

                    cdiStream.Seek(2, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.PregapLength = BitConverter.ToUInt32(buffer4);

                    cdiStream.Read(buffer4, 0, 4);
                    track.Length = BitConverter.ToUInt32(buffer4);

                    cdiStream.Seek(6, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.Mode = BitConverter.ToUInt32(buffer4);

                    cdiStream.Seek(12, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.StartLba = BitConverter.ToUInt32(buffer4);

                    cdiStream.Read(buffer4, 0, 4);
                    track.TotalLength = BitConverter.ToUInt32(buffer4);

                    cdiStream.Seek(16, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.SectorSizeValue = BitConverter.ToUInt32(buffer4);

                    switch (track.SectorSizeValue)
                    {
                        case 0: track.SectorSize = 2048; break;
                        case 1: track.SectorSize = 2336; break;
                        case 2: track.SectorSize = 2352; break;
                        default:
                            throw new Exception($"Unexpected SectorSizeValue in CDI ({track.SectorSizeValue}).");
                    }

                    if (track.Mode > 2)
                    {
                        throw new Exception($"Unmanaged track mode ({track.Mode}).");
                    }

                    cdiStream.Seek(29, SeekOrigin.Current);

                    if (cdiVersion != Cdi.CdiVersion2)
                    {
                        cdiStream.Seek(5, SeekOrigin.Current);
                        cdiStream.Read(buffer4, 0, 4);
                        if (BitConverter.ToUInt32(buffer4) == 0xffffffff)
                        {
                            cdiStream.Seek(78, SeekOrigin.Current);
                        }
                    }

                    session.Tracks.Add(track);

                    var position = cdiStream.Position;

                    if (track.TotalLength < track.PregapLength + track.Length)
                    {
                        cdiStream.Seek(globalTrackPosition, SeekOrigin.Begin);
                        cdiStream.Seek(track.TotalLength, SeekOrigin.Current);
                        track.Position = cdiStream.Position;
                        globalTrackPosition = cdiStream.Position;
                    }
                    else
                    {
                        cdiStream.Seek(globalTrackPosition, SeekOrigin.Begin);
                        track.Position = cdiStream.Position;
                        cdiStream.Seek(track.TotalLength * (long)track.SectorSize, SeekOrigin.Current);
                        globalTrackPosition = cdiStream.Position;
                    }

                    cdiStream.Seek(position, SeekOrigin.Begin);
                }

                cdi.Sessions.Add(session);

                cdiStream.Seek(4, SeekOrigin.Current);
                cdiStream.Seek(8, SeekOrigin.Current);

                if (cdiVersion != Cdi.CdiVersion2)
                {
                    cdiStream.Seek(1, SeekOrigin.Current);
                }
            }

            if (cdi.Sessions.Count != 2)
            {
                throw new Exception("Cannot manage CDI with something else than two sessions.");
            }

            if (cdi.Sessions[1].Tracks.Count != 1)
            {
                throw new Exception("Cannot manage CDI with the second session not having one track.");
            }

            return cdi;
        }
    }
}