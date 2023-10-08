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

        Product? _product = await _hQContext.Products.FirstOrDefaultAsync(
            x => EF.Functions.ILike(x.Code, product.Code)
        );

        if (_product == null)
        {
            _hQContext.Products.Add(product);
        }
        else
        {
            _product.Name = product.Name;
            _product.Type = product.Type;
            _product.Price = product.Price;
            _product.IsDiscontinued = product.IsDiscontinued;
        }

        await _hQContext.SaveChangesAsync();

        return product;
    }
}
