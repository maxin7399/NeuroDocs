using System.Text.RegularExpressions;
using NeuroDocs.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NeuroDocs.Services;

public sealed class FirmaExtractor
{
    // "T.P. 255291", "TP 109758", "Reg. Profesional: TP..."
    private static readonly Regex RegistroRegex = new(
        @"^(T\.?\s*P\.?|Reg\.?\s*Profesional)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Distancia máxima entre la firma y la línea del registro, para no confundir otra imagen con la firma.
    private const double DistanciaMaximaPt = 150;

    /// <summary>
    /// Busca la firma usando como ancla la línea del registro profesional: la firma es la imagen
    /// más cercana por encima, y el bloque de texto son las líneas entre la imagen y el registro.
    /// </summary>
    /// <param name="lineas">Líneas ya extraídas del mismo PDF (evita leerlo dos veces).</param>
    public Firma? Extraer(string pdfPath, IReadOnlyList<PdfLine> lineas)
    {
        using var documento = PdfDocument.Open(pdfPath);

        // La firma suele estar al final: se recorre desde la última página.
        for (int numero = documento.NumberOfPages; numero >= 1; numero--)
        {
            var lineasPagina = lineas.Where(l => l.Page == numero).ToList();
            var registro = lineasPagina.LastOrDefault(l => RegistroRegex.IsMatch(l.Text.Trim()));
            if (registro is null) continue;

            var imagen = documento.GetPage(numero).GetImages()
                .Where(i => i.Bounds.Bottom > registro.Y && i.Bounds.Bottom - registro.Y <= DistanciaMaximaPt)
                .OrderBy(i => i.Bounds.Bottom)
                .FirstOrDefault();
            if (imagen is null || !TryObtenerBytes(imagen, out var bytes, out var formato)) continue;

            var bloque = lineasPagina
                .Where(l => l.Y < imagen.Bounds.Bottom && l.Y >= registro.Y)
                .OrderByDescending(l => l.Y) // de arriba hacia abajo
                .Select(l => l.Text.Trim())
                .ToList();

            return new Firma(bytes, formato, imagen.Bounds.Width, imagen.Bounds.Height, bloque);
        }

        return null;
    }

    private static bool TryObtenerBytes(IPdfImage imagen, out byte[] bytes, out FormatoImagen formato)
    {
        if (imagen.TryGetPng(out var png))
        {
            bytes = png;
            formato = FormatoImagen.Png;
            return true;
        }

        // Las imágenes JPEG (típicas de firmas escaneadas) vienen tal cual en el PDF.
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