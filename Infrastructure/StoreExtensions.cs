using ChocolateStores.Context;
using ChocolateStores.Models;
using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Infrastructure;

public static class StoreExtensions
{
    public static IServiceCollection AddDataContexts(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.AddDbContext<HQContext>();

        serviceCollection.AddScoped(ctx =>
        {
            IConfiguration config =
                ctx.GetService<IConfiguration>()
                ?? throw new Exception("Could not find configurations");

            if (EF.IsDesignTime)
            {
                return new InStoreContext(config, new DbContextOptions<InStoreContext>());
            }

            IHttpContextAccessor httpContext =
                ctx.GetService<IHttpContextAccessor>()
                ?? throw new Exception("HTTP context not accessible");

            HQContext hqContext =
                ctx.GetService<HQContext>() ?? throw new Exception("HQ database not set");

            string schema =
                httpContext.HttpContext?.GetSchemaFromHeader(hqContext)
                ?? InStoreContext.DefaultSchema;

            return new InStoreContext(config, schema);
        });

        return serviceCollection;
    }

    public static string? GetSchemaFromHeader(this HttpContext http, HQContext hqContext)
    {
        string? code =
            http.Request.Headers[StoreHeader.HeaderName].FirstOrDefault() ?? string.Empty;

        Store? store = hqContext.Stores
            .AsNoTracking()
            .FirstOrDefault(x => EF.Functions.ILike(x.Code, code));

        return store?.Schema;
    }

    public static WebApplication ApplyDbMigrations(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();

        HQContext hqContext = scope.ServiceProvider.GetRequiredService<HQContext>();

        hqContext.Database.Migrate();
        hqContext.ApplyDbMigrations(scope.ServiceProvider.GetRequiredService<IConfiguration>());

        return app;
    }

    public static void ApplyDbMigrations(this HQContext hqContext, IConfiguration configuration)
    {
        foreach (string schema in hqContext.Stores.AsNoTracking().Select(x => x.Schema).ToList())
        {
            using InStoreContext worldContext = new(configuration, schema);

            worldContext.Database.Migrate();
        }
    }

    public static Task ApplyDbMigrationsAsync(
        this HQContext hqContext,
        IConfiguration configuration
    ) => Task.Run(() => hqContext.ApplyDbMigrations(configuration));
}
