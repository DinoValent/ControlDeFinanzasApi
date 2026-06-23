using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace GestorGastos.Api.Services;

public record MovimientoExtraidoPdf(DateTime Fecha, string Descripcion, decimal Monto, bool EsIngreso);

public class ExtractorPdfBancario
{
    // Ancla en: DD/MES + número de operación (8+ dígitos) + descripción + $monto1 + $monto2(saldo)
    // El lookahead al final asegura que paramos justo antes de la siguiente fila o del final del texto
    private static readonly Regex RegexMovimiento = new(
        @"(\d{2})/([A-Z]{3})(\d{8,})(.*?)\$\s*([\d.,]+)\$\s*([\d.,]+)(?=\d{2}/[A-Z]{3}\d{8,}|$)",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, int> Meses = new()
    {
        ["ENE"] = 1,
        ["FEB"] = 2,
        ["MAR"] = 3,
        ["ABR"] = 4,
        ["MAY"] = 5,
        ["JUN"] = 6,
        ["JUL"] = 7,
        ["AGO"] = 8,
        ["SEP"] = 9,
        ["OCT"] = 10,
        ["NOV"] = 11,
        ["DIC"] = 12
    };

    public List<MovimientoExtraidoPdf> Extraer(Stream pdfStream, int año)
    {
        var movimientos = new List<MovimientoExtraidoPdf>();
        using var documento = PdfDocument.Open(pdfStream);

        // Unimos todas las páginas SIN separador: si un renglón se corta justo en el salto de
        // página, necesitamos que quede contiguo para que el regex lo pueda matchear igual.
        var textoCompleto = string.Join("", documento.GetPages().Select(p => p.Text));

        foreach (Match match in RegexMovimiento.Matches(textoCompleto))
        {
            var dia = match.Groups[1].Value;
            var mesTexto = match.Groups[2].Value;
            var descripcion = match.Groups[4].Value.Trim();
            var montoTexto = match.Groups[5].Value; // el segundo ($ monto2) es el saldo acumulado, lo ignoramos

            if (!Meses.TryGetValue(mesTexto, out var mes)) continue;

            var fecha = new DateTime(año, mes, int.Parse(dia), 0, 0, 0, DateTimeKind.Utc);
            var monto = decimal.Parse(montoTexto.Replace(".", "").Replace(",", "."),
                CultureInfo.InvariantCulture);

            bool esIngreso = descripcion.Contains("Rendimiento", StringComparison.OrdinalIgnoreCase)
                || descripcion.Contains("recibida", StringComparison.OrdinalIgnoreCase);

            movimientos.Add(new MovimientoExtraidoPdf(fecha, descripcion, monto, esIngreso));
        }

        return movimientos;
    }
}