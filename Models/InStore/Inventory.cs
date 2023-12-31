using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChocolateStores.Models.InStore;

[Table("inventory")]
public class Inventory
{
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("stock")]
    public int Stock { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("last_order")]
    public DateTime LastOrder { get; set; } = DateTime.UtcNow;
}

public class CatalogueConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.HasKey(x => x.Code);
    }
}
