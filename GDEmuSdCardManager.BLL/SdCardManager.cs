using GDEmuSdCardManager.DTO;
using Medallion.Shell;
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

        public IEnumerable<GameOnSd> GetGames()
        {
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
                    string gameName = GameManager.GetName(subFolder, gdiFile);

                    gamesOnSdCard.Add(new GameOnSd
                    {
                        GameName = gameName,
                        FullPath = subFolder,
                        Path = Path.GetFileName(subFolder),
                        FormattedSize = FileManager.GetDirectoryFormattedSize(subFolder)
                    });
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
            var sdSubFoldersListWithGames = Directory.EnumerateDirectories(DrivePath).Where(f => Directory.EnumerateFiles(f).Any(f => System.IO.Path.GetExtension(f) == ".gdi"));

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

        public async Task AddGame(string gamePath, short destinationFolderIndex, bool mustShrink)
        {
            string format = GetGdemuFolderNameFromIndex(destinationFolderIndex);
            string destinationFolder = Path.GetFullPath(DrivePath + destinationFolderIndex.ToString(format));

            if (mustShrink)
            {
                string tempPath = @".\Extract Re-Build GDI's\temp_game_copy";
                Directory.CreateDirectory(tempPath);
                FileManager.RemoveAllFilesInDirectory(tempPath);
                await FileManager.CopyDirectoryContentToAnother(
                    gamePath,
                    tempPath);
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

                await FileManager.CopyDirectoryContentToAnother(
                    tempPath,
                    destinationFolder);

                Directory.Delete(tempPath, true);

                Directory.Delete(tempPath + " Extracted", true);
            }
            else
            {
                await FileManager.CopyDirectoryContentToAnother(gamePath, destinationFolder);
            }
        }

        private string GetGdemuFolderNameFromIndex(short index)
        {
            return index < 100 ? "D2" : index < 1000 ? "D3" : "D4";
        }
    }
}