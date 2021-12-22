using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Extensions.Interactivity.Menus.Paged;
using DustyBot.Core.Formatting;

namespace DustyBot.Framework.Interactivity
{
    public class ListingPageProvider : PageProvider
    {
        public override int PageCount => Pages.Count;
        public IReadOnlyCollection<Page> Pages { get; }

        public ListingPageProvider(
            IEnumerable<string> items,
            Action<LocalEmbed>? embedBuilder = null,
            int maxItemsPerPage = 15)
        {
            Page BuildPage(string description)
            {
                var embed = new LocalEmbed().WithDescription(description);
                embedBuilder?.Invoke(embed);
                return new Page().WithEmbeds(embed);
            }

            var pages = new List<Page>();
            var content = new StringBuilder();
            var currentRows = 0;
            foreach (var (item, i) in items.Select((x, i) => (x, i)))
            {
                var line = item.Truncate(LocalEmbed.MaxDescriptionLength - 1);
                if (++currentRows > maxItemsPerPage || !content.TryAppendLineLimited(line, LocalEmbed.MaxDescriptionLength))
                {
                    pages.Add(BuildPage(content.ToString()));
                    content.Clear();
                    currentRows = 1;

                    content.Append(line + '\n');
                }  
            }

            if (content.Length > 0)
                pages.Add(BuildPage(content.ToString()));

            Pages = pages;
        }

        public ListingPageProvider(
            IEnumerable<string> items,
            string title,
            int maxItemsPerPage = 15)
            : this(items, x => x.WithTitle(title), maxItemsPerPage)
        {
        }

        public override ValueTask<Page?> GetPageAsync(PagedViewBase view) =>
            new(Pages.ElementAtOrDefault(view.CurrentPageIndex));
    }
}
