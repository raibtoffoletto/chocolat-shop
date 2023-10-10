# Multi-tenancy using schemas with Entity Framework and PostgreSQL

## Introduction

Multi-tenancy is when a single software handle multiple customers and it can be achieved through different architectures. One of them is the use of a single database and using table's `schemas` to separate data from one account to another. 

One popular choice for constructing web apis is to use Microsoft's «.NET» with it's «Entity Framework» ORM. However, the official documentation for achieving multi-tenancy with EF do not support use of schemas:

| Approach               | Column for Tenant? | Schema per Tenant? | Multiple Databases? | EF Core Support     |
| ---------------------- | ------------------ | ------------------ | ------------------- | ------------------- |
| Discriminator (column) | Yes                | No                 | No                  | Global query filter |
| Database per tenant    | No                 | No                 | Yes                 | Configuration       |
| Schema per tenant      | No                 | Yes                | No                  | Not supported       |

But fear not. There is a way! In this article we will explore how to achieve it without [*too much*] effort.

### Proposed scenario

Let us imagine that we are a belgium chocolate maker. With have our Head Quarters where all production is made, and we have a few stores in different cities. The HQ control the records for how many stores we own and the records for all products manufactured. 

Each store than will handle the record of current inventory, sales, employees, etc... for bureaucratic reasons those records should be completely isolated from each other.
So we will use different schemas for each store, and to keep software development simple, our api will have the same set of endpoints to handle multiple store requests.

### Tools used
* dotnet - `v7.0.111`
* dotnet-ef - `v7.0.111`
* PostgreSQL - `v15.2`
* Visual Studio Code
* Ubuntu 22.04 LTS

I won't cover the installation of any of those tools, please, follow the official instructions for your platform.

> All commands are run from a unix-like command line, it may need adapting to your specific platform.

## Head Quarter's API

### Scaffolding the base project

We start by creating the base project from the `webapi` template:

```bash
dotnet new webapi -o ChocolateStores
cd ChocolateStores
dotnet new gitignore
dotnet new editorconfig
git init -b main
```

And by installing the extra packages needed: Entity Framework and the PostgreSQL driver.

```bash
# If you do not have the tool already installed
dotnet tool install --global dotnet-ef

dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

We can already remove some unnecessary files:

```bash
rm WeatherForecast.cs Controllers/WeatherForecastController.cs

# If you are running the project from the terminal, this can also be removed.
rm -R Properties
```

And replace the `Program.cs` with this minimal set up:

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

However, we need at least one controller for the project to run.
So we can create the stores controller `Controllers/StoresController.cs` with a placeholder method for the moment:

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

> You should be able to access Swagger at this address: [http://localhost:5000/swagger](http://localhost:5000/swagger).

### HQ Models and Context

For the global entities, we will go ahead and create two simple ones: `Models/Store.cs` and `Models/Product.cs`. Because we are the product's makers, we can control the product catalogue and details from our head quarters, which each store will be able to access later on.

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

We can create now the `Contexts/HQContext.cs`, it will handle the requests to the global schema.

It is important to keep this context in its own schema with its own migrations history table. In order to do that, we will override the `OnConfiguring` method and add any configuration we may need inside it.


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

Now we need to update the `appsettings.json` to include our connection to the database (local or remote):

```json
"ConnectionStrings": {
    "DefaultConnection": "Host=;Database=chocolate_stores;Username=;Password="
}
```

And we also need to add the `HQContext` to the `Program.cs`.

```cs
using ChocolateStores.Context;
...
builder.Services.AddDbContext<HQContext>();
...
```

Voilà, we are set to perform our first migration.

Because we will have multiple contexts, we will need to specify each of them in the migration command. We will also separate them in two directories in order to keep things organized.

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

For the HQ's controllers let us keep it simple, just a `GET` and a `POST` for this moment. There's no need for a full CRUD api in this proof of concept. We are also not going to implement any services/repositories for the sake of brevity, therefore we are going to inject the `HQContext` directly in the `StoresController`

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
        _logger.LogDebug("Adding new store {name}", store.Name);

        _hQContext.Stores.Add(store);

        await _hQContext.SaveChangesAsync();

        return store;
    }
}
```

And we will create a `Controllers/ProductsController.cs` in the same fashion:

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
        _logger.LogDebug("Getting product list");

        return await _hQContext.Products.OrderBy(x => x.Name).ToListAsync();
    }

    [HttpPost]
    public async Task<Product> Post(Product product)
    {
        _logger.LogDebug("Adding new product {name}", product.Name);

        _hQContext.Products.Add(product);

        await _hQContext.SaveChangesAsync();

        return product;
    }
}
```

We can now add some data via swagger. So let us populate our table with these records:

#### Stores

| code | name              | city      | schema |
| ---- | ----------------- | --------  | ------ |
| ST01 | Chocolate Store   | Brugge    | st01   |
| ST02 | Chocolate Express | Bruxelles | st02   |

#### Products

| code | name               | type     |
| ---- | ------------------ | -------- |
| B01  | Noir avec Noisette | Tablette |
| B02  | Noir avec Pistache | Tablette |
| PR01 | Manon Noir         | Pralines |
| PR02 | Manon au Lait      | Pralines |

> \* The `isDiscontinued` property can be omitted from the json. It will default to `false`.

🎉 We are all set with the HQ tables and data. Let us move on to the in store api.

## In Store API

### Creating Migrations

For this part let us create a simple entity/table just with the store's inventory: the products it sells and the current stock. And to keep it organised, it will be in a different directory/namespace: `Models/InStore/Inventory.cs`.

```cs
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChocolateStores.Models.InStore;

