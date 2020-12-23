using GDEmuSdCardManager.BLL.ImageReaders;
using GDEmuSdCardManager.DTO;
using SharpCompress.Archives;
using System.IO;
using System.Linq;

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
            var gdiReader = new GdiReader();
            BaseGame game = gdiReader.ExtractGameDataFromArchive(archivePath, archive);

            if (game.GameName == "GDMENU")
            {
                return null;
            }

            var gameOnPc = ConvertBaseGameToGameOnPc(game);

            gameOnPc.IsCompressed = true;

            return gameOnPc;
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

        public static void LinkGameOnPcToGameOnSd(GameOnPc gameOnPc, GameOnSd gameOnSd)
        {
            gameOnPc.IsInSdCard = true;
            gameOnPc.MustBeOnSd = true;
            gameOnPc.SdFolder = gameOnSd.Path;
            gameOnPc.SdSize = FileManager.GetDirectorySize(gameOnSd.FullPath);
            gameOnPc.SdFormattedSize = FileManager.GetDirectoryFormattedSize(gameOnSd.FullPath);
        }

        public static void UnLinkGameOnPcToGameOnSd(GameOnPc gameOnPc)
        {
            gameOnPc.IsInSdCard = false;
            gameOnPc.MustBeOnSd = false;
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
                ReleaseDate = game.ReleaseDate,
                Size = game.Size
            };
        }

        /// <summary>
        /// Extract game information from a folder
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        private static BaseGame ExtractGameData(string folderPath)
        {
            BaseGame game = null;

            string imagePath = FileManager.GetImageFilesPathInFolder(folderPath).FirstOrDefault();
            if (!string.IsNullOrEmpty(imagePath) && imagePath.EndsWith(".gdi"))
            {
                var gdiReader = new GdiReader();
                game = gdiReader.ExtractGameData(imagePath);
            }
            else if (!string.IsNullOrEmpty(imagePath) && imagePath.EndsWith(".cdi"))
            {
                using (var fs = File.OpenRead(imagePath))
                {
                    var cdiReader = new CdiReader();
                    game = cdiReader.ExtractGameData(imagePath);
                }
            }
            else
            {
                game = new BaseGame
                {
                    FullPath = folderPath,
                    Path = folderPath.Split(Path.DirectorySeparatorChar).Last(),
                    Size = FileManager.GetDirectorySize(folderPath),
                    FormattedSize = FileManager.GetDirectoryFormattedSize(folderPath)
                };
            }

            if (game.GameName == "GDMENU")
            {
                return null;
            }

            return game;
        }
    }
}