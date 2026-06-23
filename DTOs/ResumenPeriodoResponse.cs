namespace GestorGastos.Api.DTOs;

public class ResumenPeriodoResponse
{
    public decimal Total { get; set; }
    public int CantidadMovimientos { get; set; }
    public decimal PromedioDiario { get; set; }
    public string? CategoriaTop { get; set; }
    public decimal TotalPeriodoAnterior { get; set; }
    public float? VariacionPorcentual { get; set; }
    public List<ResumenCategoria> PorCategoria { get; set; } = new();
    public List<PuntoTendencia> Tendencia { get; set; } = new();
}

public class ResumenCategoria
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class PuntoTendencia
{
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
}