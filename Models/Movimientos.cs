namespace GestorGastos.Api.Models;

public class Movimiento
{
    public int Id { get; set; }

    public int CuentaId { get; set; }
    public Cuenta? Cuenta { get; set; }

    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }

    public string DescripcionOriginal { get; set; } = string.Empty;
    public string? DescripcionLimpia { get; set; }

    public int? CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public float ConfianzaIA { get; set; }
    public bool RevisadoPorUsuario { get; set; }
    public string OrigenCarga { get; set; } = "Manual"; // Manual, CSV, Imagen
}