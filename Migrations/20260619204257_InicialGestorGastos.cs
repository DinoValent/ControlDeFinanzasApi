using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestorGastos.Api.Migrations
{
    /// <inheritdoc />
    public partial class InicialGestorGastos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Icono = table.Column<string>(type: "text", nullable: true),
                    EsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cuentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuentas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movimientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CuentaId = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DescripcionOriginal = table.Column<string>(type: "text", nullable: false),
                    DescripcionLimpia = table.Column<string>(type: "text", nullable: true),
                    CategoriaId = table.Column<int>(type: "integer", nullable: true),
                    ConfianzaIA = table.Column<float>(type: "real", nullable: false),
                    RevisadoPorUsuario = table.Column<bool>(type: "boolean", nullable: false),
                    OrigenCarga = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movimientos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Movimientos_Cuentas_CuentaId",
                        column: x => x.CuentaId,
                        principalTable: "Cuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "EsDefault", "Icono", "Nombre" },
                values: new object[,]
                {
                    { 1, true, null, "Comida" },
                    { 2, true, null, "Transporte" },
                    { 3, true, null, "Servicios" },
                    { 4, true, null, "Entretenimiento" },
                    { 5, true, null, "Salud" },
                    { 6, true, null, "Otros" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_CategoriaId",
                table: "Movimientos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_CuentaId",
                table: "Movimientos",
                column: "CuentaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movimientos");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Cuentas");
        }
    }
}
