using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GDEmuSdCardManager.BLL
{
    public class ArchiveManager
    {
        public static IEnumerable<IArchiveEntry> RetreiveFilesFromArchiveStartingWith(IArchive archive, string fileNameStart)
        {
            return archive.Entries
                .Where(e =>
                    e.Key.StartsWith(fileNameStart, StringComparison.InvariantCultureIgnoreCase)
                    && !e.IsDirectory);
        }

        public static IArchiveEntry RetreiveUniqueFileFromArchiveEndingWith(IArchive archive, string fileNameEnd)
        {
            return archive.Entries.SingleOrDefault(e =>
            e.Key != null
            && e.Key.EndsWith(fileNameEnd, StringComparison.InvariantCultureIgnoreCase));
        }

        public static int CountFilesFromArchiveEndingWith(IArchive archive, string fileNameEnd)
        {
            return archive.Entries.Count(e =>
            e.Key != null
            && e.Key.EndsWith(fileNameEnd, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
