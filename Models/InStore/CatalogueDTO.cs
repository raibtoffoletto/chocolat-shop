namespace ChocolateStores.Models.InStore;

public class CatalogueDTO
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Stock { get; set; }
}
