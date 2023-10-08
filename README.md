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

### Scafolding the base project

Let's create the base project from the `webapi` template:

```bash
dotnet new webapi -o ChocolateStores
cd ChocolateStores
dotnet new gitignore
dotnet new editorconfig
git init -b main
```

We can already remove some unnecessary files:

```bash
rm -R Properties
rm WeatherForecast.cs Controllers/WeatherForecastController.cs
```

This is will be our minimal `Program.cs`:

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

And our first controller `Controllers/StoresController.cs`:

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

Before anything, we will need to install the Entity Framework tool/package and the PostgreSQL driver.

```bash
# If you don't have the tool already installed
dotnet tool install --global dotnet-ef

dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

For the global entities, let's go ahead and create two simple ones: `Store` and `Product`. Because we are the product makers we can control the product catalogue and details from our head quarters, each store will be able to access that.

```bash
mkdir Models
touch Models/Store.cs
touch Models/Product.cs
```

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
We can create now the `HQContext` that will handle requests to the global schema. It is important to keep this context in its own schema with its own migrations history table. To do that we will override the `OnConfiguring` method to add any configuration we may need (like the connection string).

```bash
mkdir Contexts
touch Contexts/HQContext.cs
```

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

We need to update the `appsettings.json` to include our connection to the database (local or remote).

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

And voilà, we are set to perform our first migration. Because we will have multiple contexts we need to specify them in the command, also we will separate them in two directories to keep things organized.

```bash
dotnet ef migrations add InitialHQ_StoresProducts --context HQContext --output-dir Migrations/HQ
```

To perform those migrations on every startup, we can extend the `Program.cs` to include those operations right after we build the `WebApplication app`:

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

For the controllers let's keep it simple, just a `GET` and a `POST`. There's no need for a full CRUD api in this proof of concept. We are also not going to implement any services/repositories for the sake of brevidity, therefore we are goint to inject the `HQContext` directly in the `StoresController`

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

And let's create a `ProductsController` in the same fashion:

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

> \* The `isDiscontinued` prop can be ommited. It will default to `false`.

🎉 We are all set for the HQ tables and data. Let's move on to the store per store api.

