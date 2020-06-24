﻿using GDEmuSdCardManager.DTO;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GDEmuSdCardManager.BLL
{
    public static class MenuManager
    {
        public async static Task CreateIndex(string destinationFolder, IEnumerable<GameOnSd> gamesToIndex)
        {
            var sdFolders = Directory.EnumerateDirectories(destinationFolder);

            foreach (var folder in sdFolders.Where(f => Path.GetFileName(f) != "01" && int.TryParse(Path.GetFileName(f), out int _)))
            {
                Directory.Move(folder, folder + "_");
            }

            var tempPath = @".\menu_tools_and_files\temp_content";
            if (Directory.Exists(tempPath))
            {
                FileManager.RemoveAllFilesInDirectory(tempPath);
            }
            else
            {
                Directory.CreateDirectory(tempPath);
            }

            string tempListIniPath = tempPath + @"\LIST.INI";

            await FileManager.CopyDirectoryContentToAnother(@".\menu_tools_and_files\content", tempPath, false);

            for(short i = 2; i <= gamesToIndex.Count() + 1; i++)
            {
                var game = gamesToIndex.OrderBy(g => g.GameName).ElementAt(i - 2);
                string index = i.ToString(SdCardManager.GetGdemuFolderNameFromIndex(i));

                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, $"{ index}.name={game.GameName}");
                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, $"{index}.disc={game.FormattedDiscNumber}");
                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, $"{index}.vga=1");
                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, $"{index}.region={game.Region}");
                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, $"{index}.version={game.ProductV}");
                File.AppendAllText(tempListIniPath, Environment.NewLine);
                File.AppendAllText(tempListIniPath, $"{index}.date={game.ReleaseDate}");

                string newPath = Path.Combine(destinationFolder, index);
                Directory.Move(game.FullPath + "_", newPath);
                File.Create(Path.Combine(newPath, "name.txt")).Close();
                File.AppendAllText(Path.Combine(newPath, "name.txt"), game.GameName);
            }

            for(int i = 0; i <= 2; i++ )
            {
                File.AppendAllText(tempListIniPath, Environment.NewLine);
            }

            var commandResult = await Command
                    .Run(@".\menu_tools_and_files\mkisofs.exe", "-C", "0,11702", "-V", "GDMENU", "-G",  @".\menu_tools_and_files\ip.bin", "-l", "-o", @".\menu_tools_and_files\disc.iso", tempPath)
                    .Task;

            var commandResult2 = await Command
                    .Run(@".\menu_tools_and_files\cdi4dc.exe", @".\menu_tools_and_files\disc.iso", @".\menu_tools_and_files\disc.cdi")
                    .Task;

            File.Move(@".\menu_tools_and_files\disc.cdi", Path.Combine(destinationFolder, @"01\disc.cdi"), true);
        }

        public async static Task CreateMenuWithoutIndex(string destinationFolder)
        {
            var tempPath = @".\menu_tools_and_files\temp_content";
            if (Directory.Exists(tempPath))
            {
                FileManager.RemoveAllFilesInDirectory(tempPath);
            }
            else
            {
                Directory.CreateDirectory(tempPath);
            }

            string tempListIniPath = tempPath + @"\LIST.INI";

            await FileManager.CopyDirectoryContentToAnother(@".\menu_tools_and_files\content", tempPath, false);
            File.Delete(tempListIniPath);

            var commandResult = await Command
                    .Run(@".\menu_tools_and_files\mkisofs.exe", "-C", "0,11702", "-V", "GDMENU", "-G", @".\menu_tools_and_files\ip.bin", "-l", "-o", @".\menu_tools_and_files\disc.iso", tempPath)
                    .Task;

            var commandResult2 = await Command
                    .Run(@".\menu_tools_and_files\cdi4dc.exe", @".\menu_tools_and_files\disc.iso", @".\menu_tools_and_files\disc.cdi")
                    .Task;

            File.Move(@".\menu_tools_and_files\disc.cdi", Path.Combine(destinationFolder, @"01\disc.cdi"), true);
        }
    }
}
