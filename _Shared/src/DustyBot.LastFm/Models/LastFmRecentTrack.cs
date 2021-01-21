using System;

namespace DustyBot.LastFm.Models
{
    public class LastFmRecentTrack
    {
        public object Id => (Artist.Name, Name);
        public string Name { get; }
        public bool NowPlaying { get; }
        public LastFmArtist Artist => Album.Artist;
        public LastFmAlbum Album { get; }
        public DateTimeOffset? Timestamp { get; }
        public string Url { get; }
        public string HashId => _hashId ?? (_hashId = LastFmClient.GetTrackId(Artist.Name, Name));

        private string _hashId;

        internal LastFmRecentTrack(string name, bool nowPlaying, string artistName, string albumName, Uri imageUri, DateTimeOffset? timestamp = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            NowPlaying = nowPlaying;
            Album = new LastFmAlbum(albumName, new LastFmArtist(artistName), imageUri: imageUri);
            Timestamp = timestamp;
            Url = LastFmClient.GetTrackUrl(artistName, name);
        }

        public LastFmTrack ToTrack(int? playcount = null) => new LastFmTrack(Name, Album, playcount);
    }
}
