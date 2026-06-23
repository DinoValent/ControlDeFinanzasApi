using Microsoft.EntityFrameworkCore;
using GestorGastos.Api.Models;

namespace GestorGastos.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movimiento>()
            .Property(m => m.Monto)
            .HasColumnType("decimal(18,2)");

        // Seed de categorías default, así no arrancás con la app vacía
        modelBuilder.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nombre = "Comida", EsDefault = true },
            new Categoria { Id = 2, Nombre = "Transporte", EsDefault = true },
            new Categoria { Id = 3, Nombre = "Servicios", EsDefault = true },
            new Categoria { Id = 4, Nombre = "Entretenimiento", EsDefault = true },
            new Categoria { Id = 5, Nombre = "Salud", EsDefault = true },
            new Categoria { Id = 6, Nombre = "Otros", EsDefault = true }
        );
    }
}