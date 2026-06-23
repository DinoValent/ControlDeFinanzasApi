namespace GestorGastos.Api.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public bool EsDefault { get; set; }

    public List<Movimiento> Movimientos { get; set; } = new();
}