using GDEmuSdCardManager.DTO;
using System.Linq;

namespace GDEmuSdCardManager
{
    public class ScanViewModel
    {
        public readonly string PathSplitter = @"|";

        public bool IsScanSuccessful { get; set; }
        public string PcFolder { get; set; }
        public string SdDrive { get; set; }
        public bool MustScanSevenZip { get; set; }
        public IOrderedEnumerable<GameOnPc> GamesOnPc { get; set; }
    }
}