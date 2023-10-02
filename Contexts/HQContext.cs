using ChocolateStores.Models;
using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Context;

public class HQContext : DbContext
{
    public static readonly string Schema = "hq";
    public static readonly string Migrations = "_migrations";
    protected readonly string _connection;

    public HQContext(IConfiguration configuration, DbContextOptions<HQContext> options)
        : base(options)
    {
        _connection = GetConnection(configuration);
    }

    public DbSet<Store> Stores { get; set; }
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HQContext).Assembly,
            t => t.Namespace == typeof(Store).Namespace
        );
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connection, x => x.MigrationsHistoryTable(Migrations, Schema));
    }

    public static string GetConnection(IConfiguration configuration)
    {
        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new Exception("Connection string not found");
    }
}
