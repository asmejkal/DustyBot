using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Extensions.Interactivity.Menus.Paged;

namespace DustyBot.Framework.Interactivity
{
    public class FieldListingPageProvider : PageProvider
    {
        public override int PageCount => Pages.Count;
        public IReadOnlyCollection<Page> Pages { get; }

        public FieldListingPageProvider(
            IEnumerable<LocalEmbedField> items,
            Action<LocalEmbed>? embedBuilder = null,
            int maxItemsPerPage = 10)
        {
            var pages = new List<Page>();
            foreach (var chunk in items.Chunk(maxItemsPerPage))
            {
                var embed = new LocalEmbed().WithFields(chunk);
                embedBuilder?.Invoke(embed);
                pages.Add(new Page().WithEmbeds(embed));
            }

            Pages = pages;
        }

        public override ValueTask<Page?> GetPageAsync(PagedViewBase view) =>
            new(Pages.ElementAtOrDefault(view.CurrentPageIndex));
    }
}
