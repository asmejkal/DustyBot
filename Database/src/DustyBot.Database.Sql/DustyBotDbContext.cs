using Microsoft.EntityFrameworkCore;

namespace DustyBot.Database.Sql
{
    public class DustyBotDbContext : DbContext
    {
        public DustyBotDbContext(DbContextOptions<DustyBotDbContext> options)
            : base(options)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public static DustyBotDbContext Create(string connectionString)
        {
            var options = new DbContextOptionsBuilder<DustyBotDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new DustyBotDbContext(options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("DustyBot");
        }
    }
}
