using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.LastFm.Models
{
    public class LastFmArtistDetail : LastFmArtist
    {
        public IList<LastFmAlbum> TopAlbums { get; }
        public IList<LastFmTrack> TopTracks { get; }

        public int AlbumsListened { get; }
        public int TracksListened { get; }

        internal LastFmArtistDetail(string name, Uri imageUri, IEnumerable<LastFmAlbum> topAlbums, IEnumerable<LastFmTrack> topTracks, int albumsListened, int tracksListened, int playcount = -1)
            : base(name, playcount, imageUri)
        {
            TopAlbums = topAlbums.ToList();
            TopTracks = topTracks.ToList();
            AlbumsListened = albumsListened;
            TracksListened = tracksListened;
        }

        internal LastFmArtistDetail(LastFmArtist artist, Uri imageUri, IEnumerable<LastFmAlbum> topAlbums, IEnumerable<LastFmTrack> topTracks, int albumsListened, int tracksListened, int playcount = -1)
            : base(artist.Name, playcount, imageUri)
        {
            TopAlbums = topAlbums.ToList();
            TopTracks = topTracks.ToList();
            AlbumsListened = albumsListened;
            TracksListened = tracksListened;
        }
    }
}
