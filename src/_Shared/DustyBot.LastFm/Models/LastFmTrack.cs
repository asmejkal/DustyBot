namespace DustyBot.LastFm.Models
{
    public class LastFmTrack
    {
        public object Id => (Artist.Name, Name);
        public string Name { get; }
        public LastFmArtist Artist => Album.Artist;
        public LastFmAlbum Album { get; }
        public string Url => _url ?? (_url = LastFmClient.GetTrackUrl(Artist.Name, Name));
        public int? Playcount { get; }
        public string HashId => _hashId ?? (_hashId = LastFmClient.GetTrackId(Artist.Name, Name));

        private string _url;
        private string _hashId;

        internal LastFmTrack(string name, LastFmAlbum album, int? playcount = null)
        {
            Name = name;
            Album = album;
            Playcount = playcount;
        }

        internal LastFmTrack WithPlaycount(int playcount) => new LastFmTrack(Name, Album, playcount);
    }
}
