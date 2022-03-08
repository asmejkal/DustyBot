using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.DaumCafe;

namespace DustyBot.Service.Services.DaumCafe
{
    public interface IDaumCafePostSender
    {
        Task SendPostAsync(IMessageGuildChannel channel, DaumCafePage post, CancellationToken ct);
    }
}