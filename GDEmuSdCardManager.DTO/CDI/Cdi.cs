using System.Collections.Generic;

namespace GDEmuSdCardManager.DTO.CDI
{
    public class Cdi
    {
        public static readonly uint CdiVersion2 = 0x80000004;
        public static readonly uint CdiVersion3 = 0x80000005;
        public static readonly uint CdiVersion35 = 0x80000006;
        public List<CdiSession> Sessions { get; set; }
    }
}