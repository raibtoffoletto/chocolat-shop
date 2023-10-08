using ChocolateStores.Context;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChocolateStores.Migrations.InStore
{
    public partial class InitialInStore_Catalogue : Migration
    {
        private readonly IInStoreContext _context;

        public InitialInStore_Catalogue(IInStoreContext context)
        {
            _context = context;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: _context.Schema);

            migrationBuilder.CreateTable(
                name: "catalogue",
                schema: _context.Schema,
                columns: table =>
                    new
                    {
                        code = table.Column<string>(type: "text", nullable: false),
                        stock = table.Column<int>(type: "integer", nullable: false),
                        last_order = table.Column<DateTime>(
                            type: "timestamp with time zone",
                            nullable: false
                        )
                    },
                constraints: table => table.PrimaryKey("PK_catalogue", x => x.code)
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "catalogue", schema: _context.Schema);
        }
    }
}
