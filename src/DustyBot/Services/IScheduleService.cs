using DustyBot.Settings;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    internal interface IScheduleService
    {
        Task RefreshNotifications(ulong serverId, ScheduleSettings settings);
    }
}
