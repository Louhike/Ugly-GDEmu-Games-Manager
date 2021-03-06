﻿namespace GDEmuSdCardManager.DTO
{
    public class GameOnPc : BaseGame
    {
        public bool IsInSdCard { get; set; }
        public string SdFolder { get; set; }
        public bool IsCompressed { get; set; }
        public bool Is7z { get; set; }
        public bool MustShrink { get; set; }
        public bool MustBeOnSd { get; set; }
        public bool NotMustCopyAndNotMustShrink { get { return !MustBeOnSd && !MustShrink; } }
        public long? SdSize { get; set; }
        public string SdFormattedSize { get; set; }
        public object FileManager { get; private set; }
    }
}