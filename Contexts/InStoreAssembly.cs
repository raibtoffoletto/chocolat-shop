using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using System.Reflection;

namespace ChocolateStores.Context;

#pragma warning disable EF1001
public class InStoreAssembly : MigrationsAssembly, IMigrationsAssembly
{
    private readonly DbContext _context;

    public InStoreAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger
    )
        : base(currentContext, options, idGenerator, logger)
    {
        _context = currentContext.Context;
    }

    public override Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        if (activeProvider == null || activeProvider == string.Empty)
        {
            throw new ArgumentNullException(nameof(activeProvider));
        }

        bool isInStoreMigration =
            migrationClass.GetConstructor(new[] { typeof(IInStoreContext) }) != null;

        if (isInStoreMigration && _context is IInStoreContext storeContext)
        {
            Migration? migration = (Migration?)
                Activator.CreateInstance(migrationClass.AsType(), storeContext);

            if (migration != null)
            {
                migration.ActiveProvider = activeProvider;

                return migration;
            }
        }

        return base.CreateMigration(migrationClass, activeProvider);
    }
}
