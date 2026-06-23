using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestorGastos.Api.Data;
using GestorGastos.Api.Models;

namespace GestorGastos.Api.Controllers;

[ApiController]
[Route("api/cuentas")]
public class CuentasController : ControllerBase
{
    private readonly AppDbContext _db;
    private const string UserIdFijo = "demo-user"; // temporal, hasta meter auth

    public CuentasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cuentas = await _db.Cuentas
            .Where(c => c.UserId == UserIdFijo)
            .ToListAsync();
        return Ok(cuentas);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] Cuenta cuenta)
    {
        cuenta.UserId = UserIdFijo;
        _db.Cuentas.Add(cuenta);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), cuenta);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta == null) return NotFound();

        _db.Cuentas.Remove(cuenta);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}