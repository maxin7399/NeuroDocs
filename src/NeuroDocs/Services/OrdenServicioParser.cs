using System.Text.RegularExpressions;
using NeuroDocs.Models;
using static NeuroDocs.Services.TextoUtil;

namespace NeuroDocs.Services;

public sealed class OrdenServicioParser
{
    // "10 SESIONES MENSUALES POR 3 MESES": la frase se parte en dos líneas, por eso se busca en el texto unido.
    private static readonly Regex SesionesMesesRegex = new(
        @"(\d+)\s+sesiones\s+mensuales\s+por\s+(\d+)\s+mes",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DocumentoRegex = new(
        @"No\.?\s*Documento:\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "F067 - TRASTORNO COGNOSCITIVO LEVE"
    private static readonly Regex Cie10Regex = new(
        @"^([A-Z]\d{2,3}[A-Z0-9.]*)\s*-\s*(.+)$",
        RegexOptions.Compiled);

    public DatosOrden Parse(IReadOnlyList<PdfLine> lines)
    {
        string textoCompleto = string.Join(" ", lines.Select(l => l.Text));

        var sesionesMeses = SesionesMesesRegex.Match(textoCompleto);
        var documento = DocumentoRegex.Match(textoCompleto);
        var (codigo, descripcion) = ExtraerCie10(lines);

        return new DatosOrden(
            documento.Success ? documento.Groups[1].Value : null,
            sesionesMeses.Success ? int.Parse(sesionesMeses.Groups[1].Value) : null,
            sesionesMeses.Success ? int.Parse(sesionesMeses.Groups[2].Value) : null,
            codigo,
            descripcion);
    }

    private static (string? Codigo, string? Descripcion) ExtraerCie10(IReadOnlyList<PdfLine> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++)
        {
            if (!Normalizar(lines[i].Text).Contains("diagnostico cie-10")) continue;

            var match = Cie10Regex.Match(lines[i + 1].Text.Trim());
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value.Trim());
            }
        }
        return (null, null);
    }
}