# Multi-tenancy using schemas with Entity Framework and PostgreSQL

* dotnet    `7.0.111`
* dotnet-ef     `7.0.111`
* PostgreSQL    `15.2`

| Approach               | Column for Tenant? | Schema per Tenant? | Multiple Databases? | EF Core Support     |
| ---------------------- | ------------------ | ------------------ | ------------------- | ------------------- |
| Discriminator (column) | Yes                | No                 | No                  | Global query filter |
| Database per tenant    | No                 | No                 | Yes                 | Configuration       |
| Schema per tenant      | No                 | Yes                | No                  | Not supported       |

## Head Quarter's API

### Scaffolding the base project

Let's create the base project from the `webapi` template:

```bash
dotnet new webapi -o ChocolateStores
cd ChocolateStores
dotnet new gitignore
dotnet new editorconfig
git init -b main
```

And install the basic packages needed for this project: Entity Framework and the PostgreSQL driver.

```bash
# If you don't have the tool already installed
dotnet tool install --global dotnet-ef

dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

We can already remove some unnecessary files:

```bash
rm -R Properties
rm WeatherForecast.cs Controllers/WeatherForecastController.cs
```

And this is will be our minimal `Program.cs`:

```cs
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

And let's create the stores controller `Controllers/StoresController.cs`:

```cs
using Microsoft.AspNetCore.Mvc;

namespace ChocolateStores.Controllers;

[ApiController]
[Route("[controller]")]
public class StoresController : ControllerBase
{
    private readonly ILogger<StoresController> _logger;

    public StoresController(ILogger<StoresController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<object> Get()
    {
        _logger.LogDebug("Getting store list");

        return new List<object>();
    }
}
```

Now we are ready to run the project for the first time with `dotnet run`.

> You should be able to access Swagger on `http://localhost:5000/swagger`.

### HQ Models and Context

For the global entities, we will go ahead and create two simple ones: `Models/Store.cs` and `Models/Product.cs`. Because we are the product's makers, we can control the product catalogue and details from our head quarters, each store should be able to access that.

```cs
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChocolateStores.Models;

[Table("stores")]
public class Store
{
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("city")]
    public string City { get; set; } = string.Empty;

    [Column("schema")]
    public string Schema { get; set; } = string.Empty;
}

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.HasKey(x => x.Code);

        builder.HasIndex(x => x.Schema).IsUnique();
    }
}
```

```cs
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChocolateStores.Models;

[Table("products")]
public class Product
{
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("discontinued")]
    public bool IsDiscontinued { get; set; }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Code);
    }
}
```
We can create now the `Contexts/HQContext.cs` that will handle requests to the global schema. It is important to keep this context in its own schema with its own migrations history table. To do that we will override the `OnConfiguring` method to add any configuration we may need (like the connection string).


```cs
using ChocolateStores.Models;
using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Context;

public class AppDataContext : DbContext
{
    public static readonly string Schema = "hq";
    public static readonly string Migrations = "_migrations";
    protected readonly string _connection;

    public AppDataContext(IConfiguration configuration, DbContextOptions<AppDataContext> options)
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
            typeof(AppDataContext).Assembly,
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
```

Now we need to update the `appsettings.json` to include our connection to the database (local or remote).

```json
"ConnectionStrings": {
    "DefaultConnection": "Host=;Database=chocolate_stores;Username=;Password="
}
```

And also need to add the `HQContext` to the `Program.cs`.

```cs
using ChocolateStores.Context;
...
builder.Services.AddDbContext<HQContext>();
...
```

And voilà, we are set to perform our first migration. Because we will have multiple contexts, we will need to specify each of them in the migration command, we will also separate them in two directories in order to keep things organized.

```bash
dotnet ef migrations add InitialHQ_StoresProducts --context HQContext --output-dir Migrations/HQ
```

To perform those migrations automatically on every startup, we can extend the `Program.cs` to include those operations right after we build the `WebApplication app`:

```cs
...
using Microsoft.EntityFrameworkCore;
...
WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    HQContext hqContext = scope.ServiceProvider.GetRequiredService<HQContext>();

    hqContext.Database.Migrate();
}
...
```

