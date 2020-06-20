using System.IO;
using System.Linq;
using System.Text;

namespace GDEmuSdCardManager.BLL
{
    public class GameManager
    {
        public static string GetName(string folderPath, string gdiPath)
        {
            string gameName;
            string gdiFileName = Path.GetFileNameWithoutExtension(gdiPath);
            try
            {
                var bin1File = Directory
                    .EnumerateFiles(folderPath)
                    .SingleOrDefault(f => Path.GetFileName(f) == "track01.bin" || Path.GetFileName(f) == "track01.iso");
                if (bin1File == null)
                {
                    // using the GDI file name as game name
                    gameName = gdiFileName;
                }
                else
                {
                    // Reading the game name from track01.bin
                    byte[] buffer = File.ReadAllBytes(bin1File).Skip(144).Take(140).ToArray();
                    gameName = Encoding.UTF8.GetString(buffer).Replace('\0', ' ').Trim();
                    if (string.IsNullOrEmpty(gameName))
                    {
                        gameName = gdiFileName;
                    }
                }
            }
            catch
            {
                // If anything fails, we just the name of the GDI
                gameName = gdiFileName;
            }

            return gameName;
        }
    }
}
