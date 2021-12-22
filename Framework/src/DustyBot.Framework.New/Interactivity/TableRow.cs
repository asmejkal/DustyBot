using System.Collections.Generic;

namespace DustyBot.Framework.Interactivity
{
    public class TableRow
    {
        public IReadOnlyDictionary<string, IEnumerable<string>> Columns => _columns;

        private readonly Dictionary<string, IEnumerable<string>> _columns;

        public TableRow(IEnumerable<KeyValuePair<string, IEnumerable<string>>> columns)
        {
            _columns = new Dictionary<string, IEnumerable<string>>(columns);
        }

        public TableRow()
        {
            _columns = new Dictionary<string, IEnumerable<string>>();
        }

        public TableRow Add(string key, string value) => Add(key, new[] { value });

        public TableRow Add(string key, params string[] values) => Add(key, values);

        public TableRow Add(string key, IEnumerable<string> values)
        {
            _columns.Add(key, values);
            return this;
        }
    }
}