### HQ Controllers and sample data

For the HQ's controllers let's keep it simple, just a `GET` and a `POST` for this moment. There's no need for a full CRUD api in this proof of concept. We are also not going to implement any services/repositories for the sake of brevity, therefore we are going to inject the `HQContext` directly in the `StoresController`

```cs
using ChocolateStores.Context;
using ChocolateStores.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Controllers;

[ApiController]
[Route("[controller]")]
public class StoresController : ControllerBase
{
    private readonly ILogger<StoresController> _logger;
    private readonly HQContext _hQContext;

    public StoresController(ILogger<StoresController> logger, HQContext hQContext)
    {
        _logger = logger;
        _hQContext = hQContext;
    }

    [HttpGet]
    public async Task<IEnumerable<Store>> Get()
    {
        _logger.LogDebug("Getting store list");

        return await _hQContext.Stores.OrderBy(x => x.Schema).ToListAsync();
    }

    [HttpPost]
    public async Task<Store> Post(Store store)
    {
        _logger.LogDebug("Adding new store");

        _hQContext.Stores.Add(store);

        await _hQContext.SaveChangesAsync();

        return store;
    }
}
```

And let's create a `Controllers/ProductsController.cs` in the same fashion:

```cs
using ChocolateStores.Context;
using ChocolateStores.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ILogger<ProductsController> _logger;
    private readonly HQContext _hQContext;

    public ProductsController(ILogger<ProductsController> logger, HQContext hQContext)
    {
        _logger = logger;
        _hQContext = hQContext;
    }

    [HttpGet]
    public async Task<IEnumerable<Product>> Get()
    {
        _logger.LogDebug("Getting store list");

        return await _hQContext.Products.OrderBy(x => x.Name).ToListAsync();
    }

    [HttpPost]
    public async Task<Product> Post(Product product)
    {
        _logger.LogDebug("Adding new store");

        _hQContext.Products.Add(product);

        await _hQContext.SaveChangesAsync();

        return product;
    }
}
```

We can now add some data via swagger. So let's populate our table with these records:

| code | name              | city      | schema |
| ---- | ----------------- | --------  | ------ |
| ST01 | Chocolate Store   | Brugge    | st01   |
| ST02 | Chocolate Express | Bruxelles | st02   |

| code | name               | type     |
| ---- | ------------------ | -------- |
| B01  | Noir avec Noisette | Tablette |
| B02  | Noir avec Pistache | Tablette |
| PR01 | Manon Noir         | Pralines |
| PR02 | Manon au Lait      | Pralines |

> \* The `isDiscontinued` prop can be omitted. It will default to `false`.

🎉 We are all set for the HQ tables and data. Let's move on to the store per store api.

## In Store API

### Creating Migrations

For this part let's create a simple entity/table just with the store's catalogue: the products it sells and the current stock. And to keep it organised, it will be in a different directory/namespace: `Models/InStore/Catalogue.cs`.

```cs
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChocolateStores.Models.InStore;

[Table("catalogue")]
public class Catalogue
{
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("stock")]
    public int Stock { get; set; }

    [Column("last_order")]
    public DateTime LastOrder { get; set; } = DateTime.UtcNow;
}

public class CatalogueConfiguration : IEntityTypeConfiguration<Catalogue>
{
    public void Configure(EntityTypeBuilder<Catalogue> builder)
    {
        builder.HasKey(x => x.Code);
    }
}
```

Before creating the in store context, we will need a way to detect which contexts use multiple schemas later on, for that we can create the interface `Contexts/IInStoreContext.cs`:

```cs
namespace ChocolateStores.Context;

public interface IInStoreContext
{
    public string Schema { get; }
}
```

Now we go ahead and create our `Contexts/InStoreContext.cs` that implements the `IInStoreContext` interface. It is based on the HQ's one, but with a few modifications. We will set the default schema name with the PostgreSQL's `public`, however we are not going to perform any migrations on it. We will also need an extra constructor that can accept the schema name.

