using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChocolateStores.Models.InStore;

[Table("catalogue")]
public class Catalogue
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

public class CatalogueConfiguration : IEntityTypeConfiguration<Catalogue>
{
    public void Configure(EntityTypeBuilder<Catalogue> builder)
    {
        builder.HasKey(x => x.Code);
    }
}
