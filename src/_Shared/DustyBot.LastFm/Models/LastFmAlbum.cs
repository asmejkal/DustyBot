using System;

namespace DustyBot.LastFm.Models
{
    public class LastFmAlbum
    {
        public object Id => (Artist.Name, Name);
        public string Name { get; }
        public LastFmArtist Artist { get; }
        public string Url => _url ?? (_url = LastFmClient.GetAlbumUrl(Artist.Name, Name));
        public int? Playcount { get; }
        public Uri ImageUri { get; }
        public string HashId => _hashId ?? (_hashId = LastFmClient.GetAlbumId(Artist.Name, Name));

        private string _url;
        private string _hashId;

        internal LastFmAlbum(string name, LastFmArtist artist, int? playcount = null, Uri imageUri = null)
        {
            Name = name;
            Artist = artist;
            Playcount = playcount;
            ImageUri = imageUri;
        }

        internal LastFmAlbum WithPlaycount(int playcount) => new LastFmAlbum(Name, Artist, playcount);
    }
}
