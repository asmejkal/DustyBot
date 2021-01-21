using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;

namespace DustyBot.Services
{
    internal interface IScheduleService
    {
        Task RefreshNotifications(ulong serverId, ScheduleSettings settings);
    }
}
