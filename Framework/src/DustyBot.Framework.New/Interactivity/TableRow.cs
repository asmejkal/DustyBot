using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Framework.Interactivity
{
    public class TableRow
    {
        public readonly struct Cell
        {
            public IEnumerable<string?> Values { get; }
            public TableColumnFlags Flags { get; }

            public Cell(IEnumerable<string?> values, TableColumnFlags flags)
            {
                Values = values ?? throw new ArgumentNullException(nameof(values));
                Flags = flags;
            }
        }

        public IReadOnlyDictionary<string, Cell> Cells => _cells;

        private readonly Dictionary<string, Cell> _cells;

        public TableRow(IEnumerable<KeyValuePair<string, IEnumerable<string?>>> cells)
            : this(cells.Select(x => KeyValuePair.Create(x.Key, new Cell(x.Value, TableColumnFlags.None))))
        {
        }

        public TableRow(IEnumerable<KeyValuePair<string, Cell>> cells)
        {
            _cells = new(cells);
        }

        public TableRow()
        {
            _cells = new();
        }

        public TableRow Add(string key, string? value, TableColumnFlags flags = TableColumnFlags.None) => Add(key, new[] { value }, flags);

        public TableRow Add(string key, params string?[] values) => Add(key, values.AsEnumerable());
        public TableRow Add(string key, TableColumnFlags flags, params string?[] values) => Add(key, values.AsEnumerable(), flags);

        public TableRow Add(string key, IEnumerable<string?> values, TableColumnFlags flags = TableColumnFlags.None)
        {
            _cells.Add(key, new Cell(values, flags));
            return this;
        }
    }
}
