using System.ComponentModel.DataAnnotations;

namespace GestorGastos.Api.DTOs;

public class MovimientoManualRequest
{
	[Required(ErrorMessage = "La cuenta es obligatoria")]
	public int CuentaId { get; set; }

	[Required(ErrorMessage = "La descripción es obligatoria")]
	[MinLength(3, ErrorMessage = "La descripción es muy corta")]
	public string Descripcion { get; set; } = string.Empty;

	[Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo")]
	public decimal? Monto { get; set; }

	public DateTime? Fecha { get; set; }
}