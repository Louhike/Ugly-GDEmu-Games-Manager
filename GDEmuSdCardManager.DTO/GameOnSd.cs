namespace GDEmuSdCardManager.DTO
{
    public class GameOnSd : BaseGame
    {
        public int SdIndex
        {
            get
            {
                return int.Parse(Path);
            }
        }
    }
}