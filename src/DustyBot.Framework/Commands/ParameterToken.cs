using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DustyBot.Framework.Utility;
using System.Globalization;
using DustyBot.Core.Utility;

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

            public bool TryGet(ParameterType type, out dynamic value)
            {
                if (_type == type)
                {
                    value = _value;
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }

            public ParameterType Type => _type;
        }

        private class ValueCache
        {
            Dictionary<ParameterType, DynamicValue> _data = new Dictionary<ParameterType, DynamicValue>();

            public bool TryGet(ParameterType type, out dynamic value)
            {
                value = null;
                DynamicValue cached;
                return _data.TryGetValue(type, out cached) ? cached.TryGet(type, out value) : false;
            }

            public void Add(DynamicValue value) => _data.Add(value.Type, value);
            public void Remove(ParameterType type) => _data.Remove(type);
        }

        public static readonly ParameterToken Empty = new ParameterToken();

        ValueCache _cache = new ValueCache();
        Regex _regex;

        public SocketGuild Guild { get; }
        public string Raw { get; } = string.Empty;
        public string LastError { get; private set; }
        public ParameterRegistration Registration { get; set; }
        public bool HasValue => !string.IsNullOrEmpty(Raw);
        public int Begin { get; }
        public int End { get; }
        public IList<ParameterToken> Repeats { get; } = new List<ParameterToken>();
        public Regex Regex { get => _regex; set { _regex = value; _cache.Remove(ParameterType.Regex); } }

        public ParameterToken this[int key] => Repeats.ElementAtOrDefault(key) ?? Empty;

        private ParameterToken()
        {
        }

        public ParameterToken(StringExtensions.Token token, SocketGuild guild)
        {
            Repeats.Add(this);
            Begin = token.Begin;
            End = token.End;
            Raw = token.Value ?? string.Empty;
            Guild = guild;
        }

        public ParameterToken(ParameterRegistration registration, int begin, int end, string value, SocketGuild guild)
        {
            Repeats.Add(this);
            Registration = registration;
            Begin = begin;
            End = end;
            Raw = value ?? string.Empty;
            Guild = guild;
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
                case ParameterType.Regex: result = AsRegex; break;
                case ParameterType.ColorCode: result = AsColorCode; break;
                case ParameterType.Id: result = AsId; break;
                case ParameterType.MentionOrId: result = AsMentionOrId; break;
                case ParameterType.TextChannel: result = AsTextChannel; break;
                case ParameterType.GuildUser: result = AsGuildUser; break;
                case ParameterType.GuildUserOrName: result = AsGuildUserOrName; break;
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
        public Uri AsUri => TryConvert<Uri>(this, ParameterType.Uri, x => new Uri(DiscordHelpers.TrimLinkBraces(x)));
        public Guid? AsGuid => TryConvert<Guid>(this, ParameterType.Guid, Guid.TryParse);
        public Match AsRegex => TryConvert<Match>(this, ParameterType.Regex, x => Regex?.Match(x));
        public ulong? AsId => TryConvert<ulong>(this, ParameterType.Id, ulong.TryParse);

        private static bool TryParseMentionOrId(string value, out ulong id)
        {
            if (!ulong.TryParse(value, out id))
            {
                var match = UserMentionRegex.Match(value);
                if (!match.Success)
                    return false;

                id = ulong.Parse(match.Groups[1].Value);
            }

            return true;
        }

        public ulong? AsMentionOrId => TryConvert<ulong>(this, ParameterType.MentionOrId, TryParseMentionOrId);

        private static Regex ColorCodeRegex = new Regex("^#?([a-fA-F0-9]+)$", RegexOptions.Compiled);
        private static bool TryParseColorCode(string value, out uint result)
        {
            var match = ColorCodeRegex.Match(value);
            if (match.Success)
            {
                var number = match.Groups[1].Value;
                return uint.TryParse(number, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
            }

            result = 0;
            return false;
        }

        public uint? AsColorCode => TryConvert<uint>(this, ParameterType.ColorCode, TryParseColorCode);

        private static Regex ChannelMentionRegex = new Regex("<#([0-9]+)>", RegexOptions.Compiled);
        public ITextChannel AsTextChannel => TryConvert<ITextChannel>(this, ParameterType.TextChannel, x =>
        {
            ulong id;
            if (ulong.TryParse(x, out id))
                return Guild?.TextChannels.FirstOrDefault(y => y.Id == id);

            var match = ChannelMentionRegex.Match(x);
            if (match.Success)
            {
                id = ulong.Parse(match.Groups[1].Value);
                return Guild?.TextChannels.FirstOrDefault(y => y.Id == id);
            }

            return Guild?.TextChannels.FirstOrDefault(y => string.Compare(y.Name, x, true) == 0);
        });

        private static readonly Regex UserMentionRegex = new Regex("<@!?([0-9]+)>", RegexOptions.Compiled);
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

            return Guild?.Users.FirstOrDefault(y => y.Id == id);
        });

        public Tuple<IGuildUser, string> AsGuildUserOrName => TryConvert<Tuple<IGuildUser, string>>(this, ParameterType.GuildUserOrName, x =>
        {
            if (string.IsNullOrEmpty(x))
                return null;

            ulong id;
            if (!ulong.TryParse(x, out id))
            {
                var match = UserMentionRegex.Match(x);
                if (!match.Success)
                    return Tuple.Create((IGuildUser)null, x);

                id = ulong.Parse(match.Groups[1].Value);
            }

            var user = (IGuildUser)Guild?.Users.FirstOrDefault(y => y.Id == id);
            if (user == null)
                return null;

            return Tuple.Create((IGuildUser)Guild?.Users.FirstOrDefault(y => y.Id == id), (string)null);
        });

        public IRole AsRole => TryConvert<IRole>(this, ParameterType.Role, x =>
        {
            var role = Guild?.Roles.FirstOrDefault(r => string.Compare(r.Name, x) == 0);
            if (role != null)
                return role;

            role = Guild?.Roles.FirstOrDefault(r => string.Compare(r.Name, x, true) == 0);
            if (role != null)
                return role;

            ulong id;
            if (!ulong.TryParse(x, out id))
            {
                var match = DiscordHelpers.RoleMentionRegex.Match(x);
                if (!match.Success)
                    return null;

                id = ulong.Parse(match.Groups[1].Value);
            }

            return Guild?.GetRole(id);
        });

        public async Task<IUserMessage> AsGuildUserMessage() => await TryConvert<IUserMessage>(this, ParameterType.GuildUserMessage, async x =>
        {
            if (AsULong == null || Guild == null)
                return null;

            var message = (await Guild.GetMessageAsync(AsULong.Value).ConfigureAwait(false)) as IUserMessage;
            if (message == null)
                LastError = Properties.Resources.Parameter_UserMessageNotFound;

            return message;
        });

        public async Task<IUserMessage> AsGuildSelfMessage() => await TryConvert<IUserMessage>(this, ParameterType.GuildSelfMessage, async x =>
        {
            var message = await AsGuildUserMessage();
            if (message == null || Guild == null)
                return null;

            if (message.Author.Id != Guild.CurrentUser.Id)
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
        public static explicit operator Match (ParameterToken token) => token.AsRegex;

        static T TryConvert<T>(ParameterToken token, ParameterType type, Func<string, T> parser)
            where T : class
        {
            dynamic result;
            if (token._cache.TryGet(type, out result))
                return result;

            try
            {
                result = parser(token.Raw);
            }
            catch (Exception)
            {
                result = null;
            }

            token._cache.Add(new DynamicValue(result, type));
            return result;
        }

        static async Task<T> TryConvert<T>(ParameterToken token, ParameterType type, Func<string, Task<T>> parser)
            where T : class
        {
            dynamic result;
            if (token._cache.TryGet(type, out result))
                return result;

            try
            {
                result = await parser(token.Raw);
            }
            catch (Exception)
            {
                result = null;
            }

            token._cache.Add(new DynamicValue(result, type));
            return result;
        }

        public delegate bool TryParseDelegate<T>(string value, out T result);
        static T? TryConvert<T>(ParameterToken token, ParameterType type, TryParseDelegate<T> parser)
            where T : struct
        {
            dynamic cache;
            if (token._cache.TryGet(type, out cache))
                return cache;

            T tmp;
            T? result = parser(token.Raw, out tmp) ? new T?(tmp) : new T?();
            token._cache.Add(new DynamicValue(result, type));

            return result;
        }

        public override string ToString() => Raw;
    }
}
