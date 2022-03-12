using System.Linq;
using Disqord;
using Disqord.Extensions.Interactivity.Menus.Paged;

namespace DustyBot.Framework.Interactivity
{
    public class AdaptivePagedView : PagedView
    {
        public AdaptivePagedView(PageProvider pageProvider, LocalMessage? templateMessage = null) 
            : base(pageProvider, templateMessage)
        {
            RemoveComponent(StopButton);

            if (pageProvider.PageCount <= 1)
                ClearComponents();
        }

        protected override void ApplyPageIndex(Page page)
        {
            if (PageProvider.PageCount <= 1)
                return;

            var indexText = $"Page {CurrentPageIndex + 1} of {PageProvider.PageCount}";
            var embed = page.Embeds.LastOrDefault();
            if (embed != null)
            {
                if (embed.Footer != null)
                {
                    if (embed.Footer.Text == null)
                        embed.Footer.Text = indexText;
                    else if (embed.Footer.Text.Length + indexText.Length + 3 <= LocalEmbedFooter.MaxTextLength)
                        embed.Footer.Text = $"{indexText} • " + embed.Footer.Text;
                }
                else
                {
                    embed.WithFooter(indexText);
                }
            }
        }

        protected override LocalMessage GetPagelessMessage()
        {
            return new LocalMessage().WithContent("No items.");
        }
    }
}
