using Microsoft.Data.SqlClient;
using StoredProcedureEFCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Sql.StoredProcedures.Utility
{
    public static class StoredProcBuilderExtensions
    {
        public static async Task<IReadOnlyList<T>> ReadListResultAsync<T>(this IStoredProcBuilder builder, CancellationToken ct)
            where T : class, new()
        {
            List<T> result = null;
            await builder.ExecAsync(async reader => result = await reader.ToListAsync<T>(ct), ct);
            return result;
        }

        public static IStoredProcBuilder AddParam<T>(this IStoredProcBuilder builder, string name, IEnumerable<T> values, string typeName)
        {
            builder.AddParam(new SqlParameter(name, SqlDbType.Structured)
            {
                Value = values.ToDataTable(),
                TypeName = typeName
            });

            return builder;
        }

        private static DataTable ToDataTable<T>(this IEnumerable<T> values)
        {
            var table = new DataTable()
            {
                Locale = CultureInfo.InvariantCulture
            };

            var properties = typeof(T).GetProperties();
            var columns = new List<DataColumn>();
            foreach (var info in properties)
            {
                var propertyType = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
                if (propertyType.IsEnum)
                    propertyType = propertyType.GetEnumUnderlyingType();

                columns.Add(new DataColumn(info.Name, propertyType));
            }

            foreach (var item in values)
            {
                var row = new object[properties.Length];
                for (int i = 0; i < properties.Length; ++i)
                    row[i] = properties[i].GetValue(item);

                table.Rows.Add(values);
            }

            return table;
        }
    }
}
