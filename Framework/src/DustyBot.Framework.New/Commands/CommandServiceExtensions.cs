using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class CommandServiceExtensions
    {
        public static void ReplaceTypeParser<T>(this CommandService instance, TypeParser<T> parser, bool replacePrimitive = false)
        {
            var replaced = instance.GetTypeParser<T>();
            if (replaced != null)
                instance.RemoveTypeParser(replaced);

            instance.AddTypeParser(parser);
        }
    }
}
