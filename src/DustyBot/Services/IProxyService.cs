using System.Net;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    internal interface IProxyService
    {
        Task<WebProxy> GetProxyAsync();
        Task BlacklistProxyAsync(WebProxy proxy);
    }
}
