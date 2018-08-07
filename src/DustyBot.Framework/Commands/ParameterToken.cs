using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DustyBot.Framework.Utility;

namespace DustyBot.Framework.Commands
{
    public class ParameterToken
    {
        private struct DynamicValue
        {
            dynamic _value;
            ParameterType _type;

            public DynamicValue(dynamic value, ParameterType type)
            {
                _value = value;
                _type = type;
            }

            public dynamic TryGet(ParameterType type) => _type == type ? _value : null;
            public ParameterType Type => _type;
        }

        DynamicValue _cache;
        SocketGuild _guild;

        public string Raw { get; }
        public string LastError { get; private set; }
        public bool HasValue => !string.IsNullOrEmpty(Raw);

        public ParameterToken(string parameter, SocketGuild guild)
        {
            Raw = parameter ?? string.Empty;
            _guild = guild;
            _cache = new DynamicValue(Raw, ParameterType.String);
        }

        public async Task<bool> IsType(ParameterType type) => (await GetValue(type).ConfigureAwait(false)) != null;

        public async Task<dynamic> GetValue(ParameterType type)
        {
            dynamic result;
            switch (type)
            {
                case ParameterType.Short: result = AsShort; break;
                case ParameterType.UShort: result = AsUShort; break;
                case ParameterType.Int: result = AsInt; break;
                case ParameterType.UInt: result = AsUInt; break;
                case ParameterType.Long: result = AsLong; break;
                case ParameterType.ULong: result = AsULong; break;
                case ParameterType.Double: result = AsDouble; break;
                case ParameterType.Float: result = AsFloat; break;
                case ParameterType.Decimal: result = AsDecimal; break;
                case ParameterType.Bool: result = AsBool; break;
                case ParameterType.String: result = AsString; break;
                case ParameterType.Uri: result = AsUri; break;
                case ParameterType.Guid: result = AsGuid; break;
                case ParameterType.Id: result = AsId; break;
                case ParameterType.TextChannel: result = AsTextChannel; break;
                case ParameterType.GuildUser: result = AsGuildUser; break;
                case ParameterType.Role: result = AsRole; break;
                case ParameterType.GuildUserMessage: result = await AsGuildUserMessage().ConfigureAwait(false); break;
                case ParameterType.GuildSelfMessage: result = await AsGuildSelfMessage().ConfigureAwait(false); break;
                default: result = null; break;
            }

            return result;
        }

        public short? AsShort => TryConvert<short>(this, ParameterType.Short, short.TryParse);
        public ushort? AsUShort => TryConvert<ushort>(this, ParameterType.UShort, ushort.TryParse);
        public int? AsInt => TryConvert<int>(this, ParameterType.Int, int.TryParse);
        public uint? AsUInt => TryConvert<uint>(this, ParameterType.UInt, uint.TryParse);
        public long? AsLong => TryConvert<long>(this, ParameterType.Long, long.TryParse);
        public ulong? AsULong => TryConvert<ulong>(this, ParameterType.ULong, ulong.TryParse);
        public double? AsDouble => TryConvert<double>(this, ParameterType.Double, double.TryParse);
        public float? AsFloat => TryConvert<float>(this, ParameterType.Float, float.TryParse);
        public decimal? AsDecimal => TryConvert<decimal>(this, ParameterType.Decimal, decimal.TryParse);
        public bool? AsBool => TryConvert<bool>(this, ParameterType.Bool, bool.TryParse);
        public string AsString => Raw;
        public Uri AsUri => TryConvert<Uri>(this, ParameterType.Uri, x => new Uri(x));
        public Guid? AsGuid => TryConvert<Guid>(this, ParameterType.Guid, Guid.TryParse);
        public ulong? AsId => TryConvert<ulong>(this, ParameterType.Id, ulong.TryParse);

        private static Regex ChannelMentionRegex = new Regex("<#([0-9]+)>", RegexOptions.Compiled);
        public ITextChannel AsTextChannel => TryConvert<ITextChannel>(this, ParameterType.TextChannel, x =>
        {
            ulong id;
            if (!ulong.TryParse(x, out id))
            {
                var match = ChannelMentionRegex.Match(x);
                if (!match.Success)
                    return null;

                id = ulong.Parse(match.Groups[1].Value);
            }

            return _guild?.TextChannels.FirstOrDefault(y => y.Id == id);
        });

