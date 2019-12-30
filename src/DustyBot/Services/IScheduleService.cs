using DustyBot.Framework.Services;
using DustyBot.Settings;
using System;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    interface IScheduleService : IService
    {
        Task RefreshNotifications(ulong serverId, ScheduleSettings settings);
    }
}