[Table("inventory")]
public class Inventory
{
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("stock")]
    public int Stock { get; set; }

    [Column("last_order")]
    public DateTime LastOrder { get; set; } = DateTime.UtcNow;
}

public class CatalogueConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.HasKey(x => x.Code);
    }
}
```

Before creating the in store context, we will need later on a way to detect which contexts use dynamic schemas. So we will create the interface `Contexts/IInStoreContext.cs`:

```cs
namespace ChocolateStores.Context;

public interface IInStoreContext
{
    public string Schema { get; }
}
```

Now we can go ahead and create our `Contexts/InStoreContext.cs` that implements the `IInStoreContext` interface.

It is based on the HQ's one, but with a few modifications. We will set the default schema name with the PostgreSQL's `public`, however we are not going to perform any migrations on it. We will also need an extra constructor that can accept the schema name.

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

    public DbSet<Inventory> Inventory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InStoreContext).Assembly,
            t => t.Namespace == typeof(Inventory).Namespace
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

If we have a look at the Migrations directory, we will see that we have successfully created the file. Easy, right? *«So we can go ahead and apply those migrations?»* Not quite... now it is when the real trickery starts.

In the migration file, we will need a constructor that accepts the current `DbContext` being used to performing the migration and store it in a private field. After that, we can remove all mentions of the `public` schema and replace it with `_context.Schema`, thus turning this migration class a bit more dynamic. It should look something like this:

```cs
using ChocolateStores.Context;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChocolateStores.Migrations.InStore
{
    public partial class InitialInStore_Inventory : Migration
    {
        private readonly IInStoreContext _context;

        public InitialInStore_Inventory(IInStoreContext context)
        {
            _context = context;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: _context.Schema);

            migrationBuilder.CreateTable(
                name: "inventory",
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
            migrationBuilder.DropTable(name: "inventory", schema: _context.Schema);
        }
    }
}
```

### Performing Migrations

*«So, now our migration class is dynamic, I can apply them, right?»* Not really... there are some other things we need to take care before. EF expects migrations classes without constructors. So it will spit some errors if we try to apply the migrations with the `IInStoreContext` parameter being expected during the class instantiation. The solution is pretty simple then... we will hack it!

The first step is to extend the `MigrationsAssembly` class, so go ahead and create the `Contexts/InStoreAssembly.cs` file.

> **<!> Warning**: This is not sanctioned by Microsoft, you will see the follow warning when trying to extend from that class:
> 
> *This is an internal API that supports the Entity Framework Core infrastructure and not subject to the same compatibility standards as public APIs. It may be changed or removed without notice in any release. You should only use it directly in your code with extreme caution and knowing that doing so can result in application failures when updating to a new Entity Framework Core release.*
> 
> Therefore, the solution presented here works fine for **this** exact EF's version, it may not work in the future.

In its constructor we will pass all the params down to the base class, but store the current context in a private field. The secret sauce is done by overriding the `CreateMigration` method: First we check if we have a known `activeProvider` (i.e. a specific database flavour for EF to construct SQL statements), otherwise we throw an exception. Then let us check if the migration class contains a constructor with the `IInStoreContext` interface as argument. If it does, and our `_context` also implements the `IInStoreContext` interface, we go ahead and try to instantiate the `Migration` class with it, otherwise we fallback to the `base.CreateMigration` method.

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

And then we create a factory (`Contexts/InStoreCacheKeyFactory.cs`) that implements it:

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

And let us go ahead and add an endpoint to the `StoresController.cs` to trigger those migrations, so we do not need to restart the app every time a store is created.

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
            _logger.LogDebug("Migrating schema {name}", schema);
            
            using InStoreContext worldContext = new(_configuration, schema);

            await worldContext.Database.MigrateAsync();
        }
    }
...
```

### Dynamically querying a store

Now we have the database in place, it's time to set up a way to define what store is being querying at runtime and inject the `InStoreContext` accordingly. For that, we will be passing the `Store.Code` via the HTTP headers.

