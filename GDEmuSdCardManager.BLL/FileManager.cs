using System;
using System.IO;
using System.Threading.Tasks;

namespace GDEmuSdCardManager.BLL
{
    public class FileManager
    {
        private static readonly string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };

        public long GetDirectorySize(string dirPath)
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

        public string FormatSize(long bytes)
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

        public async Task CopyDirectoryContentToAnother(string fromDirectory, string toDirectory)
        {
            if (!Directory.Exists(toDirectory))
            {
                Directory.CreateDirectory(toDirectory);
            }

            foreach (var fileToCopy in Directory.EnumerateFiles(fromDirectory))
            {
                string filePath = Path.GetFullPath(toDirectory + @"\" + Path.GetFileName(fileToCopy));
                using (FileStream SourceStream = File.Open(fileToCopy, FileMode.Open))
                {
                    using (FileStream DestinationStream = File.Create(filePath))
                    {
                        await SourceStream.CopyToAsync(DestinationStream);
                    }
                }
            }
        }
    }
}
