using GDEmuSdCardManager.BLL.Extensions;
using GDEmuSdCardManager.DTO;
using GDEmuSdCardManager.DTO.GDI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
                FormattedSize = FileManager.GetDirectoryFormattedSize(folderPath)
            };

            string gdiPath = Directory.EnumerateFiles(folderPath).FirstOrDefault(f => System.IO.Path.GetExtension(f) == ".gdi");
            game.GdiInfo = GetGdiFromFile(gdiPath);
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
                game.GameName = Encoding.UTF8.GetString(gameNameBuffer).Replace('\0', ' ').Trim();
            }

            return game;
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
    }
}