        private static Regex UserMentionRegex = new Regex("<@!?([0-9]+)>", RegexOptions.Compiled);
        public IGuildUser AsGuildUser => TryConvert<IGuildUser>(this, ParameterType.GuildUser, x =>
        {
            ulong id;
            if (!ulong.TryParse(x, out id))
            {
                var match = UserMentionRegex.Match(x);
                if (!match.Success)
                    return null;

                id = ulong.Parse(match.Groups[1].Value);
            }

            return _guild?.Users.FirstOrDefault(y => y.Id == id);
        });

        public IRole AsRole => TryConvert<IRole>(this, ParameterType.Role, x =>
        {
            var role = _guild?.Roles.FirstOrDefault(r => string.Equals(r.Name, x, StringComparison.CurrentCultureIgnoreCase));
            if (role != null)
                return role;

            ulong id;
            if (!ulong.TryParse(x, out id))
                return null;

            return _guild?.GetRole(id);
        });

        public async Task<IUserMessage> AsGuildUserMessage() => await TryConvert<IUserMessage>(this, ParameterType.GuildUserMessage, async x =>
        {
            if (AsULong == null || _guild == null)
                return null;

            var message = (await _guild.GetMessageAsync(AsULong.Value).ConfigureAwait(false)) as IUserMessage;
            if (message == null)
                LastError = Properties.Resources.Parameter_UserMessageNotFound;

            return message;
        });

        public async Task<IUserMessage> AsGuildSelfMessage() => await TryConvert<IUserMessage>(this, ParameterType.GuildSelfMessage, async x =>
        {
            var message = await AsGuildUserMessage();
            if (message == null || _guild == null)
                return null;

            if (message.Author.Id != _guild.CurrentUser.Id)
            {
                LastError = Properties.Resources.Parameter_NotSelfMessage;
                return null;
            }

            return message;
        });

        public static explicit operator short? (ParameterToken token) => token.AsShort;
        public static explicit operator ushort? (ParameterToken token) => token.AsUShort;
        public static explicit operator int? (ParameterToken token) => token.AsInt;
        public static explicit operator uint? (ParameterToken token) => token.AsUInt;
        public static explicit operator long? (ParameterToken token) => token.AsLong;
        public static explicit operator ulong? (ParameterToken token) => token.AsULong;
        public static explicit operator double? (ParameterToken token) => token.AsDouble;
        public static explicit operator float? (ParameterToken token) => token.AsFloat;
        public static explicit operator decimal? (ParameterToken token) => token.AsDecimal;
        public static explicit operator bool? (ParameterToken token) => token.AsBool;
        public static implicit operator string(ParameterToken token) => token.AsString;
        public static explicit operator Uri(ParameterToken token) => token.AsUri;
        public static explicit operator Guid? (ParameterToken token) => token.AsGuid;

        static T TryConvert<T>(ParameterToken token, ParameterType type, Func<string, T> parser)
            where T : class
        {
            T result = token._cache.TryGet(type);
            if (result != null)
                return result;

            try
            {
                result = parser(token.Raw);
            }
            catch (Exception)
            {
                result = null;
            }

            token._cache = new DynamicValue(result, type);
            return result;
        }

        static async Task<T> TryConvert<T>(ParameterToken token, ParameterType type, Func<string, Task<T>> parser)
            where T : class
        {
            T result = token._cache.TryGet(type);
            if (result != null)
                return result;

            try
            {
                result = await parser(token.Raw);
            }
            catch (Exception)
            {
                result = null;
            }

            token._cache = new DynamicValue(result, type);
            return result;
        }

        public delegate bool TryParseDelegate<T>(string value, out T result);
        static T? TryConvert<T>(ParameterToken token, ParameterType type, TryParseDelegate<T> parser)
            where T : struct
        {
            T? cache = token._cache.TryGet(type);
            if (cache != null)
                return cache;

            T tmp;
            T? result = parser(token.Raw, out tmp) ? new T?(tmp) : new T?();
            token._cache = new DynamicValue(result, type);

            return result;
        }
    }
}