```cs
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
```

We can now register this context in the `Program.cs` by adding `builder.Services.AddDbContext<InStoreContext>();` (just below the `HQContext` line) and create our fist migration:

```bash
dotnet ef migrations add InitialInStore_Catalogue --context InStoreContext --output-dir Migrations/InStore
```

If we have a look at the Migrations directory, we will see that we have successfully created it. Easy, right? *«So we can go ahead and apply those migrations?»* Not quite... now it is when the real trickery starts.

In the migration file, we will need a constructor that accepts the current `DbContext` being used to performing the migration and store it in a private field. After that, we can remove all mentions of the `public` schema and replace it with `_context.Schema`, thus turning this migration class a bit more dynamic. It should look something like this:

```cs
using ChocolateStores.Context;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChocolateStores.Migrations.InStore
{
    public partial class InitialInStore_Catalogue : Migration
    {
        private readonly IInStoreContext _context;

        public InitialInStore_Catalogue(IInStoreContext context)
        {
            _context = context;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: _context.Schema);

            migrationBuilder.CreateTable(
                name: "catalogue",
                schema: _context.Schema,
                columns: table =>
                    new
                    {
                        code = table.Column<string>(type: "text", nullable: false),
                        stock = table.Column<int>(type: "integer", nullable: false),
                        last_order = table.Column<DateTime>(
                            type: "timestamp with time zone",
                            nullable: false
                        )
                    },
                constraints: table => table.PrimaryKey("PK_catalogue", x => x.code)
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "catalogue", schema: _context.Schema);
        }
    }
}
```

### Performing Migrations

*So, now our migration class is dynamic, I can apply them, right?* Not really... there are some other things we need to take care before.EF expects migrations classes without constructors. So it will spit some errors if we try to apply the migrations with the `IInStoreContext` being expected during the class instantiation. The solution is pretty simple then... let's hack it!

The first step is to extend the `MigrationsAssembly` class, so go ahead and create the `Contexts/InStoreAssembly.cs` file.

> **<!> Warning**: This is not sanctioned by Microsoft, you will see the follow warning when trying to extend from that class:
> 
> *This is an internal API that supports the Entity Framework Core infrastructure and not subject to the same compatibility standards as public APIs. It may be changed or removed without notice in any release. You should only use it directly in your code with extreme caution and knowing that doing so can result in application failures when updating to a new Entity Framework Core release.*
> 
> Therefore, the solution presented here works fine for **this** exact EF's version, it may not work in the future.

In the constructor we will pass all the params down to the base class, but store the current context in a private field. The secret sauce is done by overriding the `CreateMigration` method: First we check if we have a known `activeProvider` (i.e. a specific database flavour for EF to construct SQL statements), otherwise we throw an exception. Then let's check if the migration class contains a constructor with the `IInStoreContext` interface as argument. If it does, and our `_context` also implements the `IInStoreContext` interface, we go ahead and try to instantiate the `Migration` class with it, otherwise we fallback to the `base.CreateMigration` method.

```cs
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

        bool isInStoreMigration = migrationClass.GetConstructor(new[] { typeof(IInStoreContext) }) != null;

        if (isInStoreMigration && _context is IInStoreContext storeContext)
        {
            Migration? migration = (Migration?)Activator.CreateInstance(migrationClass.AsType(), storeContext);

            if (migration != null)
            {
                migration.ActiveProvider = activeProvider;

                return migration;
            }
        }

        return base.CreateMigration(migrationClass, activeProvider);
    }
}
```

