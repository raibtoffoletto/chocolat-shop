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