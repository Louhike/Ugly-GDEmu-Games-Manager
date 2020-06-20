namespace GDEmuSdCardManager.DTO
{
    public abstract class BaseGame
    {
        public string GameName { get; set; }
        public string FullPath { get; set; }
        public string Path { get; set; }
        public string FormattedSize { get; set; }
    }
}