To not pollute the `Program.cs`, and have better control of injection order, we are going to create an extension (`Infrastructure/StoreExtensions.cs`) to handle the registering of both database contexts. First, we will need to inject `IHttpContextAccessor`, so the `HttpContext` can be available to us. Then, we add the `HQContext` as before, and only then we will add the `InStoreContext` as a scoped service. Inside the service registration, we check if the program is in designTime, if yes we return a default `InStoreContext`, so the schema does not impact the migrations. Otherwise, we access the HTTP request to get the store code and query the store schema from it, in case of not found we fallback to the default schema name.

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

    [HttpGet("Inventory"), UseStoreHeader]
    public async Task<IEnumerable<Inventory>> GetInventory()
    {
        _logger.LogDebug("Getting inventory for store: {store}", _storeContext.Schema);

        return await _storeContext.Inventory.OrderByDescending(x => x.LastOrder).ToListAsync();
    }

    [HttpPost("Inventory"), UseStoreHeader]
    public async Task<Inventory> PostInventory(Inventory stock)
    {
        _logger.LogDebug("Updating inventory for store: {store}", _storeContext.Schema);

        Inventory? inventory = await _storeContext.Inventory.FirstOrDefaultAsync(
            x => x.Code == stock.Code
        );

        if (inventory == null)
        {
            _storeContext.Inventory.Add(stock);
        }
        else
        {
            inventory.Stock = stock.Stock;
            inventory.LastOrder = stock.LastOrder;
        }

        await _storeContext.SaveChangesAsync();

        return stock;
    }
}
```

All set! Now we can pass the store id to those routes and operations will be restricted to the corresponding store. So let us add some data for the stores we had registered using the `POST` method. Then when triggering the GET method, the correct list should be return.

#### **Store 01**

| code | stock |
| ---- | ----- |
| B01  | 100   |
| B02  | 100   |
| PR02 | 100   |
| PR01 | 100   |

#### **Store 02**

| code | stock |
| ---- | ----- |
| B02  | 100   |
| PR02 | 100   |

### Join query between schemas.

One of the pitfalls of this architecture is that EF will not perform joining queries from sets that are not in the same context. For example, imagine that we want to return the inventory list with the product's name and type and the in store current stock... one solution is to query the store products first, keep them in memory, then query the HQ's inventory and join both lists using LINQ. This can be very slow (and memory consuming) depending on the size of the dataset in question. Other way would be to write manually a query and then cast its results to the corresponding DTO.

However, there is also another solution: to add a `Products` set to the `InStoreContext`. To do that, the configuration is a bit different than the other entities. In the `OnModelCreating` method, we need to configure the `Product` entity as it is done in the `ProductConfiguration` class. We also need to set the correct schema to the entity's metadata, as well set it to be excluded from the migrations in that context.

```cs
using ChocolateStores.Models;
...
    public DbSet<Product> Products { get; set; }
...
        modelBuilder.Entity<Product>(x =>
        {
            x.HasKey(x => x.Code);
            x.Metadata.SetSchema(HQContext.Schema);
            x.Metadata.SetIsTableExcludedFromMigrations(true);
        });
```

Now the `Products` table will accessible from the context, we will create a `Models/InStore/CatalogueDTO.cs` to join store and hq product's information:

```cs
namespace ChocolateStores.Models.InStore;

public class CatalogueDTO
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Stock { get; set; }
}
```

In the `InStoreController` we can extend the `POST` method to validate the product's code sent, and also create the `GET` method to return the `CatalogueDTO`:

```cs
...
    public async Task<Inventory> PostStock(Inventory stock)
    {
        _logger.LogDebug("Updating stock for store: {store}", _storeContext.Schema);

        Product product =
            await _storeContext.Products.FirstOrDefaultAsync(
                x => EF.Functions.ILike(x.Code, stock.Code)
            ) ?? throw new Exception("Product code does not exist");

        if (product.IsDiscontinued)
        {
            throw new Exception("Product is discontinued, cannot order any more");
        }

        Inventory? inventory = await _storeContext.Inventory.FirstOrDefaultAsync(
            x => x.Code == product.Code
        );
...
    }

    [HttpGet("Catalogue"), UseStoreHeader]
    public async Task<IEnumerable<CatalogueDTO>> GetCatalogue()
    {
        _logger.LogDebug("Getting catalogue for store: {store}", _storeContext.Schema);

        return await _storeContext.Inventory
            .Join(
                _storeContext.Products,
                c => c.Code,
                p => p.Code,
                (c, p) =>
                    new CatalogueDTO
                    {
                        Code = c.Code,
                        Name = p.Name,
                        Type = p.Type,
                        Stock = c.Stock
                    }
            )
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
```

That is it! With this method you can still have access to the HQ's entity, the drawback is that we need to manually configure the `Product` model, the configuration class will not help here.

> **Bonus Tasks:**
> - Extract the migration logic to an extension to avoid code duplication
> - Implement `Upsert` logic in the store and product controllers
> - Extend the Product and Inventory entities to include a price property,
> the DTO could contain both informations.
> - Try it out with MSSQL.
> - Create a front-end consumer for the API.

## Conclusion

Although not officially supported by EF, multi-tenancy using schemas can be achieved through some tinkering. There are some drawbacks and some advantages when using this solution, so evaluate well if it is worth implementing it. In any case, it is a nice case study to understand better EF's inner works.

> You can check the full project on [github](https://github.com/raibtoffoletto/chocolat-shop).