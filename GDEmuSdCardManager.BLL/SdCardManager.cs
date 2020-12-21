using GDEmuSdCardManager.BLL.Extensions;
using GDEmuSdCardManager.DTO;
using Medallion.Shell;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
                var imageFile = Directory.EnumerateFiles(subFolder).SingleOrDefault(f => Path.GetExtension(f) == ".gdi" || Path.GetExtension(f) == ".cdi");
                if (imageFile != null)
                {
                    try
                    {
                        var game = GameManager.ExtractSdGameData(subFolder);
                        if(game != null)
                        {
                            gamesOnSdCard.Add(game);
                        }
                    }
                    catch (Exception error)
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
            string oldImagePath = game.FullPath;
            //string oldGdiPath = Directory.EnumerateFiles(game.FullPath).Single(f => Path.GetExtension(f) == ".gdi");

            if (game.IsCompressed)
            {
                oldImagePath = ExtractArchive(game);
            }

            if (game.MustShrink)
            {
                if (Directory.Exists(destinationFolder))
                {
                    FileManager.RemoveAllFilesInDirectory(destinationFolder);
                }
                else
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                //var commandResult = await Command
                //    .Run(@".\gditools\dist\gditools_messily_tweaked.exe", oldGdiPath, destinationFolder)
                //    .Task;

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                Command command = null;
                try
                {
                    command = Command.Run(@".\gditools\dist\gditools_messily_tweaked.exe", oldImagePath, destinationFolder);
                    await command.Task.WaitOrCancel(cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    if (command != null)
                    {
                        command.Kill();
                    }

                    throw new OperationCanceledException($"Timeout while shrinking {game.GameName}. You might need to copy it without shrinking.");
                }

                //if (!commandResult.Success)
                //{
                //    // There is always an error even if it's working, need find out why
                //    //throw new System.Exception("There was an error while shriking the GDI: " + commandResult.StandardError);
                //}

                var gdiPath = Directory.EnumerateFiles(destinationFolder).SingleOrDefault(f => Path.GetExtension(f) == ".gdi");
                if (gdiPath == null)
                {
                    throw new OperationCanceledException($"Could not shrink {game.GameName}. You might need to copy it without shrinking.");
                }
                var newGdi = GameManager.GetGdiFromFile(gdiPath);
                File.Delete(gdiPath);
                newGdi.SaveTo(Path.Combine(destinationFolder, "disc.gdi"), true);
                newGdi.RenameTrackFiles(destinationFolder);
            }
            else
            {
                await FileManager.CopyDirectoryContentToAnother(new FileInfo(oldImagePath).Directory.FullName, destinationFolder, true);

                if (game.IsGdi)
                {
                    var gdiPath = Directory.EnumerateFiles(destinationFolder).Single(f => Path.GetExtension(f) == ".gdi");
                    var newGdi = GameManager.GetGdiFromFile(gdiPath);
                    File.Delete(gdiPath);
                    newGdi.SaveTo(Path.Combine(destinationFolder, "disc.gdi"), true);
                    newGdi.RenameTrackFiles(destinationFolder);
                }
                else // CDI
                {
                    var cdiPath = Directory.EnumerateFiles(destinationFolder).Single(f => Path.GetExtension(f) == ".cdi");
                    File.Move(cdiPath, Path.Combine(destinationFolder, "disc.cdi"));
                }
            }
        }

        private static string ExtractArchive(GameOnPc game)
        {
            string oldGdiPath;
            var tempPath = @".\temp_uncompressed\";
            oldGdiPath = tempPath;
            if (game.Is7z)
            {
                var sevenZipArchive = SevenZipArchive.Open(game.FullPath);
                var gpiEntry = sevenZipArchive.Entries.FirstOrDefault(e => e.Key.EndsWith(".gdi"));
                var separator = "/";
                var pathParts = gpiEntry.Key.Split(separator);
                List<SevenZipArchiveEntry> entriesToExtract = new List<SevenZipArchiveEntry>();
                if (pathParts.Count() > 1)
                {
                    string rootPath = gpiEntry.Key.Replace(pathParts.Last(), string.Empty);
                    entriesToExtract.AddRange(sevenZipArchive.Entries.Where(e => e.Key.StartsWith(rootPath) && !e.IsDirectory));
                }
                else
                {
                    entriesToExtract.AddRange(sevenZipArchive.Entries.Where(e => !e.Key.Contains(separator) && !e.IsDirectory));
                }

                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }

                foreach (var entry in entriesToExtract)
                {
                    var fileName = entry.Key.Split(separator).Last();
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        using (var destinationFileStream = new FileStream(tempPath + fileName, FileMode.Create))
                        {
                            entryStream.CopyTo(destinationFileStream);
                        }
                    }
                }

                oldGdiPath = Directory.EnumerateFiles(tempPath).Single(f => Path.GetExtension(f) == ".gdi");
            }

            return oldGdiPath;
        }

        public static string GetGdemuFolderNameFromIndex(short index)
        {
            return index < 100 ? "D2" : index < 1000 ? "D3" : "D4";
        }
    }
}