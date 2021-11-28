using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.TableStorage.Configuration;
using DustyBot.Database.TableStorage.Tables;
using DustyBot.Database.TableStorage.Utility;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace DustyBot.Database.Services
{
    public class SpotifyAccountsService : ISpotifyAccountsService
    {
        private readonly CloudTable _table;

        public SpotifyAccountsService(IOptions<TableStorageOptions> options)
        {
            var storageAccount = CloudStorageAccount.Parse(options.Value.ConnectionString);
            var storageClient = storageAccount.CreateCloudTableClient();
            _table = storageClient.GetTableReference(SpotifyAccount.TableName);
        }

        public async Task<SpotifyAccount?> GetUserAccountAsync(ulong userId, CancellationToken ct)
        {
            var filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, userId.ToString());
            var results = await _table.ExecuteQueryAsync(new TableQuery<SpotifyAccount>().Where(filter), ct);
            if (!results.Any())
                return null;

            return results.Single();
        }

        public async Task AddOrUpdateUserAccountAsync(SpotifyAccount account, CancellationToken ct)
        {
            await _table.CreateIfNotExistsAsync();
            account.PartitionKey = "root";
            account.RowKey = account.UserId;
            account.ETag = "*";
            await _table.ExecuteAsync(TableOperation.InsertOrReplace(account));
        }

        public Task RemoveUserAccountAsync(ulong userId, CancellationToken ct)
        {
            var account = new SpotifyAccount()
            {
                UserId = userId.ToString(),
                PartitionKey = "root",
                RowKey = userId.ToString(),
                ETag = "*"
            };

            return _table.ExecuteAsync(TableOperation.Delete(account));
        }
    }
}
