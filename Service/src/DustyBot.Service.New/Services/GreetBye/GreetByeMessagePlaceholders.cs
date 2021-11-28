namespace DustyBot.Service.Services.GreetBye
{
    internal static class GreetByeMessagePlaceholders
    {
        public const string Mention = "{mention}";
        public const string Name = "{name}";
        public const string FullName = "{fullname}";
        public const string Id = "{id}";
        public const string Server = "{server}";
        public const string MemberCount = "{membercount}";

        public const string PlaceholderList = $"{Mention}, {Name}, {FullName}, {Id}, {Server}, and {MemberCount}";
    }
}
