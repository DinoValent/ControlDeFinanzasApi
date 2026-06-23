namespace GestorGastos.Api.DTOs;

public class ConfirmarLoteRequest
{
    public int CuentaId { get; set; }
    public List<MovimientoCsvPreview> Movimientos { get; set; } = new();
}