using ChocolateStores.Models.InStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChocolateStores.Context;

public class InStoreContext : DbContext, IInStoreContext
{
    public static readonly string DefaultSchema = "public";
    protected readonly string _connection;
    public string Schema { get; }

    public InStoreContext(IConfiguration configuration, DbContextOptions<InStoreContext> options)
        : base(options)
    {
        Schema = DefaultSchema;

        _connection = HQContext.GetConnection(configuration);
    }

    public InStoreContext(IConfiguration configuration, string schema)
    {
        if (schema == DefaultSchema)
        {
            throw new Exception("Failed to consult data for this store.");
        }

        Schema = schema;

        _connection = HQContext.GetConnection(configuration);
    }

    public DbSet<Catalogue> Catalogue { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InStoreContext).Assembly,
            t => t.Namespace == typeof(Catalogue).Namespace
        );
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .ReplaceService<IMigrationsAssembly, InStoreAssembly>()
            .ReplaceService<IModelCacheKeyFactory, InStoreCacheKeyFactory>();

        optionsBuilder.UseNpgsql(
            _connection,
            x => x.MigrationsHistoryTable(HQContext.Migrations, Schema)
        );
    }
}
