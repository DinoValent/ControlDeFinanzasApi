namespace GestorGastos.Api.Models;

public class Cuenta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;   // "Mercado Pago", "Lemon Cash", "Efectivo"
    public string Tipo { get; set; } = string.Empty;     // Wallet, Banco, Efectivo, Cripto
    public string UserId { get; set; } = string.Empty;

    public List<Movimiento> Movimientos { get; set; } = new();
}