> \* You can check the original `CreateMigration` implementation at: 
> [https://github.com/dotnet/efcore/blob/release/7.0/src/EFCore.Relational/Migrations/Internal/MigrationsAssembly.cs](https://github.com/dotnet/efcore/blob/release/7.0/src/EFCore.Relational/Migrations/Internal/MigrationsAssembly.cs)

The second step is to create a couple more classes: one that replaces the `ModelCacheKey` class and one that implements `IModelCacheKeyFactory` using our implementation from the previous one. This is needed because the `ModelCacheKey` is used by EF to track if a migration has or not been performed, therefore we need to add the schema in consideration, otherwise it will only perform migrations for only one schema.

We start by creating a `Contexts/InStoreCacheKey.cs` class and storing privately the possible schema name, context type and if is run in design mode or not. Those fields will be used to compare objects and generate the object's hash.

```cs
using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Context;

internal class InStoreCacheKey
{
    private readonly Type _dbContextType;
    private readonly bool _designTime;
    private readonly string? _schema;

    public InStoreCacheKey(DbContext context, bool designTime)
    {
        _dbContextType = context.GetType();
        _designTime = designTime;
        _schema = (context as IInStoreContext)?.Schema;
    }

    protected bool Equals(InStoreCacheKey other) =>
        _dbContextType == other._dbContextType
        && _designTime == other._designTime
        && _schema == other._schema;

    public override bool Equals(object? obj) =>
        (obj is InStoreCacheKey otherAsKey) && Equals(otherAsKey);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_dbContextType);
        hash.Add(_designTime);
        hash.Add(_schema);

        return hash.ToHashCode();
    }
}
```

And now we create a factory (`Contexts/InStoreCacheKeyFactory.cs`) that implements our `InStoreCacheKey`:

```cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ChocolateStores.Context;

internal class InStoreCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        return new InStoreCacheKey(context, designTime);
    }
}
```

The final step is to configure those classes for dependency injection in the `InStoreContext`'s `OnConfiguring` method, and voilà! Now we are ready for performing migrations.

```cs
...
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
...
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .ReplaceService<IMigrationsAssembly, InStoreAssembly>()
            .ReplaceService<IModelCacheKeyFactory, InStoreCacheKeyFactory>();
...
```

To perform those migrations automatically together with the `HQContext` ones, we will need to alter a bit the `Program.cs`:

```cs
...
using (IServiceScope scope = app.Services.CreateScope())
{
    HQContext hqContext = scope.ServiceProvider.GetRequiredService<HQContext>();
    IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    hqContext.Database.Migrate();

    foreach (string schema in hqContext.Stores.AsNoTracking().Select(x => x.Schema).ToList())
    {
        using InStoreContext worldContext = new(configuration, schema);

        worldContext.Database.Migrate();
    }
}
...
```

And let's go ahead and add an endpoint to the `StoresController.cs` to trigger those migrations, so we don't need to restart the app every time a store is created.

```cs
...
    private readonly ILogger<StoresController> _logger;
    private readonly IConfiguration _configuration;
    private readonly HQContext _hQContext;

    public StoresController(
        ILogger<StoresController> logger,
        IConfiguration configuration,
        HQContext hQContext
    )
    {
        _logger = logger;
        _configuration = configuration;
        _hQContext = hQContext;
    }
...
    [HttpGet("migrate")]
    public async Task PostMigrate()
    {
        _logger.LogDebug("Performing migrations to all stores");

        foreach (string schema in _hQContext.Stores.AsNoTracking().Select(x => x.Schema).ToList())
        {
            using InStoreContext worldContext = new(_configuration, schema);

            await worldContext.Database.MigrateAsync();
        }
    }
...
```

> \* To avoid code duplication we could extract this logic to an extension.

### Dynamically querying a store

Now we have the database in place, it's time to set up a way to define what store is being querying at runtime and inject the `InStoreContext` accordingly. For that, we will be passing the `Store.Code` via the HTTP headers.

To not pollute the `Program.cs`, and have better control of injection order, we are going to create an extension (`Infrastructure/StoreExtensions.cs`) to handle the registering of both database contexts. First, we will need to inject `IHttpContextAccessor`, so the `HttpContext` can be available to us. Then, we add the `HQContext` as before, and then we will add the `InStoreContext` as a scoped service. Inside the service registration, we check if the program is in designTime, if yes we return a default `InStoreContext`, so the schema does not impact the migrations. Otherwise, we access the HTTP request to get the store code and query the store schema from it, in case of not found we fallback to the default schema name.

```cs
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
            IConfiguration config = ctx.GetService<IConfiguration>() ?? throw new Exception("Could not find configurations");

            if (EF.IsDesignTime)
            {
                return new InStoreContext(config, new DbContextOptions<InStoreContext>());
            }

            IHttpContextAccessor httpContext = ctx.GetService<IHttpContextAccessor>() ?? throw new Exception("HTTP context not accessible");

            HQContext hqContext = ctx.GetService<HQContext>() ?? throw new Exception("HQ database not set");

            string schema = httpContext.HttpContext?.GetSchemaFromHeader(hqContext) ?? InStoreContext.DefaultSchema;

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
}
```

Then, to be sure that our `InStoreContext` is only used with valid schemas, we add this exception to its constructor:

```cs
...
    public InStoreContext(IConfiguration configuration, string schema)
    {
        if (schema == DefaultSchema)
        {
            throw new Exception("Failed to consult data for this store.");
        }

        Schema = schema;

        _connection = HQContext.GetConnection(configuration);
    }
...
```

To help us pass the store code via the HTTP headers when using the Swagger page, we can create a decorator (`Infrastructure/StoreHeader.cs`) to use in the required controller's routes.

```cs
using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ChocolateStores.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public class UseStoreHeader : Attribute { }

public class StoreHeader : IOperationFilter
{
    public static readonly string HeaderName = "store-code";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        if (context.MethodInfo.GetCustomAttribute(typeof(UseStoreHeader)) is UseStoreHeader _)
        {
            operation.Parameters.Add(
                new OpenApiParameter()
                {
                    Name = HeaderName,
                    In = ParameterLocation.Header,
                    Required = true
                }
            );
        }
    }
}
```

To use our extension and decorators, we simply alter the `Program.cs` by removing both `AddDbContext()` lines and inserting those below:

```cs
...
using ChocolateStores.Infrastructure;
...
builder.Services.AddSwaggerGen(c => c.OperationFilter<StoreHeader>());
builder.Services.AddDataContexts();
...
```

And now, we can create a store controller (`Controllers/InStoreController.cs`) with a simple `GET` and `POST` that will execute operations for a specific store.

```cs
using ChocolateStores.Context;
using ChocolateStores.Models.InStore;
using ChocolateStores.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChocolateStores.Models;

namespace ChocolateStores.Controllers;

[ApiController]
[Route("[controller]")]
public class InStoreController : ControllerBase
{
    private readonly ILogger<InStoreController> _logger;
    private readonly InStoreContext _storeContext;

    public InStoreController(ILogger<InStoreController> logger, InStoreContext storeContext)
    {
        _logger = logger;
        _storeContext = storeContext;
    }

    [HttpGet("Stock"), UseStoreHeader]
    public async Task<IEnumerable<Catalogue>> GetStock()
    {
        _logger.LogDebug("Getting stock for store: {store}", _storeContext.Schema);

        return await _storeContext.Catalogue.OrderByDescending(x => x.LastOrder).ToListAsync();
    }

    [HttpPost("Stock"), UseStoreHeader]
    public async Task<Catalogue> PostStock(Catalogue stock)
    {
        _logger.LogDebug("Updating stock for store: {store}", _storeContext.Schema);

        Catalogue? catalogue = await _storeContext.Catalogue.FirstOrDefaultAsync(
            x => x.Code == stock.Code
        );

        if (catalogue == null)
        {
            _storeContext.Catalogue.Add(stock);
        }
        else
        {
            catalogue.Stock = stock.Stock;
            catalogue.LastOrder = stock.LastOrder;
        }

        await _storeContext.SaveChangesAsync();

        return stock;
    }
}
```

All set! Now we can pass the store id to those routes and operations will be restricted to the corresponding store. So let's add some data for the stores we had registered.

* **Store 01**

| code | stock |
| ---- | ----- |
| B01  | 100   |
| B02  | 100   |
| PR02 | 100   |
| PR01 | 100   |

* **Store 02**

| code | stock |
| ---- | ----- |
| B02  | 100   |
| PR02 | 100   |