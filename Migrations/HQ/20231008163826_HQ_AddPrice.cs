using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChocolateStores.Migrations.HQ;

public partial class HQ_AddPrice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "price",
            schema: "hq",
            table: "products",
            type: "numeric",
            nullable: false,
            defaultValue: 0m
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "price", schema: "hq", table: "products");
    }
}
