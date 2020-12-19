using GDEmuSdCardManager.BLL.Extensions;
using GDEmuSdCardManager.DTO;
using GDEmuSdCardManager.DTO.CDI;
using GDEmuSdCardManager.DTO.GDI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GDEmuSdCardManager.BLL
{
    public class GameManager
    {
        public static GameOnPc ExtractPcGameData(string folderPath)
        {
            var game = ExtractGameData(folderPath);
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
                    game.CdiInfo = GetCdiFromFile(cdiPath);

                    var track3 = game.CdiInfo.Sessions[1].Tracks[0];

                    bool isRawMode = track3.SectorSize == 2352; // 2352/RAW mode or 2048

                    using (var fs = File.OpenRead(cdiPath))
                    {
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
                }
            }

            return game;
        }

        private static void ReadGameInfoFromBinaryData(BaseGame game, FileStream fs)
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
            var gdiContent = File.ReadAllLines(path)
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
        public static Cdi GetCdiFromFile(string path)
        {
            var cdi = new Cdi();

            using (var fs = new FileStream(path, FileMode.Open))
            {
                long length = fs.Seek(0L, SeekOrigin.End);
                fs.Seek(length - 8, SeekOrigin.Begin);

                long globalTrackPosition = 0;
                byte[] buffer1 = new byte[1];
                byte[] buffer2 = new byte[2];
                byte[] buffer4 = new byte[4];

                fs.Read(buffer4, 0, 4);
                uint cdiVersion = BitConverter.ToUInt32(buffer4);

                fs.Read(buffer4, 0, 4);
                uint headerOffset = BitConverter.ToUInt32(buffer4);

                fs.Seek(length - headerOffset, SeekOrigin.Begin);

                fs.Read(buffer2, 0, 2);
                ushort numberOfSessions = BitConverter.ToUInt16(buffer2);

                cdi.Sessions = new List<CdiSession>();
                for (int i = 0; i < numberOfSessions; i++)
                {
                    var session = new CdiSession();
                    session.Tracks = new List<CdiTrack>();

                    fs.Read(buffer2, 0, 2);
                    ushort numberOfTracks = BitConverter.ToUInt16(buffer2);
                    for (int j = 0; j < numberOfTracks; j++)
                    {
                        var track = new CdiTrack();
                        track.Number = j + 1;

                        byte[] trackStartMark = { 0, 0, 0x01, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF };
                        byte[] trackStartMarkBuffer = new byte[10];

                        fs.Read(buffer4, 0, 4);
                        if (BitConverter.ToUInt32(buffer4) != 0)
                        {
                            fs.Seek(8, SeekOrigin.Current);
                        }

                        for (int k = 0; k < 2; k++)
                        {
                            fs.Read(trackStartMarkBuffer, 0, 10);
                            if (!trackStartMarkBuffer.SequenceEqual(trackStartMark))
                            {
                                throw new Exception("Bad CDI format. Incorrect track start mark.");
                            }
                        }

                        fs.Seek(4, SeekOrigin.Current);
                        fs.Read(buffer1, 0, 1);
                        track.FilenameLength = buffer1[0];
                        fs.Seek(track.FilenameLength, SeekOrigin.Current);

                        fs.Seek(11, SeekOrigin.Current);
                        fs.Seek(4, SeekOrigin.Current);
                        fs.Seek(4, SeekOrigin.Current);

                        fs.Read(buffer4, 0, 4);
                        if (BitConverter.ToUInt32(buffer4) == 0x80000000)
                        {
                            fs.Seek(8, SeekOrigin.Current);
                        }

                        fs.Seek(2, SeekOrigin.Current);

                        fs.Read(buffer4, 0, 4);
                        track.PregapLength = BitConverter.ToUInt32(buffer4);

                        fs.Read(buffer4, 0, 4);
                        track.Length = BitConverter.ToUInt32(buffer4);

                        fs.Seek(6, SeekOrigin.Current);

                        fs.Read(buffer4, 0, 4);
                        track.Mode = BitConverter.ToUInt32(buffer4);

                        fs.Seek(12, SeekOrigin.Current);

                        fs.Read(buffer4, 0, 4);
                        track.StartLba = BitConverter.ToUInt32(buffer4);

                        fs.Read(buffer4, 0, 4);
                        track.TotalLength = BitConverter.ToUInt32(buffer4);

                        fs.Seek(16, SeekOrigin.Current);

                        fs.Read(buffer4, 0, 4);
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

                        fs.Seek(29, SeekOrigin.Current);

                        if (cdiVersion != Cdi.CdiVersion2)
                        {
                            fs.Seek(5, SeekOrigin.Current);
                            fs.Read(buffer4, 0, 4);
                            if (BitConverter.ToUInt32(buffer4) == 0xffffffff)
                            {
                                fs.Seek(78, SeekOrigin.Current);
                            }
                        }

                        session.Tracks.Add(track);

                        var position = fs.Position;

                        if (track.TotalLength < track.PregapLength + track.Length)
                        {
                            fs.Seek(globalTrackPosition, SeekOrigin.Begin);
                            fs.Seek(track.TotalLength, SeekOrigin.Current);
                            track.Position = fs.Position;
                            globalTrackPosition = fs.Position;
                        }
                        else
                        {
                            fs.Seek(globalTrackPosition, SeekOrigin.Begin);
                            track.Position = fs.Position;
                            fs.Seek(track.TotalLength * (long)track.SectorSize, SeekOrigin.Current);
                            globalTrackPosition = fs.Position;
                        }

                        fs.Seek(position, SeekOrigin.Begin);
                    }

                    cdi.Sessions.Add(session);

                    fs.Seek(4, SeekOrigin.Current);
                    fs.Seek(8, SeekOrigin.Current);

                    if (cdiVersion != Cdi.CdiVersion2)
                    {
                        fs.Seek(1, SeekOrigin.Current);
                    }
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