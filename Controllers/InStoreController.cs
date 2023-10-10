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
    public async Task<IEnumerable<Inventory>> GetStock()
    {
        _logger.LogDebug("Getting inventory for store: {store}", _storeContext.Schema);

        return await _storeContext.Inventory.OrderByDescending(x => x.LastOrder).ToListAsync();
    }

    [HttpPost("Inventory"), UseStoreHeader]
    public async Task<Inventory> PostInventory(Inventory stock)
    {
        _logger.LogDebug("Updating inventory for store: {store}", _storeContext.Schema);

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

        if (inventory == null)
        {
            _storeContext.Inventory.Add(stock);
        }
        else
        {
            inventory.Stock = stock.Stock;
            inventory.Price = stock.Price;
            inventory.LastOrder = stock.LastOrder;
        }

        await _storeContext.SaveChangesAsync();

        return stock;
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
                        Price = c.Price,
                        RSP = p.Price,
                        Stock = c.Stock
                    }
            )
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
