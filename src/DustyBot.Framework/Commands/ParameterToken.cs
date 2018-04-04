using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public class ParameterToken
    {
        private string _parameter;

        public ParameterToken(string parameter)
        {
            _parameter = parameter;
        }

        public bool IsType(ParameterType type) => GetValue(type) != null;

        public dynamic GetValue(ParameterType type)
        {
            dynamic value;

            switch (type)
            {
                case ParameterType.Short: value = (short?)this; break;
                case ParameterType.UShort: value = (ushort?)this; break;
                case ParameterType.Int: value = (int?)this; break;
                case ParameterType.UInt: value = (uint?)this; break;
                case ParameterType.Long: value = (long?)this; break;
                case ParameterType.ULong: value = (ulong?)this; break;
                case ParameterType.Double: value = (double?)this; break;
                case ParameterType.Float: value = (float?)this; break;
                case ParameterType.Decimal: value = (decimal?)this; break;
                case ParameterType.Bool: value = (bool?)this; break;
                case ParameterType.String: value = (string)this; break;
                case ParameterType.Uri: value = (Uri)this; break;
                default: value = null; break;
            }

            return value;
        }

        public static explicit operator short?(ParameterToken value)
        {
            short result;
            return short.TryParse(value._parameter, out result) ? new short?(result) : null;
        }

        public static explicit operator ushort?(ParameterToken value)
        {
            ushort result;
            return ushort.TryParse(value._parameter, out result) ? new ushort?(result) : null;
        }

        public static explicit operator int?(ParameterToken value)
        {
            int result;
            return int.TryParse(value._parameter, out result) ? new int?(result) : null;
        }

        public static explicit operator uint?(ParameterToken value)
        {
            uint result;
            return uint.TryParse(value._parameter, out result) ? new uint?(result) : null;
        }

        public static explicit operator long?(ParameterToken value)
        {
            long result;
            return long.TryParse(value._parameter, out result) ? new long?(result) : null;
        }

        public static explicit operator ulong?(ParameterToken value)
        {
            ulong result;
            return ulong.TryParse(value._parameter, out result) ? new ulong?(result) : null;
        }

        public static explicit operator double?(ParameterToken value)
        {
            double result;
            return double.TryParse(value._parameter, out result) ? new double?(result) : null;
        }

        public static explicit operator float?(ParameterToken value)
        {
            float result;
            return float.TryParse(value._parameter, out result) ? new float?(result) : null;
        }

        public static explicit operator decimal?(ParameterToken value)
        {
            decimal result;
            return decimal.TryParse(value._parameter, out result) ? new decimal?(result) : null;
        }

        public static explicit operator bool?(ParameterToken value)
        {
            bool result;
            return bool.TryParse(value._parameter, out result) ? new bool?(result) : null;
        }

        public static explicit operator string(ParameterToken value)
        {
            return value._parameter;
        }

        public static explicit operator Uri(ParameterToken value)
        {
            try
            {
                return new Uri(value._parameter);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
