using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityTransport.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutesAndTariffRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RouteId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteTariff",
                columns: table => new
                {
                    RoutesId = table.Column<Guid>(type: "uuid", nullable: false),
                    TariffsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteTariff", x => new { x.RoutesId, x.TariffsId });
                    table.ForeignKey(
                        name: "FK_RouteTariff_Routes_RoutesId",
                        column: x => x.RoutesId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteTariff_Tariffs_TariffsId",
                        column: x => x.TariffsId,
                        principalTable: "Tariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Routes",
                columns: new[] { "Id", "Name", "Number" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), "Центр - Вокзал", "10" });

            migrationBuilder.UpdateData(
                table: "Tariffs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "Загальний разовий");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RouteId",
                table: "Orders",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTariff_TariffsId",
                table: "RouteTariff",
                column: "TariffsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Routes_RouteId",
                table: "Orders",
                column: "RouteId",
                principalTable: "Routes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Routes_RouteId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "RouteTariff");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RouteId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "Orders");

            migrationBuilder.UpdateData(
                table: "Tariffs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "Разовий квиток");
        }
    }
}
