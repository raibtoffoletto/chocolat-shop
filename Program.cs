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

    hqContext.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
