using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using StoredProcedureEFCore;

namespace Safetica.Database.Core.StoredProcedures
{
    /// <summary>
    /// Wrapper around IStoredProcBuilder so we can use simplified and unified interface.
    /// </summary>
    public class StoredProcedureBuilder
    {
        private readonly List<Action> _completionRoutines = new List<Action>();
        private IStoredProcBuilder Builder { get; }

        private void RunCompletionRoutines()
        {
            _completionRoutines.ForEach(x => x.Invoke());
        }

        public StoredProcedureBuilder(IStoredProcBuilder builder)
        {
            Builder = builder;
        }

        public StoredProcedureBuilder AddParam<T>(string parameterName, T val)
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                var underlyingType = type.GetEnumUnderlyingType();

                if (underlyingType == typeof(int))
                    Builder.AddParam(parameterName, Convert.ToInt32(val));
                else if (underlyingType == typeof(int?))
                    Builder.AddParam(parameterName, (int?)Convert.ChangeType(val, typeof(int?)));
                else
                    throw new NotSupportedException("Only integers are allowed as underlying type of enums");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
                Nullable.GetUnderlyingType(type) != null && Nullable.GetUnderlyingType(type).IsEnum)
            {
                Builder.AddParam(parameterName, val != null ? (int?)Convert.ToInt32(val) : null);
            }
            else
            {
                Builder.AddParam(parameterName, val);
            }

