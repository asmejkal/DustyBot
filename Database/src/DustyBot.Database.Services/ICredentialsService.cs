using System;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Services
{
    public interface ICredentialsService
    {
        Task<UserCredentials> ReadAsync(ulong userId, CancellationToken ct = default);
        Task AddAsync(ulong userId, Credentials credentials, CancellationToken ct = default);
        Task<bool> RemoveAsync(ulong userId, Guid credentialsId, CancellationToken ct = default);
        Task ResetAsync(ulong userId, CancellationToken ct = default);
    }
}