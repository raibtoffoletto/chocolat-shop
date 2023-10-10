using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChocolateStores.Migrations.HQ;

public partial class InitialHQ_StoresProducts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "hq");

        migrationBuilder.CreateTable(
            name: "products",
            schema: "hq",
            columns: table =>
                new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    discontinued = table.Column<bool>(type: "boolean", nullable: false)
                },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.code);
            }
        );

        migrationBuilder.CreateTable(
            name: "stores",
            schema: "hq",
            columns: table =>
                new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    schema = table.Column<string>(type: "text", nullable: false)
                },
            constraints: table =>
            {
                table.PrimaryKey("PK_stores", x => x.code);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_stores_schema",
            schema: "hq",
            table: "stores",
            column: "schema",
            unique: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "products", schema: "hq");

        migrationBuilder.DropTable(name: "stores", schema: "hq");
    }
}
