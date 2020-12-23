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
            game.Hwid = ConvertAndCleanBytesArrayToString(hwidBuffer);

            byte[] makerBuffer = new byte[16];
            fs.Read(makerBuffer, 0, 16);
            game.Maker = ConvertAndCleanBytesArrayToString(makerBuffer);

            byte[] crcBuffer = new byte[5];
            fs.Read(crcBuffer, 0, 5);
            game.Crc = ConvertAndCleanBytesArrayToString(crcBuffer);

            byte[] discBuffer = new byte[11];
            fs.Read(discBuffer, 0, 11);
            game.Disc = ConvertAndCleanBytesArrayToString(discBuffer);

            byte[] regionBuffer = new byte[8];
            fs.Read(regionBuffer, 0, 8);
            game.Region = ConvertAndCleanBytesArrayToString(regionBuffer);

            byte[] perifBuffer = new byte[8];
            fs.Read(perifBuffer, 0, 8);
            game.Perif = ConvertAndCleanBytesArrayToString(perifBuffer);

            byte[] productNBuffer = new byte[10];
            fs.Read(productNBuffer, 0, 10);
            game.ProductN = ConvertAndCleanBytesArrayToString(productNBuffer);

            byte[] productVBuffer = new byte[6];
            fs.Read(productVBuffer, 0, 6);
            game.ProductV = ConvertAndCleanBytesArrayToString(productVBuffer);

            byte[] releaseDateBuffer = new byte[16];
            fs.Read(releaseDateBuffer, 0, 16);
            game.ReleaseDate = ConvertAndCleanBytesArrayToString(releaseDateBuffer);

            byte[] bootFileBuffer = new byte[16];
            fs.Read(bootFileBuffer, 0, 16);
            game.BootFile = ConvertAndCleanBytesArrayToString(bootFileBuffer);

            byte[] producerBuffer = new byte[16];
            fs.Read(producerBuffer, 0, 16);
            game.Producer = ConvertAndCleanBytesArrayToString(producerBuffer);

            byte[] gameNameBuffer = new byte[128];
            fs.Read(gameNameBuffer, 0, 128);
            game.GameName = CleanName(ConvertAndCleanBytesArrayToString(gameNameBuffer));
        }

        private static string ConvertAndCleanBytesArrayToString(byte[] array)
        {
            return Encoding.UTF8.GetString(array).Replace('\0', ' ').Trim();
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