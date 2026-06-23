using System.Text;
using System.Text.Json;
using GestorGastos.Api.Models;

namespace GestorGastos.Api.Services;

public class OllamaCategorizadorService : ICategorizadorIA
{
    private readonly HttpClient _httpClient;
    private const string Modelo = "qwen2.5:14b"; 

    public OllamaCategorizadorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:11434");
        _httpClient.Timeout = TimeSpan.FromMinutes(2); // los modelos locales pueden tardar más que una API
    }

    public async Task<ResultadoCategorizacion> CategorizarAsync(
        string descripcionCruda, decimal? montoConocido, List<Categoria> categoriasDisponibles)
    {
        var listaCategorias = string.Join(", ", categoriasDisponibles.Select(c => $"{c.Id}:{c.Nombre}"));

        // CORRECCIÓN 1: Dos signos de dólar y doble llave para variables
        var prompt = $$"""
            Categorizá este gasto personal de Argentina.
            Descripción: "{{descripcionCruda}}"
            Monto conocido: {{montoConocido?.ToString() ?? "null"}}
            Categorías disponibles (id:nombre): {{listaCategorias}}

            Respondé solo con este JSON (sin texto adicional):
            {"categoria_id": <int o null>, "descripcion_limpia": "<texto corto>", "monto_inferido": <decimal>, "confianza": <float 0-1>}
            """;

        var textoRespuesta = await LlamarOllamaAsync(prompt);
        var resultado = JsonSerializer.Deserialize<JsonElement>(textoRespuesta);

        return new ResultadoCategorizacion(
            CategoriaId: resultado.TryGetProperty("categoria_id", out var c) && c.ValueKind != JsonValueKind.Null
                ? c.GetInt32() : null,
            DescripcionLimpia: resultado.GetProperty("descripcion_limpia").GetString() ?? descripcionCruda,
            MontoInferido: resultado.TryGetProperty("monto_inferido", out var m) ? m.GetDecimal() : (montoConocido ?? 0),
            Confianza: resultado.TryGetProperty("confianza", out var conf) ? conf.GetSingle() : 0.5f
        );
    }

    public async Task<List<ResultadoCategorizacion>> CategorizarLoteAsync(
     List<MovimientoCrudo> movimientos, List<Categoria> categoriasDisponibles)
    {
        var listaCategorias = string.Join(", ", categoriasDisponibles.Select(c => $"{c.Id}:{c.Nombre}"));
        var itemsTexto = string.Join("\n", movimientos.Select((m, i) =>
            $"{i}. \"{m.Descripcion}\", monto: {m.Monto?.ToString() ?? "null"}"));

        var prompt = $$"""
        Categorizá estos gastos personales de Argentina.
        Categorías disponibles (id:nombre): {listaCategorias}

        Si la descripción menciona transferencia hacia el mismo titular de la cuenta
        (verás "[Titular de la cuenta: NOMBRE]"), es un movimiento interno, no un gasto real:
        devolvé categoria_id null y descripcion_limpia "Transferencia entre cuentas propias".

        Movimientos a categorizar (son {movimientos.Count} en total):
        {itemsTexto}

        Tu respuesta DEBE ser un JSON array con exactamente {movimientos.Count} elementos.
        NO devuelvas un objeto único. NO devuelvas nada fuera del array.
        Formato de cada elemento:
        [{"indice": <int>, "categoria_id": <int o null>, "descripcion_limpia": "<texto corto>", "monto_inferido": <decimal>, "confianza": <float 0-1>}]
        """;

        var textoRespuesta = await LlamarOllamaAsync(prompt);

        Console.WriteLine("=== RESPUESTA CRUDA DE OLLAMA ===");
        Console.WriteLine(textoRespuesta);
        Console.WriteLine("=================================");

        var arrayResultado = ExtraerArrayDeRespuesta(textoRespuesta);

        var resultados = new List<ResultadoCategorizacion>();
        foreach (var item in arrayResultado.OrderBy(x => x.GetProperty("indice").GetInt32()))
        {
            var idx = item.GetProperty("indice").GetInt32();
            resultados.Add(new ResultadoCategorizacion(
                CategoriaId: item.TryGetProperty("categoria_id", out var c) && c.ValueKind != JsonValueKind.Null
                    ? c.GetInt32() : null,
                DescripcionLimpia: item.GetProperty("descripcion_limpia").GetString() ?? movimientos[idx].Descripcion,
                MontoInferido: item.TryGetProperty("monto_inferido", out var m) ? m.GetDecimal() : (movimientos[idx].Monto ?? 0),
                Confianza: item.TryGetProperty("confianza", out var conf) ? conf.GetSingle() : 0.5f
            ));
        }

        return resultados;
    }

    private async Task<string> LlamarOllamaAsync(string prompt)
    {
        var requestBody = new
        {
            model = Modelo,
            prompt = prompt,
            format = "json",   // fuerza JSON válido siempre, esto es lo lindo de Ollama
            stream = false
        };

        var response = await _httpClient.PostAsync("/api/generate",
            new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        return doc.RootElement.GetProperty("response").GetString() ?? "{}";
    }
    private List<JsonElement> ExtraerArrayDeRespuesta(string textoRespuesta)
    {
        var elemento = JsonSerializer.Deserialize<JsonElement>(textoRespuesta);

        // Caso normal: ya es un array
        if (elemento.ValueKind == JsonValueKind.Array)
            return elemento.EnumerateArray().ToList();

        // Caso envuelto: es un objeto, buscamos la primera propiedad que sea un array
        // (sin importar cómo se llame la clave: "movimientos", "resultado", o cualquier alucinación)
        if (elemento.ValueKind == JsonValueKind.Object)
        {
            foreach (var propiedad in elemento.EnumerateObject())
            {
                if (propiedad.Value.ValueKind == JsonValueKind.Array)
                    return propiedad.Value.EnumerateArray().ToList();
            }
        }

        throw new InvalidOperationException(
            $"No se encontró un array válido en la respuesta del modelo: {textoRespuesta[..Math.Min(200, textoRespuesta.Length)]}");
    }
}