using System;

namespace DustyBot.LastFm.Models
{
    public class LastFmArtist
    {
        public object Id => Name;
        public string Name { get; }
        public string Url => _url ?? (_url = LastFmClient.GetArtistUrl(Name));
        public int? Playcount { get; }
        public Uri ImageUri { get; }
        public string HashId => _hashId ?? (_hashId = LastFmClient.GetArtistId(Name));

        private string _url;
        private string _hashId;

        internal LastFmArtist(string name, int? playcount = null, Uri imageUri = null)
        {
            Name = name;
            Playcount = playcount;
            ImageUri = imageUri;
        }

        internal LastFmArtist WithPlaycount(int playcount) => new LastFmArtist(Name, playcount) { _url = _url };
    }
}
