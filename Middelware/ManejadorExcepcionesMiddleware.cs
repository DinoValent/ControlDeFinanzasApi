using System.Net;
using System.Text.Json;

namespace GestorGastos.Api.Middleware;

public class ManejadorExcepcionesMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ManejadorExcepcionesMiddleware> _logger;

    public ManejadorExcepcionesMiddleware(RequestDelegate next, ILogger<ManejadorExcepcionesMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var mensaje = ex switch
            {
                HttpRequestException => "El servicio de IA no está disponible en este momento. Probá de nuevo en unos minutos.",
                TimeoutException => "La operación tardó demasiado. Probá con un archivo más chico o intentá de nuevo.",
                _ => "Ocurrió un error inesperado. Si persiste, contactá soporte."
            };

            var respuesta = JsonSerializer.Serialize(new { error = mensaje });
            await context.Response.WriteAsync(respuesta);
        }
    }
}