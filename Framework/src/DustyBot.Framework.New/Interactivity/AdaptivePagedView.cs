using System;
using System.Linq;
using Disqord;
using Disqord.Extensions.Interactivity.Menus.Paged;

namespace DustyBot.Framework.Interactivity
{
    public class AdaptivePagedView : PagedView
    {
        public AdaptivePagedView(PageProvider pageProvider, Action<LocalMessageBase>? messageTemplate = null) 
            : base(pageProvider, messageTemplate)
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
            var embed = page.Embeds.HasValue ? page.Embeds.Value.LastOrDefault() : null;
            if (embed is not null)
            {
                if (embed.Footer.HasValue)
                {
                    if (!embed.Footer.Value.Text.HasValue)
                        embed.Footer.Value.Text = indexText;
                    else if (embed.Footer.Value.Text.Value.Length + indexText.Length + 3 <= Discord.Limits.Message.Embed.Footer.MaxTextLength)
                        embed.Footer.Value.Text = $"{indexText} • " + embed.Footer.Value.Text;
                }
                else
                {
                    embed.WithFooter(indexText);
                }
            }
        }

        protected override Action<LocalMessageBase> GetPagelessMessageTemplate()
        {
            return x => x.WithContent("No items.");
        }
    }
}
