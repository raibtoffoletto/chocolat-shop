using ChocolateStores.Context;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<HQContext>();
builder.Services.AddDbContext<InStoreContext>();

WebApplication app = builder.Build();

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

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
