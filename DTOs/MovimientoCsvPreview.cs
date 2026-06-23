namespace GestorGastos.Api.DTOs;

public class MovimientoCsvPreview
{
    public string DescripcionOriginal { get; set; } = string.Empty;
    public string DescripcionLimpia { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
    public int? CategoriaId { get; set; }
    public string? CategoriaNombre { get; set; }
    public float Confianza { get; set; }
}