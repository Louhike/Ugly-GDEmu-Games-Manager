using GDEmuSdCardManager.DTO;
using GDEmuSdCardManager.DTO.GDI;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GDEmuSdCardManager.BLL
{
    public class SdCardManager
    {
        public string DrivePath { get; set; }

        public SdCardManager(string path)
        {
            DrivePath = path;
        }

        public IEnumerable<GameOnSd> GetGames(out List<string> errors)
        {
            errors = new List<string>();

            if (!Directory.Exists(DrivePath))
            {
                throw new FileNotFoundException("SD path is invalid");
            }

            if (new DirectoryInfo(DrivePath).Parent != null)
            {
                throw new FileNotFoundException("The SD path must be at the root of the card");
            }

            var subFoldersList = Directory.EnumerateDirectories(DrivePath);
            var gamesOnSdCard = new List<GameOnSd>();

            foreach (var subFolder in subFoldersList)
            {
                var gdiFile = Directory.EnumerateFiles(subFolder).SingleOrDefault(f => Path.GetExtension(f) == ".gdi");
                if (gdiFile != null)
                {
                    try
                    {
                        var game = GameManager.ExtractSdGameData(subFolder);
                        gamesOnSdCard.Add(game);
                    }
                    catch(Exception error)
                    {
                        errors.Add(error.Message);
                    }
                }
            }

            return gamesOnSdCard;
        }

        public long GetFreeSpace()
        {
            DriveInfo driveInfo = new DriveInfo(DrivePath);
            return driveInfo.AvailableFreeSpace;
        }

        public short FindAvailableFolderForGame(short index = 2)
        {
            var sdSubFoldersListWithGames = Directory.EnumerateDirectories(DrivePath).Where(f => Directory.EnumerateFiles(f).Any());

            do
            {
                string format = GetGdemuFolderNameFromIndex(index);
                string formattedIndex = index.ToString(format);
                if (!sdSubFoldersListWithGames.Any(f => Path.GetFileName(f) == formattedIndex))
                {
                    return index;
                }

                index++;
            } while (index < 10000);

            return -1;
        }

        public async Task AddGame(GameOnPc game, short destinationFolderIndex)
        {
            string format = GetGdemuFolderNameFromIndex(destinationFolderIndex);
            string destinationFolder = Path.GetFullPath(DrivePath + destinationFolderIndex.ToString(format));

            if (game.MustShrink)
            {
                string tempPath = @".\Extract Re-Build GDI's\temp_game_copy";
                Directory.CreateDirectory(tempPath);
                FileManager.RemoveAllFilesInDirectory(tempPath);
                if(Directory.Exists(tempPath + " Extracted"))
                {
                    Directory.Delete(tempPath + " Extracted", true);
                }

                await FileManager.CopyDirectoryContentToAnother(
                    game.FullPath,
                    tempPath,
                    false);
                var commandResult = await Command
                    .Run(@".\Extract Re-Build GDI's\Extract GDI Image.bat", tempPath)
                    .Task;
                if(!commandResult.Success)
                {
                    // There is always an error even if it's working, find out why (or use the new gditools)
                    //throw new System.Exception("There was an error while extracting the GDI: " + commandResult.StandardError);
                }

                var commandResult2 = await Command
                    .Run(@".\Extract Re-Build GDI's\Build Truncated GDI Image.bat", tempPath + " Extracted")
                    .Task;
                if (!commandResult2.Success)
                {
                    //throw new System.Exception("There was an error while extracting the GDI: " + commandResult2.StandardError);
                }

                var gdiPath = Directory.EnumerateFiles(tempPath).Single(f => Path.GetExtension(f) == ".gdi");
                var newGdi = GameManager.GetGdiFromFile(gdiPath);
                File.Delete(gdiPath);
                newGdi.SaveTo(Path.Combine(tempPath, "disc.gdi"), true);
                newGdi.RenameTrackFiles(tempPath);
                await FileManager.CopyDirectoryContentToAnother(
                    tempPath,
                    destinationFolder,
                    true);

                Directory.Delete(tempPath, true);

                Directory.Delete(tempPath + " Extracted", true);
            }
            else
            {
                await FileManager.CopyDirectoryContentToAnother(game.FullPath, destinationFolder, true);

                game.GdiInfo.SaveTo(Path.Combine(destinationFolder, "disc.gdi"), true);
                game.GdiInfo.RenameTrackFiles(destinationFolder);
            }
        }

        private string GetGdemuFolderNameFromIndex(short index)
        {
            return index < 100 ? "D2" : index < 1000 ? "D3" : "D4";
        }
    }
}