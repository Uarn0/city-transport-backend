using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityTransport.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStopsAndAddRouteStops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stops_Routes_RouteId",
                table: "Stops");

            migrationBuilder.DropIndex(
                name: "IX_Stops_RouteId",
                table: "Stops");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Stops");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "Stops");

            migrationBuilder.AddColumn<long>(
                name: "OsmId",
                table: "Stops",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportType",
                table: "Stops",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RouteStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    StopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStops_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteStops_Stops_StopId",
                        column: x => x.StopId,
                        principalTable: "Stops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stops_OsmId",
                table: "Stops",
                column: "OsmId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_RouteId_Order",
                table: "RouteStops",
                columns: new[] { "RouteId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_StopId",
                table: "RouteStops",
                column: "StopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteStops");

            migrationBuilder.DropIndex(
                name: "IX_Stops_OsmId",
                table: "Stops");

            migrationBuilder.DropColumn(
                name: "OsmId",
                table: "Stops");

            migrationBuilder.DropColumn(
                name: "TransportType",
                table: "Stops");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Stops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RouteId",
                table: "Stops",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Stops_RouteId",
                table: "Stops",
                column: "RouteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stops_Routes_RouteId",
                table: "Stops",
                column: "RouteId",
                principalTable: "Routes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
