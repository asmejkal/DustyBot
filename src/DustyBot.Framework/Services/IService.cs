using System.Threading.Tasks;

namespace DustyBot.Framework.Services
{
    public interface IService
    {
        Task StartAsync();
        Task StopAsync();
    }
}
