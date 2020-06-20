using GDEmuSdCardManager.DTO;
using Microsoft.VisualBasic;
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
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
                Hwid = game.Hwid,
                Maker = game.Maker,
                Path = game.FullPath,
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
                Path = Path.GetDirectoryName(folderPath),
                FormattedSize = FileManager.GetDirectoryFormattedSize(folderPath)
            };

            string gdiPath = Directory.EnumerateFiles(folderPath).FirstOrDefault(f => System.IO.Path.GetExtension(f) == ".gdi");
            var gdiContent = File.ReadAllLines(gdiPath).Where(s => !string.IsNullOrEmpty(s));

            var track03lba = int.Parse(gdiContent.ElementAt(3).Split(" ")[1]);
            if(track03lba != 45000)
            {
                throw new System.Exception("Bad track03.bin LBA");
            }

            bool isRawMode = int.Parse(gdiContent.ElementAt(3).Split(" ")[3]) == 2352; // 2352/RAW mode or 2048

            var bin3File = Directory
                .EnumerateFiles(folderPath)
                .SingleOrDefault(f => Path.GetFileName(f) == "track03.bin" || Path.GetFileName(f) == "track03.iso");
            if (bin3File == null)
            {
                // using the GDI file name as game name
                throw new System.Exception("Error while retrieving track03.bin from game at " + folderPath);
            }

            using (var fs = File.OpenRead(bin3File))
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
    }
}
