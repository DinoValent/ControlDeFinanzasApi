using GestorGastos.Api.Models;

namespace GestorGastos.Api.Services;

public record ResultadoCategorizacion(
    int? CategoriaId,
    string DescripcionLimpia,
    decimal MontoInferido,
    float Confianza
);

public record MovimientoCrudo(string Descripcion, decimal? Monto, DateTime Fecha);

public interface ICategorizadorIA
{
    Task<ResultadoCategorizacion> CategorizarAsync(
        string descripcionCruda,
        decimal? montoConocido,
        List<Categoria> categoriasDisponibles);

    Task<List<ResultadoCategorizacion>> CategorizarLoteAsync(
        List<MovimientoCrudo> movimientos,
        List<Categoria> categoriasDisponibles);
}
