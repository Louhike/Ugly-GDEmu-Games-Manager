namespace GDEmuSdCardManager.DTO
{
    public class GameOnPc : BaseGame
    {
        public bool IsInSdCard { get; set; }
        public string IsInSdCardString { get; set; }
        public string SdFolder { get; set; }
        public bool MustShrink { get; set; }
        public bool MustCopy { get; set; }
        public bool MustRemove { get; set; }
        public bool NotMustRemove { get { return !MustRemove; } }
        public bool NotMustCopyAndNotMustShrink { get { return !MustCopy && !MustShrink; } }
        public string SdFormattedSize { get; set; }
    }
}