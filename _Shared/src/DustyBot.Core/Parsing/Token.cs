using System;

namespace DustyBot.Core.Parsing
{
    public struct Token
    {
        public int Begin { get; set; }
        public int End { get; set; }
        public string Value { get; set; }

        public override bool Equals(object obj)
        {
            return obj is Token token &&
                   Begin == token.Begin &&
                   End == token.End &&
                   Value == token.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Begin, End, Value);
        }

        public static bool operator ==(Token left, Token right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Token left, Token right)
        {
            return !(left == right);
        }
    }
}
