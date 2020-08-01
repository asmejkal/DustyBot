using Microsoft.EntityFrameworkCore;
using StoredProcedureEFCore;

namespace Safetica.Database.Core.StoredProcedures
{
    public static class StoredProceduresExtensions
    {
        public static StoredProcedureBuilder CreateStoredProcedure(this DbContext dbContext, string procedureName)
        {
            return new StoredProcedureBuilder(dbContext.LoadStoredProc(procedureName));
        }
    }
}
