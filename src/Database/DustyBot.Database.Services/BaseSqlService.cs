using System;
using DustyBot.Database.Sql;

namespace DustyBot.Database.Services
{
    public abstract class BaseSqlService : IDisposable
    {
        protected DustyBotDbContext DbContext { get; }
        
        private bool _disposedValue = false; // To detect redundant calls

        protected BaseSqlService(DustyBotDbContext dbContext)
        {
            DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DbContext.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}
