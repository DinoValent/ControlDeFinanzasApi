namespace GestorGastos.Api.DTOs;

public class ConfirmarMovimientoRequest
{
    public int CuentaId { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
    public string DescripcionOriginal { get; set; } = string.Empty;
    public string? DescripcionLimpia { get; set; }
    public int? CategoriaId { get; set; }
    public float Confianza { get; set; }
    public string OrigenCarga { get; set; } = "Manual";
}