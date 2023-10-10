using ChocolateStores.Context;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChocolateStores.Migrations.InStore;

public partial class InStore_AddPrice : Migration
{
    private readonly IInStoreContext _context;

    public InStore_AddPrice(IInStoreContext context)
    {
        _context = context;
    }

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "price",
            schema: _context.Schema,
            table: "catalogue",
            type: "numeric",
            nullable: false,
            defaultValue: 0m
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "price", schema: _context.Schema, table: "catalogue");
    }
}
