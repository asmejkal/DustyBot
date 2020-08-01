using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace Safetica.Database.Core
{
    public static class DataTableExtensions
    {
        /// <summary>
        /// Helper method that creates DataTable from supplied collection of objects (via reflection).
        /// DataTable is used for passing table type into stored procedure.
        /// </summary>
        public static DataTable ToDataTable<T>(this IEnumerable<T> list)
        {
            var type = typeof(T);
            var properties = type.GetProperties();

            var dataTable = new DataTable()
            {
                Locale = CultureInfo.InvariantCulture
            };

            foreach (var info in properties)
            {
                var propertyType = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
                if (propertyType.IsEnum)
                    propertyType = typeof(int);
                dataTable.Columns.Add(new DataColumn(info.Name, propertyType));
            }

            foreach (T entity in list)
            {
                var values = new object[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    values[i] = properties[i].GetValue(entity);
                }

                dataTable.Rows.Add(values);
            }

            return dataTable;
        }
    }
}
