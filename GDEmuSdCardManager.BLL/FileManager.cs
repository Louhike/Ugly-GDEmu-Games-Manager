using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GDEmuSdCardManager.BLL
{
    public static class FileManager
    {
        private static readonly string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };

        public static long GetDirectorySize(string dirPath)
        {
            if (Directory.Exists(dirPath) == false)
            {
                return 0;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);

            long size = 0;

            // Add file sizes.
            FileInfo[] fis = dirInfo.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }

            // Add subdirectory sizes.
            DirectoryInfo[] dis = dirInfo.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += GetDirectorySize(di.FullName);
            }

            return size;
        }

        public static string GetDirectoryFormattedSize(string dirPath)
        {
            return FormatSize(GetDirectorySize(dirPath));
        }

        public static string FormatSize(long bytes)
        {
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }

        public static async Task CopyDirectoryContentToAnother(string fromDirectory, string toDirectory, bool renameGdiFileToDisc)
        {
            if (!Directory.Exists(toDirectory))
            {
                Directory.CreateDirectory(toDirectory);
            }
            else
            {
                RemoveAllFilesInDirectory(toDirectory);
            }

            foreach (var fileToCopy in Directory.EnumerateFiles(fromDirectory))
            {
                string fileName = Path.GetFileNameWithoutExtension(fileToCopy);
                string fileExtension = Path.GetExtension(fileToCopy);
                if(fileExtension == ".gdi" && renameGdiFileToDisc)
                {
                    fileName = "disc";
                }

                fileName += fileExtension;
                string filePath = Path.GetFullPath(toDirectory + @"\" + fileName);
                using (FileStream SourceStream = File.Open(fileToCopy, FileMode.Open))
                {
                    using (FileStream DestinationStream = File.Create(filePath))
                    {
                        await SourceStream.CopyToAsync(DestinationStream);
                    }
                }
            }
        }

        public static void RemoveAllFilesInDirectory(string directoryPath)
        {
            var di = new DirectoryInfo(directoryPath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
        }

        public static IEnumerable<string> EnumerateFolders(string path)
        {
            var subFolders = new List<string>();
            subFolders.AddRange(Directory.EnumerateDirectories(
                                path,
                                "*",
                                new EnumerationOptions
                                {
                                    IgnoreInaccessible = true,
                                    RecurseSubdirectories = true,
                                    ReturnSpecialDirectories = false
                                }));

            return subFolders;
        }

        public static IEnumerable<string> EnumerateArchives(string path)
        {
            var compressedFiles = new List<string>();
            compressedFiles.AddRange(Directory.EnumerateFiles(
                path,
                "*",
                 new EnumerationOptions
                 {
                     IgnoreInaccessible = true,
                     RecurseSubdirectories = true,
                     ReturnSpecialDirectories = false
                 }).Where(p => p.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)
                 || p.EndsWith(".7z", StringComparison.InvariantCultureIgnoreCase)
                 || p.EndsWith(".rar", StringComparison.InvariantCultureIgnoreCase)
                 || p.EndsWith(".bz", StringComparison.InvariantCultureIgnoreCase)
                 || p.EndsWith(".bz2", StringComparison.InvariantCultureIgnoreCase)
                 || p.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase)
                 || p.EndsWith(".lz", StringComparison.InvariantCultureIgnoreCase)));

            return compressedFiles;
        }
    }
}
