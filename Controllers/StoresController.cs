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
