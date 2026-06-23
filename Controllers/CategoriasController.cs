using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestorGastos.Api.Data;

namespace GestorGastos.Api.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categorias = await _db.Categorias.ToListAsync();
        return Ok(categorias);
    }
}