using System;
using System.Net;
using System.Threading.Tasks;

namespace DustyBot.Service.Services
{
    internal interface IProxyService
    {
        Task<WebProxy> GetProxyAsync();
        Task BlacklistProxyAsync(WebProxy proxy, TimeSpan duration);
        Task ForceRefreshAsync();
    }
}
