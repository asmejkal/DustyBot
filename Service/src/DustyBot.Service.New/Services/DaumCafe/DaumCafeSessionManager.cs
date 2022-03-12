using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections.DaumCafe.Models;
using DustyBot.Database.Services;
using DustyBot.DaumCafe;
using DustyBot.Framework.Logging;
using DustyBot.Service.Helpers.DaumCafe.Exceptions;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.DaumCafe
{
    internal class DaumCafeSessionManager : IDaumCafeSessionManager
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);

        private readonly Dictionary<Guid, Tuple<DateTime, DaumCafeSession>> _sessionCache = new();
        private readonly ILogger<DaumCafeSessionManager> _logger;
        private readonly IDaumCafeSettingsService _userSettings;

        public DaumCafeSessionManager(
            ILogger<DaumCafeSessionManager> logger,
            IDaumCafeSettingsService userSettings)
        {
            _logger = logger;
            _userSettings = userSettings;
        }

        public async Task<DaumCafeSession> GetSessionAsync(DaumCafeFeed feed, CancellationToken ct)
        {
            if (feed.CredentialId == Guid.Empty)
                return DaumCafeSession.Anonymous;

            if (_sessionCache.TryGetValue(feed.CredentialId, out var dateSession) && DateTime.Now - dateSession.Item1 <= SessionLifetime)
                return dateSession.Item2;

            var credentials = await _userSettings.GetCredentialAsync(feed.CredentialUser, feed.CredentialId, ct);
            var session = DaumCafeSession.Anonymous;
            if (credentials != null)
            {
                try
                {
                    session = await DaumCafeSession.Create(credentials.Login, credentials.Password, ct);
                }
                catch (Exception ex) when (ex is CountryBlockException || ex is LoginFailedException)
                {
                    _logger.WithUser(feed.CredentialUser)
                        .LogInformation("Credential {CredentialId} is invalid, proceeding with an anonymous session", feed.CredentialId);
                }
            }
            else
            {
                _logger.WithUser(feed.CredentialUser)
                    .LogInformation("Credential {CredentialId} not found, proceeding with an anonymous session", feed.CredentialId);
            }

            _sessionCache[feed.CredentialId] = Tuple.Create(DateTime.Now, session);
            return session;
        }
    }
}
