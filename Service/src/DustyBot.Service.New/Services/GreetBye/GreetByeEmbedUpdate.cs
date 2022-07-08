using Qommon;

namespace DustyBot.Service.Services.GreetBye
{
    public class GreetByeEmbedUpdate
    {
        public Optional<string?> Title { get; set; }
        public Optional<string?> Text { get; set; }
        public Optional<string?> Footer { get; set; }
    }
}
