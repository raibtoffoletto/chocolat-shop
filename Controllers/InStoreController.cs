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
