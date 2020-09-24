using DustyBot.Database.Entities;
using System;

namespace DustyBot.Database.Services
{
    public abstract class BaseSqlService : IDisposable
    {
        protected DustyBotDbContext DbContext { get; }

        protected BaseSqlService(DustyBotDbContext dbContext)
        {
            DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

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

        public void Dispose() => Dispose(true);
        #endregion
    }
}
