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

        Store? _store = await _hQContext.Stores.FirstOrDefaultAsync(
            x => EF.Functions.ILike(x.Code, store.Code)
        );

        if (_store == null)
        {
            _hQContext.Stores.Add(store);
        }
        else
        {
            _store.Name = store.Name;
            _store.City = store.City;
            _store.Schema = store.Schema;
        }

        await _hQContext.SaveChangesAsync();

        return store;
    }

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
}
