namespace ChocolateStores.Models.InStore;

public class CatalogueDTO
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal RSP { get; set; }

    public int Stock { get; set; }

    public decimal Difference =>
        decimal.Round((100 * (Price == 0 ? 1 : Price) / (RSP == 0 ? 1 : RSP)) - 100, 2);
}
