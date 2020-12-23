using System.Collections.Generic;

namespace GDEmuSdCardManager.BLL.Comparers
{
    public class NullValuesAreLastDescendingComparer: IComparer<long?>
    {
        public int Compare(long? x, long? y)
        {
            if (y == null && x != null)
            {
                return 1;
            }
            else if (y != null && x == null)
            {
                return -1;
            }
            else if (y == null) //  && x == null => implicit
            {
                return 0;
            }
            else
            {
                return ((long)x).CompareTo((long)y);
            }
        }
    }
}