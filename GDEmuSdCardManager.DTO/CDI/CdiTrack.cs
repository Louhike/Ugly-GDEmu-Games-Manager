namespace GDEmuSdCardManager.DTO.CDI
{
    public class CdiTrack
    {
        public int Number;
        public byte FilenameLength;
        public ulong Mode;
        public long Position;
        public ulong SectorSize;
        public ulong SectorSizeValue;
        public long Length;
        public long PregapLength;
        public long TotalLength;
        public ulong StartLba;
    }
}