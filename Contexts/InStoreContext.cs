using ChocolateStores.Models.InStore;
using Microsoft.EntityFrameworkCore;

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
        optionsBuilder.UseNpgsql(
            _connection,
            x => x.MigrationsHistoryTable(HQContext.Migrations, Schema)
        );
    }
}
