using GDEmuSdCardManager.DTO;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GDEmuSdCardManager.BLL.ImageReaders
{
    public abstract class BaseImageReader
    {
        protected void ReadGameInfoFromBinaryData(BaseGame game, Stream fs)
        {
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
            game.GameName = CleanName(Encoding.UTF8.GetString(gameNameBuffer));
        }

        private static string CleanName(string name)
        {
            string newName = name.Replace('\0', ' ');
            newName = Regex.Replace(
                Regex.Replace(
                    Regex.Replace(newName, @"\p{C}+", string.Empty),
                    @"\p{Po}+", string.Empty),
                @"\p{S}+", string.Empty).Trim();

            return newName;
        }
    }
}