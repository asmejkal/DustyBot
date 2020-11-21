namespace DustyBot.LastFm.Models
{
    public class LastFmUserInfo
    {
        public int Playcount { get; }

        internal LastFmUserInfo(int playcount)
        {
            Playcount = playcount;
        }
    }
}
