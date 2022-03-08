using Disqord;

namespace DustyBot.Framework.Communication
{
    public static class LocalMessageExtensions
    {
        public static T WithDisallowedMentions<T>(this T x)
            where T : LocalMessageBase
        {
            return x.WithAllowedMentions(LocalAllowedMentions.None);
        }
    }
}
