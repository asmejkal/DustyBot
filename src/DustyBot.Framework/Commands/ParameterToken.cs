using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public class ParameterToken
    {
        private string _parameter;
        private dynamic _value = null;
        private SocketGuild _guild;

        public ParameterToken(string parameter, SocketGuild guild)
        {
            _parameter = parameter;
            _guild = guild;
        }

        public bool IsType(ParameterType type) => GetValue(type) != null;

        public dynamic GetValue(ParameterType type)
        {
            switch (type)
            {
                case ParameterType.Short: _value = AsShort; break;
                case ParameterType.UShort: _value = AsUShort; break;
                case ParameterType.Int: _value = AsInt; break;
                case ParameterType.UInt: _value = AsUInt; break;
                case ParameterType.Long: _value = AsLong; break;
                case ParameterType.ULong: _value = AsULong; break;
                case ParameterType.Double: _value = AsDouble; break;
                case ParameterType.Float: _value = AsFloat; break;
                case ParameterType.Decimal: _value = AsDecimal; break;
                case ParameterType.Bool: _value = AsBool; break;
                case ParameterType.String: _value = AsString; break;
                case ParameterType.Uri: _value = AsUri; break;
                case ParameterType.TextChannel: _value = AsTextChannel; break;
                case ParameterType.GuildUser: _value = AsGuildUser; break;
                case ParameterType.Role: _value = AsRole; break;
                default: _value = null; break;
            }

            return _value;
        }

        private dynamic GetTyped(Type type) => _value is Type ? _value : null;

        public static implicit operator short?(ParameterToken token)
        {
            short result;
            return token.GetTyped(typeof(short?)) ?? (token._value = short.TryParse(token._parameter, out result) ? new short?(result) : null);
        }

        public static implicit operator ushort?(ParameterToken token)
        {
            ushort result;
            return token.GetTyped(typeof(ushort?)) ?? (token._value = ushort.TryParse(token._parameter, out result) ? new ushort?(result) : null);
        }

        public static implicit operator int?(ParameterToken token)
        {
            int result;
            return token.GetTyped(typeof(int?)) ?? (token._value = int.TryParse(token._parameter, out result) ? new int?(result) : null);
        }

        public static implicit operator uint?(ParameterToken token)
        {
            uint result;
            return token.GetTyped(typeof(uint?)) ?? (token._value = uint.TryParse(token._parameter, out result) ? new uint?(result) : null);
        }

        public static implicit operator long?(ParameterToken token)
        {
            long result;
            return token.GetTyped(typeof(long?)) ?? (token._value = long.TryParse(token._parameter, out result) ? new long?(result) : null);
        }

        public static implicit operator ulong?(ParameterToken token)
        {
            ulong result;
            return token.GetTyped(typeof(ulong?)) ?? (token._value = ulong.TryParse(token._parameter, out result) ? new ulong?(result) : null);
        }

        public static implicit operator double?(ParameterToken token)
        {
            double result;
            return token.GetTyped(typeof(double?)) ?? (token._value = double.TryParse(token._parameter, out result) ? new double?(result) : null);
        }

        public static implicit operator float?(ParameterToken token)
        {
            float result;
            return token.GetTyped(typeof(float?)) ?? (token._value = float.TryParse(token._parameter, out result) ? new float?(result) : null);
        }

        public static implicit operator decimal?(ParameterToken token)
        {
            decimal result;
            return token.GetTyped(typeof(decimal?)) ?? (token._value = decimal.TryParse(token._parameter, out result) ? new decimal?(result) : null);
        }

        public static implicit operator bool?(ParameterToken token)
        {
            bool result;
            return token.GetTyped(typeof(bool?)) ?? (token._value = bool.TryParse(token._parameter, out result) ? new bool?(result) : null);
        }

        public static implicit operator string(ParameterToken token)
        {
            return token.GetTyped(typeof(string)) ?? (token._value = token._parameter);
        }

        public static implicit operator Uri(ParameterToken token)
        {
            try
            {
                return token.GetTyped(typeof(Uri)) ?? (token._value = new Uri(token._parameter));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public short? AsShort => this;
        public ushort? AsUShort => this;
        public int? AsInt => this;
        public uint? AsUInt => this;
        public long? AsLong => this;
        public ulong? AsULong => this;
        public double? AsDouble => this;
        public float? AsFloat => this;
        public decimal? AsDecimal => this;
        public bool? AsBool => this;
        public string AsString => this;
        public Uri AsUri => this;

        private static Regex ChannelMentionRegex = new Regex("<#([0-9]+)>", RegexOptions.Compiled);
        public ITextChannel AsTextChannel
        {
            get
            {
                if (_value != null && _value is ITextChannel)
                    return _value;

                if (_parameter == null)
                    return null;

                ulong id;
                if (!ulong.TryParse(_parameter, out id))
                {
                    var match = ChannelMentionRegex.Match(_parameter);
                    if (!match.Success)
                        return null;

                    id = ulong.Parse(match.Groups[1].Value);
                }
                
                return _value = _guild?.TextChannels.FirstOrDefault(x => x.Id == id);
            }
        }

        private static Regex UserMentionRegex = new Regex("<@([0-9]+)>", RegexOptions.Compiled);
        public IGuildUser AsGuildUser
        {
            get
            {
                if (_value != null && _value is IGuildUser)
                    return _value;

                if (_parameter == null)
                    return null;

                ulong id;
                if (!ulong.TryParse(_parameter, out id))
                {
                    var match = UserMentionRegex.Match(_parameter);
                    if (!match.Success)
                        return null;

                    id = ulong.Parse(match.Groups[1].Value);
                }

                return _value = _guild?.Users.FirstOrDefault(x => x.Id == id);
            }
        }

        public IRole AsRole
        {
            get
            {
                if (_value != null && _value is IRole)
                    return _value;

                if (_parameter == null)
                    return null;

                var role = _guild?.Roles.FirstOrDefault(x => string.Equals(x.Name, _parameter, StringComparison.CurrentCultureIgnoreCase));
                if (role != null)
                    return _value = role;

                ulong id;
                if (!ulong.TryParse(_parameter, out id))
                    return null;

                return _value = _guild?.GetRole(id);
            }
        }
    }
}
