using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestorGastos.Api.Data;
using GestorGastos.Api.DTOs;
using GestorGastos.Api.Models;
using GestorGastos.Api.Services;

namespace GestorGastos.Api.Controllers;

[ApiController]
[Route("api/movimientos")]
public class MovimientosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICategorizadorIA _categorizador;

    public MovimientosController(AppDbContext db, ICategorizadorIA categorizador)
    {
        _db = db;
        _categorizador = categorizador;
    }

    // Paso 1: el usuario escribe el gasto en texto libre, la IA sugiere
    [HttpPost("manual")]
    public async Task<ActionResult<MovimientoSugeridoResponse>> Manual([FromBody] MovimientoManualRequest req)
    {
        var categorias = await _db.Categorias.ToListAsync();

        var resultado = await _categorizador.CategorizarAsync(req.Descripcion, req.Monto, categorias);

        var categoria = categorias.FirstOrDefault(c => c.Id == resultado.CategoriaId);

        return Ok(new MovimientoSugeridoResponse
        {
            CategoriaId = resultado.CategoriaId,
            CategoriaNombre = categoria?.Nombre,
            DescripcionLimpia = resultado.DescripcionLimpia,
            Monto = resultado.MontoInferido,
            Confianza = resultado.Confianza
        });
    }

    // Paso 2: el usuario confirma (o corrigió la categoría) y se guarda definitivo
    [HttpPost("confirmar")]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmarMovimientoRequest req)
    {
        var movimiento = new Movimiento
        {
            CuentaId = req.CuentaId,
            Monto = req.Monto,
            Fecha = req.Fecha,
            DescripcionOriginal = req.DescripcionOriginal,
            DescripcionLimpia = req.DescripcionLimpia,
            CategoriaId = req.CategoriaId,
            ConfianzaIA = req.Confianza,
            RevisadoPorUsuario = true,
            OrigenCarga = req.OrigenCarga
        };

        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = movimiento.Id }, movimiento);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? cuentaId, [FromQuery] int? categoriaId)
    {
        var query = _db.Movimientos.Include(m => m.Categoria).Include(m => m.Cuenta).AsQueryable();

        if (cuentaId.HasValue) query = query.Where(m => m.CuentaId == cuentaId);
        if (categoriaId.HasValue) query = query.Where(m => m.CategoriaId == categoriaId);

        var movimientos = await query.OrderByDescending(m => m.Fecha).ToListAsync();
        return Ok(movimientos);
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen()
    {
        var resumen = await _db.Movimientos
            .Include(m => m.Categoria)
            .GroupBy(m => m.Categoria!.Nombre)
            .Select(g => new { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
            .ToListAsync();

        return Ok(resumen);
    }
    [HttpPost("importar-csv")]
    public async Task<IActionResult> ImportarCsv(IFormFile archivo, [FromForm] int cuentaId)
    {
        if (archivo.Length == 0)
            return BadRequest("El archivo está vacío.");

        var movimientosCrudos = new List<MovimientoCrudo>();

        using var stream = archivo.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.GetCultureInfo("es-AR"));

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            // AJUSTAR ESTOS NOMBRES según las columnas reales de tu export
            // (Mercado Pago, Lemon Cash, etc. tienen headers distintos)
            var descripcion = csv.GetField("Descripcion") ?? csv.GetField("Detalle") ?? "";
            var montoTexto = csv.GetField("Monto") ?? csv.GetField("Importe") ?? "0";
            var fechaTexto = csv.GetField("Fecha") ?? "";

            if (string.IsNullOrWhiteSpace(descripcion)) continue;

            decimal.TryParse(montoTexto.Replace("$", "").Replace(".", "").Replace(",", "."),
                System.Globalization.CultureInfo.InvariantCulture, out var monto);
            DateTime.TryParse(fechaTexto, System.Globalization.CultureInfo.GetCultureInfo("es-AR"),
                System.Globalization.DateTimeStyles.None, out var fecha);

            movimientosCrudos.Add(new MovimientoCrudo(descripcion, Math.Abs(monto),
                fecha == default ? DateTime.Now : fecha));
        }

        if (movimientosCrudos.Count == 0)
            return BadRequest("No se encontraron filas válidas en el CSV.");

        var categorias = await _db.Categorias.ToListAsync();
        var preview = new List<MovimientoCsvPreview>();

        // Procesamos en lotes de 20 para no mandar todo de una ni hacer 1 request por fila
        const int tamañoLote = 10;
        for (int i = 0; i < movimientosCrudos.Count; i += tamañoLote)
        {
            var lote = movimientosCrudos.Skip(i).Take(tamañoLote).ToList();
            var resultados = await _categorizador.CategorizarLoteAsync(lote, categorias);

            for (int j = 0; j < lote.Count; j++)
            {
                var categoria = categorias.FirstOrDefault(c => c.Id == resultados[j].CategoriaId);
                preview.Add(new MovimientoCsvPreview
                {
                    DescripcionOriginal = lote[j].Descripcion,
                    DescripcionLimpia = resultados[j].DescripcionLimpia,
                    Monto = resultados[j].MontoInferido,
                    Fecha = lote[j].Fecha,
                    CategoriaId = resultados[j].CategoriaId,
                    CategoriaNombre = categoria?.Nombre,
                    Confianza = resultados[j].Confianza
                });
            }
        }

        return Ok(preview);
    }

    [HttpPost("confirmar-lote")]
    public async Task<IActionResult> ConfirmarLote([FromBody] ConfirmarLoteRequest req)
    {
        var movimientos = req.Movimientos.Select(m => new Movimiento
        {
            CuentaId = req.CuentaId,
            Monto = m.Monto,
            Fecha = m.Fecha,
            DescripcionOriginal = m.DescripcionOriginal,
            DescripcionLimpia = m.DescripcionLimpia,
            CategoriaId = m.CategoriaId,
            ConfianzaIA = m.Confianza,
            RevisadoPorUsuario = true,
            OrigenCarga = "CSV"
        }).ToList();

        _db.Movimientos.AddRange(movimientos);
        await _db.SaveChangesAsync();

        return Ok(new { cantidadImportada = movimientos.Count });
    }
    [HttpPost("importar-pdf")]
    public async Task<IActionResult> ImportarPdf(IFormFile archivo, [FromForm] int cuentaId,
    [FromForm] int año, [FromForm] string nombreTitular)
    {
        if (archivo.Length == 0) return BadRequest("El archivo está vacío.");

        var extractor = new ExtractorPdfBancario();
        List<MovimientoExtraidoPdf> extraidos;

        using (var stream = archivo.OpenReadStream())
        {
            extraidos = extractor.Extraer(stream, año);
        }

        // Solo nos interesan los EGRESOS para un gestor de gastos
        var egresos = extraidos.Where(m => !m.EsIngreso).ToList();

        if (egresos.Count == 0)
            return BadRequest("No se encontraron egresos en el PDF.");

        var categorias = await _db.Categorias.ToListAsync();
        var preview = new List<MovimientoCsvPreview>();

        const int tamañoLote = 20;
        for (int i = 0; i < egresos.Count; i += tamañoLote)
        {
            var lote = egresos.Skip(i).Take(tamañoLote)
                .Select(m => new MovimientoCrudo(
                    $"{m.Descripcion} [Titular de la cuenta: {nombreTitular}]", m.Monto, m.Fecha))
                .ToList();

            var resultados = await _categorizador.CategorizarLoteAsync(lote, categorias);

            for (int j = 0; j < lote.Count; j++)
            {
                var categoria = categorias.FirstOrDefault(c => c.Id == resultados[j].CategoriaId);
                preview.Add(new MovimientoCsvPreview
                {
                    DescripcionOriginal = egresos[i + j].Descripcion,
                    DescripcionLimpia = resultados[j].DescripcionLimpia,
                    Monto = resultados[j].MontoInferido,
                    Fecha = egresos[i + j].Fecha,
                    CategoriaId = resultados[j].CategoriaId,
                    CategoriaNombre = categoria?.Nombre,
                    Confianza = resultados[j].Confianza
                });
            }
        }

        return Ok(preview);
    }
    [HttpPost("debug-pdf")]
    public async Task<IActionResult> DebugPdf(IFormFile archivo)
    {
        using var stream = archivo.OpenReadStream();
        using var documento = UglyToad.PdfPig.PdfDocument.Open(stream);

        var texto = string.Join("\n---PAGINA---\n", documento.GetPages().Select(p => p.Text));
        return Ok(new { textoCompleto = texto });
    }

    [HttpGet("resumen-periodo")]
    public async Task<IActionResult> ResumenPeriodo([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        desde = DateTime.SpecifyKind(desde, DateTimeKind.Utc);
        hasta = DateTime.SpecifyKind(hasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc); // incluye todo el día "hasta"
                                                                                            // Excluimos transferencias propias (CategoriaId null con ese origen) de los totales de gasto real
        var query = _db.Movimientos
      .Include(m => m.Categoria)
      .Where(m => m.Fecha >= desde && m.Fecha <= hasta && m.CategoriaId != null);


        var movimientos = await query.ToListAsync();

        var total = movimientos.Sum(m => m.Monto);
        var dias = Math.Max(1, (hasta.Date - desde.Date).Days + 1);

        var categoriaTop = movimientos
            .GroupBy(m => m.Categoria!.Nombre)
            .OrderByDescending(g => g.Sum(m => m.Monto))
            .Select(g => g.Key)
            .FirstOrDefault();

        // Calculamos el período anterior de igual longitud, para la comparación
        var duracion = hasta - desde;
        var desdeAnterior = DateTime.SpecifyKind(desde - duracion - TimeSpan.FromDays(1), DateTimeKind.Utc);
        var hastaAnterior = DateTime.SpecifyKind(desde - TimeSpan.FromDays(1), DateTimeKind.Utc);

        var totalAnterior = await _db.Movimientos
            .Where(m => m.Fecha >= desdeAnterior && m.Fecha <= hastaAnterior && m.CategoriaId != null)
            .SumAsync(m => m.Monto);

        float? variacion = totalAnterior > 0
            ? (float)((total - totalAnterior) / totalAnterior * 100)
            : null;

        var porCategoria = movimientos
            .GroupBy(m => m.Categoria!.Nombre)
            .Select(g => new ResumenCategoria { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
            .OrderByDescending(r => r.Total)
            .ToList();

        var tendencia = movimientos
            .GroupBy(m => m.Fecha.Date)
            .Select(g => new PuntoTendencia { Fecha = g.Key, Total = g.Sum(m => m.Monto) })
            .OrderBy(p => p.Fecha)
            .ToList();

        return Ok(new ResumenPeriodoResponse
        {
            Total = total,
            CantidadMovimientos = movimientos.Count,
            PromedioDiario = total / dias,
            CategoriaTop = categoriaTop,
            TotalPeriodoAnterior = totalAnterior,
            VariacionPorcentual = variacion,
            PorCategoria = porCategoria,
            Tendencia = tendencia
        });
    }
    [HttpPost("sugerir-lote-texto")]
    public async Task<IActionResult> SugerirLoteTexto([FromBody] SugerirLoteTextoRequest req)
    {
        var lineas = req.Texto
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length >= 3)
            .ToList();

        if (lineas.Count == 0)
            return BadRequest("No se encontraron líneas válidas para procesar.");

        var movimientosCrudos = lineas
            .Select(l => new MovimientoCrudo(l, null, DateTime.UtcNow))
            .ToList();

        var categorias = await _db.Categorias.ToListAsync();
        var resultados = await _categorizador.CategorizarLoteAsync(movimientosCrudos, categorias);

        var preview = resultados.Select((r, i) =>
        {
            var categoria = categorias.FirstOrDefault(c => c.Id == r.CategoriaId);
            return new MovimientoCsvPreview
            {
                DescripcionOriginal = lineas[i],
                DescripcionLimpia = r.DescripcionLimpia,
                Monto = r.MontoInferido,
                Fecha = DateTime.UtcNow,
                CategoriaId = r.CategoriaId,
                CategoriaNombre = categoria?.Nombre,
                Confianza = r.Confianza
            };
        }).ToList();

        return Ok(preview);
    }
}