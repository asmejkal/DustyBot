using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Extensions.Interactivity.Menus.Paged;
using DustyBot.Core.Formatting;

namespace DustyBot.Framework.Interactivity
{
    public class TablePageProvider : PageProvider
    {
        public override int PageCount => Pages.Count;
        public IReadOnlyCollection<Page> Pages { get; }

        public TablePageProvider(IEnumerable<TableRow> rows, int maxRowsPerPage = 25)
        {
            var pages = new List<Page>();
            var content = new StringBuilder();
            var currentRows = 0;
            foreach (var row in rows)
            {
                var lineBuilder = new StringBuilder();
                foreach (var (name, cell) in row.Cells)
                {
                    var unquoted = cell.Flags.HasFlag(TableColumnFlags.Unquoted);
                    var value = string.Join(unquoted ? " " : "` `", cell.Values.Where(x => !string.IsNullOrEmpty(x)));

                    if (!string.IsNullOrEmpty(value))
                        lineBuilder.Append(unquoted ? $"{name}: {value} " : $"{name}: `{value}` ");
                }

                var line = lineBuilder.ToString().Truncate(LocalMessage.MaxContentLength - 1);
                if (++currentRows > maxRowsPerPage || !content.TryAppendLineLimited(line.ToString(), LocalMessage.MaxContentLength))
                {
                    pages.Add(new Page().WithContent(content.ToString()));
                    content.Clear();
                    currentRows = 1;

                    content.Append(line + '\n');
                }  
            }

            if (content.Length > 0)
                pages.Add(new Page().WithContent(content.ToString()));

            Pages = pages;
        }

        public override ValueTask<Page?> GetPageAsync(PagedViewBase view) =>
            new(Pages.ElementAtOrDefault(view.CurrentPageIndex));
    }
}
