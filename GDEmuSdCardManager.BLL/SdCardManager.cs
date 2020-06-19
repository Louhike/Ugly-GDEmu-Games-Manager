using GDEmuSdCardManager.DTO;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                    var bin1File = Directory.EnumerateFiles(subFolder).SingleOrDefault(f => Path.GetFileName(f) == "track01.bin");
                    string gameName = "Unknown name";
                    // Reading the game name
                    byte[] buffer = File.ReadAllBytes(bin1File).Skip(144).Take(140).ToArray();
                    gameName = System.Text.Encoding.UTF8.GetString(buffer).Trim();

                    gamesOnSdCard.Add(new GameOnSd
                    {
                        GameName = gameName,
                        FullPath = subFolder,
                        //GdiName = Path.GetFileName(gdiFile),
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

        public void RemoveGames()
        {

        }
    }
}