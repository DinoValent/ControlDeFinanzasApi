namespace GestorGastos.Api.DTOs;

public class MovimientoSugeridoResponse
{
    public int? CategoriaId { get; set; }
    public string? CategoriaNombre { get; set; }
    public string DescripcionLimpia { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public float Confianza { get; set; }
}