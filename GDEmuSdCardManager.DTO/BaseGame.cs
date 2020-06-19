using System;
using System.Collections.Generic;
using System.Text;

namespace GDEmuSdCardManager.DTO
{
    public abstract class BaseGame
    {
        public string FullPath { get; set; }
        public string Name { get; set; }
        public string GdiName { get; set; }
        public string FormattedSize { get; set; }
    }
}
