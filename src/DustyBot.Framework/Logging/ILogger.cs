using System.Threading.Tasks;

namespace DustyBot.Framework.Logging
{
    public interface ILogger
    {
        Task Log(Discord.LogMessage message);
    }
}
