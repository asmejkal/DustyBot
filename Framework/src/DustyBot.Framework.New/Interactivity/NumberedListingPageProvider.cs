using System;
using System.Collections.Generic;
using System.Linq;
using Disqord;

namespace DustyBot.Framework.Interactivity
{
    public class NumberedListingPageProvider : ListingPageProvider
    {
        public static readonly Func<int, string> DefaultPrefixFormatter = static x => $"`{x}.` ";

        public NumberedListingPageProvider(
            IEnumerable<string> items,
            Action<LocalEmbed>? embedBuilder = null,
            Func<int, string>? prefixFormatter = null, 
            int maxItemsPerPage = 15)
            : base(items.Select((x, i) => (prefixFormatter ?? DefaultPrefixFormatter).Invoke(i + 1) + x), embedBuilder, maxItemsPerPage)
        {
        }

        public NumberedListingPageProvider(
            IEnumerable<string> items,
            string title,
            Func<int, string>? prefixFormatter = null,
            int maxItemsPerPage = 15)
            : this(items, x => x.WithTitle(title), prefixFormatter, maxItemsPerPage)
        {
        }
    }
}
