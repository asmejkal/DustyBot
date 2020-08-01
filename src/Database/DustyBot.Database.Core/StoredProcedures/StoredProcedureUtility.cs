using System;

namespace Safetica.Database.Core.StoredProcedures
{
    public static class StoredProcedureUtility
    {
        public static T ConvertResult<T, T2>(T2 value)
        {
            var t = typeof(T);

            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (value == null)
                {
                    return default(T);
                }

                t = Nullable.GetUnderlyingType(t);
            }

            if (t.IsEnum)
            {
                return (T)Enum.ToObject(t, value);
            }
            else
            {
                return (T)Convert.ChangeType(value, t);
            }
        }
    }
}
