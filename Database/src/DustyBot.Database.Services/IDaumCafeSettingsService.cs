using System;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Services
{
    public interface IDaumCafeSettingsService
    {
        Task<UserDaumCafeSettings?> ReadAsync(ulong userId, CancellationToken ct = default);
        Task<DaumCafeCredential?> GetCredentialAsync(ulong userId, Guid credentialId, CancellationToken ct = default);
        Task AddCredentialAsync(ulong userId, DaumCafeCredential credential, CancellationToken ct = default);
        Task<bool> RemoveCredentialAsync(ulong userId, Guid credentialId, CancellationToken ct = default);
        Task ResetAsync(ulong userId, CancellationToken ct = default);
    }
}