            return this;
        }

        public StoredProcedureBuilder AddParam<T>(string parameterName, IEnumerable<T> collection, string typeName)
        {
            Builder.AddParam(new SqlParameter(parameterName, SqlDbType.Structured)
            {
                Value = collection.ToDataTable(),
                TypeName = typeName
            });

            return this;
        }

        public StoredProcedureBuilder AddParamEx<T>(string parameterName, IInOutParameter<T> inoutVal)
        {
            var res = AddParam(parameterName, inoutVal.Value, out IOutParameter<T> outVal);
            _completionRoutines.Add(() => inoutVal.Value = outVal.Value);
            return res;
        }

        public StoredProcedureBuilder AddParam<T>(string parameterName, T val, out IOutParameter<T> outVal)
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                var underlyingType = type.GetEnumUnderlyingType();

                if (underlyingType == typeof(int))
                    outVal = Builder.AddOutParam<T, int>(parameterName, Convert.ToInt32(val));
                else if (underlyingType == typeof(int?))
                    outVal = Builder.AddOutParam<T, int?>(parameterName, (int?)Convert.ChangeType(val, typeof(int?)));
                else
                    throw new NotSupportedException("Only integers are allowed as underlying type of enums");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
                Nullable.GetUnderlyingType(type) != null && Nullable.GetUnderlyingType(type).IsEnum)
            {
                outVal = Builder.AddOutParam<T, int?>(parameterName, val != null ? (int?)Convert.ToInt32(val) : null);
            }
            else
            {
                outVal = Builder.AddOutParam<T, T>(parameterName, val);
            }

            return this;
        }

        public StoredProcedureBuilder AddParamEx<T>(string parameterName, IInOutParameter<T> inoutVal, int size = 0, byte precision = 0, byte scale = 0)
        {
            var res = AddParam(parameterName, inoutVal.Value, out IOutParameter<T> outVal, size, precision, scale);
            _completionRoutines.Add(() => inoutVal.Value = outVal.Value);
            return res;
        }

        public StoredProcedureBuilder AddParam<T>(string parameterName, T val, out IOutParameter<T> outVal, int size = 0, byte precision = 0, byte scale = 0)
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                var underlyingType = type.GetEnumUnderlyingType();

                if (underlyingType == typeof(int))
                    outVal = Builder.AddOutParam<T, int>(parameterName, Convert.ToInt32(val), size, precision, scale);
                else if (underlyingType == typeof(int?))
                    outVal = Builder.AddOutParam<T, int?>(parameterName, (int?)Convert.ChangeType(val, typeof(int?)), size, precision, scale);
                else
                    throw new NotSupportedException("Only integers are allowed as underlying type of enums");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
                Nullable.GetUnderlyingType(type) != null && Nullable.GetUnderlyingType(type).IsEnum)
            {
                outVal = Builder.AddOutParam<T, int?>(parameterName, val != null ? (int?)Convert.ToInt32(val) : null);
            }
            else
            {
                outVal = Builder.AddOutParam<T, T>(parameterName, val, size, precision, scale);
            }

            return this;
        }

        public StoredProcedureBuilder AddParam<T>(string parameterName, out IOutParameter<T> outVal)
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                var underlyingType = type.GetEnumUnderlyingType();

                if (underlyingType == typeof(int))
                    outVal = Builder.AddOutParam<T, int>(parameterName);
                else if (underlyingType == typeof(int?))
                    outVal = Builder.AddOutParam<T, int?>(parameterName);
                else
                    throw new NotSupportedException("Only integers are allowed as underlying type of enums");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
                Nullable.GetUnderlyingType(type) != null && Nullable.GetUnderlyingType(type).IsEnum)
            {
                outVal = Builder.AddOutParam<T, int?>(parameterName);
            }
            else
            {
                outVal = Builder.AddOutParam<T, T>(parameterName);
            }

            return this;
        }

        public StoredProcedureBuilder AddParam<T>(string parameterName, out IOutParameter<T> outVal, int size = 0, byte precision = 0, byte scale = 0)
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                var underlyingType = type.GetEnumUnderlyingType();

                if (underlyingType == typeof(int))
                    outVal = Builder.AddOutParam<T, int>(parameterName, size, precision, scale);
                else if (underlyingType == typeof(int?))
                    outVal = Builder.AddOutParam<T, int?>(parameterName, size, precision, scale);
                else
                    throw new NotSupportedException("Only integers are allowed as underlying type of enums");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
                Nullable.GetUnderlyingType(type) != null && Nullable.GetUnderlyingType(type).IsEnum)
            {
                outVal = Builder.AddOutParam<T, int?>(parameterName, size, precision, scale);
            }
            else
            {
                outVal = Builder.AddOutParam<T, T>(parameterName, size, precision, scale);
            }

            return this;
        }

        /// <summary>
        /// Executes a stored procedure and returns the single result from the result set returned by the query.
        /// </summary>
        /// <typeparam name="TEntity">Type of the result</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The first result from the reader</returns>
        public async Task<TEntity> ExecuteReaderSingleAsync<TEntity>(CancellationToken cancellationToken)
            where TEntity : class, new()
        {
            TEntity entity = null;

            await Builder.ExecAsync(async reader => entity = await reader.SingleAsync<TEntity>(cancellationToken), cancellationToken);

            RunCompletionRoutines();

            return entity;
        }

        /// <summary>
        /// Executes a stored procedure and returns the single or default result from the result set returned by the query.
        /// </summary>
        /// <typeparam name="TEntity">Type of the result</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The first result from the reader</returns>
        public async Task<TEntity> ExecuteReaderSingleOrDefaultAsync<TEntity>(CancellationToken cancellationToken)
            where TEntity : class, new()
        {
            TEntity entity = null;

            await Builder.ExecAsync(async reader => entity = await reader.SingleOrDefaultAsync<TEntity>(cancellationToken), cancellationToken);

            RunCompletionRoutines();

            return entity;
        }

        /// <summary>
        /// Executes a stored procedure and returns a collection of results from the result set returned by the query.
        /// </summary>
        /// <typeparam name="TEntity">Type of the result</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A collection of results from the reader</returns>
        public async Task<List<TEntity>> ExecuteReaderListAsync<TEntity>(CancellationToken cancellationToken)
            where TEntity : class, new()
        {
            List<TEntity> entities = null;

            await Builder.ExecAsync(async reader => entities = await reader.ToListAsync<TEntity>(cancellationToken), cancellationToken);

            RunCompletionRoutines();

            return entities;
        }

        /// <summary>
        /// Executes a stored procedure and returns the first column of the first row in the result set returned by the query.
        /// </summary>
        /// <typeparam name="T">Type of the result</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return code that indicates the execution status of a procedure</returns>
        public async Task<T> ExecuteScalarAsync<T>(CancellationToken cancellationToken)
        {
            T entity = default;

            var type = typeof(T);
            if (type.IsEnum)
            {
                var underlyingType = type.GetEnumUnderlyingType();

                if (underlyingType == typeof(int))
                    await Builder.ExecScalarAsync<int>(res => entity = StoredProcedureUtility.ConvertResult<T, int>(res), cancellationToken);
                else if (underlyingType == typeof(int?))
                    await Builder.ExecScalarAsync<int?>(res => entity = StoredProcedureUtility.ConvertResult<T, int?>(res), cancellationToken);
                else
                    throw new NotSupportedException("Only integers are allowed as underlying type of enums");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
                Nullable.GetUnderlyingType(type) != null && Nullable.GetUnderlyingType(type).IsEnum)
            {
                await Builder.ExecScalarAsync<int?>(res => entity = StoredProcedureUtility.ConvertResult<T, int?>(res), cancellationToken);
            }
            else
            {
                await Builder.ExecScalarAsync<T>(res => entity = res, cancellationToken);
            }

            RunCompletionRoutines();

            return entity;
        }

        /// <summary>
        /// Executes a stored procedure and returns the number of rows affected.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of rows affected</returns>
        public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            var res = await Builder.ExecNonQueryAsync(cancellationToken);

            RunCompletionRoutines();

            return res;
        }

        /// <summary>
        /// Executes a stored procedure and returns the return code.
        /// </summary>
        /// <typeparam name="T">Type of the result</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return code that indicates the execution status of a procedure</returns>
        public async Task<T> ExecuteNonQueryWithReturnCodeAsync<T>(CancellationToken cancellationToken)
            where T : Enum
        {
            Builder.ReturnValue(out IOutParam<int> outParam);

            var res = await Builder.ExecNonQueryAsync(cancellationToken);

            RunCompletionRoutines();

            return (T)(object)outParam.Value;
        }
    }

    public interface IInOutParameter<T>
    {
        T Value { get; set; }
    }

    public class InOutParameter<TP, T> : IInOutParameter<T>
    {
        private readonly TP _instance;
        private readonly Func<TP, T> _propertyExpression;
        private readonly PropertyInfo _propInfo;
        private readonly FieldInfo _fieldInfo;

        public InOutParameter(TP instance, Expression<Func<TP, T>> propertyExpression)
        {
            _instance = instance;
            _propertyExpression = propertyExpression.Compile();
            _propInfo = ((MemberExpression)propertyExpression.Body).Member as PropertyInfo;
            _fieldInfo = ((MemberExpression)propertyExpression.Body).Member as FieldInfo;
        }

        public T Value
        {
            get { return _propertyExpression.Invoke(_instance); }
            set
            {
                if (_fieldInfo != null)
                {
                    _fieldInfo.SetValue(_instance, value);
                    return;
                }

                _propInfo.SetValue(_instance, value, null);
            }
        }
    }

    /// <summary>
    /// Custom interface so we don't have to import using of the StoredProcedureEFCore library.
    /// </summary>
    /// <typeparam name="T">template</typeparam>
    public interface IOutParameter<T>
    {
        T Value { get; }
    }

    public class OutParameter<T, T2> : IOutParameter<T>
    {
        private readonly IOutParam<T2> _outParam;

        public T Value
        {
            get
            {
                return StoredProcedureUtility.ConvertResult<T, T2>(_outParam.Value);
            }
        }

        public OutParameter(IOutParam<T2> outParam)
        {
            _outParam = outParam;
        }
    }

    public static class BuilderExtensions
    {
        public static OutParameter<T, T2> AddOutParam<T, T2>(this IStoredProcBuilder builder, string parameterName, T2 val)
        {
            builder.AddParam(parameterName, val, out IOutParam<T2> outParam);
            return new OutParameter<T, T2>(outParam);
        }

        public static OutParameter<T, T2> AddOutParam<T, T2>(this IStoredProcBuilder builder, string parameterName)
        {
            builder.AddParam(parameterName, out IOutParam<T2> outParam);
            return new OutParameter<T, T2>(outParam);
        }

        public static OutParameter<T, T2> AddOutParam<T, T2>(this IStoredProcBuilder builder, string parameterName, T2 val, int size = 0, byte precision = 0, byte scale = 0)
        {
            builder.AddParam(parameterName, val, out IOutParam<T2> outParam, size, precision, scale);
            return new OutParameter<T, T2>(outParam);
        }

        public static OutParameter<T, T2> AddOutParam<T, T2>(this IStoredProcBuilder builder, string parameterName, int size = 0, byte precision = 0, byte scale = 0)
        {
            builder.AddParam(parameterName, out IOutParam<T2> outParam, size, precision, scale);
            return new OutParameter<T, T2>(outParam);
        }
    }
}
