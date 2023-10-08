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

        Product product =
            await _storeContext.Products.FirstOrDefaultAsync(
                x => EF.Functions.ILike(x.Code, stock.Code)
            ) ?? throw new Exception("Product code does not exist");

        if (product.IsDiscontinued)
        {
            throw new Exception("Product is discontinued, cannot order any more");
        }

        Catalogue? catalogue = await _storeContext.Catalogue.FirstOrDefaultAsync(
            x => x.Code == product.Code
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

    [HttpGet("Catalogue"), UseStoreHeader]
    public async Task<IEnumerable<CatalogueDTO>> GetCatalogue()
    {
        _logger.LogDebug("Getting catalogue for store: {store}", _storeContext.Schema);

        return await _storeContext.Catalogue
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
