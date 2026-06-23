using System.Text;
using System.Text.Json;
using GestorGastos.Api.Models;

namespace GestorGastos.Api.Services;

public class GeminiCategorizadorService : ICategorizadorIA
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiCategorizadorService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Falta Gemini:ApiKey en configuración");
    }

    public async Task<ResultadoCategorizacion> CategorizarAsync(
        string descripcionCruda,
        decimal? montoConocido,
        List<Categoria> categoriasDisponibles)
    {
        var listaCategorias = string.Join(", ", categoriasDisponibles.Select(c => $"{c.Id}:{c.Nombre}"));

        var prompt = $$"""
            Eres un asistente que categoriza gastos personales para una app de finanzas en Argentina.

            Descripción del gasto: "{{descripcionCruda}}"
            Monto conocido (puede ser null si no vino): {{montoConocido?.ToString() ?? "null"}}

            Categorías disponibles (id:nombre): {{listaCategorias}}

            Devolvé SOLO un JSON con esta forma exacta, sin texto adicional, sin markdown:
            {
              "categoria_id": <int o null si ninguna aplica bien>,
              "descripcion_limpia": "<versión corta y clara del gasto, ej 'Nafta YPF'>",
              "monto_inferido": <decimal>,
              "confianza": <float entre 0 y 1>
            }
            """;

        var resultadoJson = await LlamarGeminiAsync(prompt);
        var resultado = JsonSerializer.Deserialize<JsonElement>(resultadoJson);

        return new ResultadoCategorizacion(
            CategoriaId: resultado.TryGetProperty("categoria_id", out var catId) && catId.ValueKind != JsonValueKind.Null
                ? catId.GetInt32() : null,
            DescripcionLimpia: resultado.GetProperty("descripcion_limpia").GetString() ?? descripcionCruda,
            MontoInferido: resultado.TryGetProperty("monto_inferido", out var monto)
                ? monto.GetDecimal() : (montoConocido ?? 0),
            Confianza: resultado.TryGetProperty("confianza", out var conf)
                ? conf.GetSingle() : 0.5f
        );
    }

    public async Task<List<ResultadoCategorizacion>> CategorizarLoteAsync(
        List<MovimientoCrudo> movimientos, List<Categoria> categoriasDisponibles)
    {
        var listaCategorias = string.Join(", ", categoriasDisponibles.Select(c => $"{c.Id}:{c.Nombre}"));

        var itemsTexto = string.Join("\n", movimientos.Select((m, i) =>
            $"{i}. descripción: \"{m.Descripcion}\", monto: {m.Monto?.ToString() ?? "null"}"));

        var prompt = $$"""
            Eres un asistente que categoriza gastos personales para una app de finanzas en Argentina.

            Categorías disponibles (id:nombre): {{listaCategorias}}

            IMPORTANTE: si la descripción menciona una transferencia hacia el mismo titular de la cuenta
            (vas a ver "[Titular de la cuenta: NOMBRE]" en la descripción), esa transferencia es un movimiento
            interno entre cuentas propias, NO es un gasto real. En ese caso devolvé categoria_id: null y
            descripcion_limpia: "Transferencia entre cuentas propias".

            Categorizá cada uno de estos movimientos:
            {{itemsTexto}}

            Devolvé SOLO un JSON array (sin texto adicional, sin markdown) con un objeto por cada movimiento,
            en el MISMO ORDEN que te los pasé, con esta forma exacta:
            [
              {
                "indice": <int>,
                "categoria_id": <int o null>,
                "descripcion_limpia": "<versión corta y clara>",
                "monto_inferido": <decimal>,
                "confianza": <float entre 0 y 1>
              }
            ]
            """;

        var textoRespuesta = await LlamarGeminiAsync(prompt);
        var arrayResultado = JsonSerializer.Deserialize<List<JsonElement>>(textoRespuesta) ?? new();

        var resultados = new List<ResultadoCategorizacion>();
        foreach (var item in arrayResultado.OrderBy(x => x.GetProperty("indice").GetInt32()))
        {
            var idx = item.GetProperty("indice").GetInt32();
            resultados.Add(new ResultadoCategorizacion(
                CategoriaId: item.TryGetProperty("categoria_id", out var c) && c.ValueKind != JsonValueKind.Null
                    ? c.GetInt32() : null,
                DescripcionLimpia: item.GetProperty("descripcion_limpia").GetString() ?? movimientos[idx].Descripcion,
                MontoInferido: item.TryGetProperty("monto_inferido", out var m)
                    ? m.GetDecimal() : (movimientos[idx].Monto ?? 0),
                Confianza: item.TryGetProperty("confianza", out var conf) ? conf.GetSingle() : 0.5f
            ));
        }

        return resultados;
    }

    // Método privado compartido: evita repetir la llamada HTTP en los dos métodos públicos
    private async Task<string> LlamarGeminiAsync(string prompt)
    {
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

        const int maxIntentos = 3;
        for (int intento = 1; intento <= maxIntentos; intento++)
        {
            var response = await _httpClient.PostAsync(url,
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                var texto = doc.RootElement
                    .GetProperty("candidates")[0].GetProperty("content")
                    .GetProperty("parts")[0].GetProperty("text").GetString() ?? "{}";

                return texto.Replace("```json", "").Replace("```", "").Trim();
            }

            // Si es un error transitorio (503 = sobrecargado, 429 = rate limit), reintentamos con espera
            if ((int)response.StatusCode == 503 || (int)response.StatusCode == 429)
            {
                if (intento == maxIntentos)
                    throw new HttpRequestException($"Gemini no respondió después de {maxIntentos} intentos (último status: {response.StatusCode})");

                var esperaSegundos = intento * 3; // 3s, 6s, 9s
                await Task.Delay(TimeSpan.FromSeconds(esperaSegundos));
                continue;
            }

            // Cualquier otro error (400, 401, etc.) no tiene sentido reintentar, falla directo
            response.EnsureSuccessStatusCode();
        }

        throw new HttpRequestException("No se pudo obtener respuesta de Gemini.");
    }
}