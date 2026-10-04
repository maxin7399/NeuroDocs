using System.Text.RegularExpressions;
using NeuroDocs.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NeuroDocs.Services;

/// <summary>
/// Extrae la firma del profesional por posición. Soporta los formatos vistos:
/// imagen con nombre/título/registro como texto debajo, o con todo eso dentro de la imagen.
/// </summary>
public sealed class FirmaExtractor
{
    // "T.P. 255291", "TP 109758", "Reg. Profesional: TP 192380"
    private static readonly Regex RegistroRegex = new(
        @"^(T\.?\s*P\.?|Reg\.?\s*Profesional)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const double DistanciaMaximaRegistroPt = 150; // entre la firma y la línea del registro
    private const double ZonaEncabezado = 0.20;           // 20 % superior de la página: logo
    private const double DistanciaPrimeraLineaPt = 30;    // del borde de la imagen a la primera línea del bloque
    private const double SaltoMaximoPt = 18;              // entre líneas del mismo bloque

    public Firma? Extraer(string pdfPath, IReadOnlyList<PdfLine> lineas)
    {
        using var documento = PdfDocument.Open(pdfPath);
        var paginas = documento.GetPages().ToList();
        var imagenesPorPagina = paginas.ToDictionary(p => p.Number, p => p.GetImages().ToList());

        // Imágenes en la misma posición en varias páginas (logo del encabezado): nunca son la firma.
        var repetidas = imagenesPorPagina.Values
            .SelectMany(imagenes => imagenes.Select(Clave).Distinct())
            .GroupBy(clave => clave)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        // La firma suele estar al final: se recorre desde la última página.
        foreach (var pagina in paginas.OrderByDescending(p => p.Number))
        {
            double limiteEncabezado = pagina.Height * (1 - ZonaEncabezado);
            var candidatas = imagenesPorPagina[pagina.Number]
                .Where(i => !repetidas.Contains(Clave(i)) && i.Bounds.Top < limiteEncabezado)
                .ToList();
            if (candidatas.Count == 0) continue;

            var lineasPagina = lineas.Where(l => l.Page == pagina.Number).ToList();
            var imagen = ElegirImagen(candidatas, lineasPagina);

            if (!TryObtenerBytes(imagen, out var bytes, out var formato)) continue;

            return new Firma(bytes, formato, imagen.Bounds.Width, imagen.Bounds.Height,
                             BloqueDebajo(lineasPagina, imagen.Bounds.Bottom));
        }

        return null;
    }

    /// <summary>
    /// Con línea de registro: la imagen más cercana por encima de ella.
    /// Sin registro (p. ej. cuando el nombre viene dentro de la imagen): la imagen más baja de la página.
    /// </summary>
    private static IPdfImage ElegirImagen(List<IPdfImage> candidatas, List<PdfLine> lineasPagina)
    {
        var registro = lineasPagina.LastOrDefault(l => RegistroRegex.IsMatch(l.Text.Trim()));

        var sobreRegistro = registro is null ? null : candidatas
            .Where(i => i.Bounds.Bottom > registro.Y && i.Bounds.Bottom - registro.Y <= DistanciaMaximaRegistroPt)
            .OrderBy(i => i.Bounds.Bottom)
            .FirstOrDefault();

        return sobreRegistro ?? candidatas.OrderBy(i => i.Bounds.Bottom).First();
    }

    /// <summary>Líneas pegadas debajo de la imagen, hasta el primer salto vertical grande.</summary>
    private static List<string> BloqueDebajo(List<PdfLine> lineasPagina, double bordeInferiorImagen)
    {
        var bloque = new List<string>();
        double? anterior = null;

        foreach (var linea in lineasPagina.Where(l => l.Y < bordeInferiorImagen).OrderByDescending(l => l.Y))
        {
            double distancia = (anterior ?? bordeInferiorImagen) - linea.Y;
            double maximo = anterior is null ? DistanciaPrimeraLineaPt : SaltoMaximoPt;
            if (distancia > maximo) break;

            bloque.Add(linea.Text.Trim());
            anterior = linea.Y;
        }

        return bloque;
    }

    private static (double, double, double, double) Clave(IPdfImage imagen) =>
        (Math.Round(imagen.Bounds.Left), Math.Round(imagen.Bounds.Bottom),
         Math.Round(imagen.Bounds.Width), Math.Round(imagen.Bounds.Height));

    private static bool TryObtenerBytes(IPdfImage imagen, out byte[] bytes, out FormatoImagen formato)
    {
        if (imagen.TryGetPng(out var png))
        {
            bytes = png;
            formato = FormatoImagen.Png;
            return true;
        }

        var crudos = imagen.RawBytes.ToArray();
        if (crudos.Length > 2 && crudos[0] == 0xFF && crudos[1] == 0xD8)
        {
            bytes = crudos;
            formato = FormatoImagen.Jpeg;
            return true;
        }

        bytes = [];
        formato = default;
        return false;
    }
}