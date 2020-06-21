using System.Text.RegularExpressions;

namespace GDEmuSdCardManager.BLL.Extensions
{
    public static class StringExtensions
    {
        public static string RemoveSpacesInSuccession(this string str)
        {
            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex("[ ]{2,}", options);
            return regex.Replace(str, " ");
        }
    }
}