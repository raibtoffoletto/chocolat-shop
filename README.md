# Multi-tenancy using schemas with Entity Framework and PostgreSQL

| Approach| Column for Tenant?|Schema per Tenant?|Multiple Databases?|EF Core Support|
|---|---|---|---|---|
|Discriminator (column)|Yes|No|No|Global query filter|
|Database per tenant|No|No|Yes|Configuration|
|Schema per tenant|No|Yes|No|Not supported|

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