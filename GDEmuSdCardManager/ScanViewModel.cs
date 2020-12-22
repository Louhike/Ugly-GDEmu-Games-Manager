using GDEmuSdCardManager.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GDEmuSdCardManager
{
    public class ScanViewModel
    {
        public bool IsScanSuccessful { get; set; }
        public string PcFolder { get; set; }
        public bool MustScanSevenZip { get; set; }
        public IOrderedEnumerable<GameOnPc> GamesOnPc { get; set; }
        public readonly string PathSplitter = @"|";
    }
}
