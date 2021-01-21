using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Services.Configuration;
using DustyBot.Database.TableStorage.Tables;
using DustyBot.Database.TableStorage.Utility;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace DustyBot.Database.Services
{
    public class SpotifyAccountsService : ISpotifyAccountsService
    {
        private CloudTable Table { get; }

        public SpotifyAccountsService(IOptions<DatabaseOptions> options)
        {
            var storageAccount = CloudStorageAccount.Parse(options.Value.TableStorageConnectionString);
            var storageClient = storageAccount.CreateCloudTableClient();
            Table = storageClient.GetTableReference(SpotifyAccount.TableName);
        }

        public async Task<SpotifyAccount> GetUserAccountAsync(ulong userId, CancellationToken ct)
        {
            var filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, userId.ToString());
            var results = await Table.ExecuteQueryAsync(new TableQuery<SpotifyAccount>().Where(filter), ct);
            if (!results.Any())
                return null;

            return results.Single();
        }

        public async Task AddOrUpdateUserAccountAsync(SpotifyAccount account, CancellationToken ct)
        {
            await Table.CreateIfNotExistsAsync();
            account.PartitionKey = "root";
            account.RowKey = account.UserId;
            account.ETag = "*";
            await Table.ExecuteAsync(TableOperation.InsertOrReplace(account));
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

            return Table.ExecuteAsync(TableOperation.Delete(account));
        }
    }
}
