using System.Text.RegularExpressions;
using NeuroDocs.Models;

namespace NeuroDocs.Services;

/// <summary>Orden de cobro de la EPS (SURA).</summary>
public sealed class OrdenCobroParser
{
    // "944301: 10 SESIONES X MES X 3 MESES - RENOVAR EN JULIO 2026..." (en Observaciones; no siempre está)
    private static readonly Regex SesionesMesesRegex = new(
        @"(\d+)\s*sesiones\s*x\s*mes\s*x\s*(\d+)\s*mes",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "CC 8601375 OSCARMIN MUÑOZ JIMENEZ ..." (también TI, CE, RC, PA)
    private static readonly Regex DocumentoRegex = new(
        @"^(?:CC|TI|CE|RC|PA)\s+(\d{5,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "944301 944301 944301 TERAPIA DE REHABILITACION COGNITIVA (SESION) F067 10"
    //  → código diagnóstico y cantidad autorizada (las dos últimas columnas)
    private static readonly Regex ProcedimientoRegex = new(
        @"^\d{6}\s+\d{6}\s+\d{6}\s+.+\s([A-Z]\d{2,3}[A-Z0-9]*)\s+(\d+)$",
        RegexOptions.Compiled);

    public DatosOrden Parse(IReadOnlyList<PdfLine> lines)
    {
        string? documento = lines
            .Select(l => DocumentoRegex.Match(l.Text.Trim()))
            .FirstOrDefault(m => m.Success)?.Groups[1].Value;

        var procedimiento = lines
            .Select(l => ProcedimientoRegex.Match(l.Text.Trim()))
            .FirstOrDefault(m => m.Success);

        var sesionesMeses = SesionesMesesRegex.Match(string.Join(" ", lines.Select(l => l.Text)));

        return new DatosOrden(
            TipoOrden.OrdenCobro,
            documento,
            sesionesMeses.Success ? int.Parse(sesionesMeses.Groups[1].Value) : null,
            sesionesMeses.Success ? int.Parse(sesionesMeses.Groups[2].Value) : null,
            procedimiento?.Groups[1].Value,
            Cie10Descripcion: null,
            CantidadAutorizada: procedimiento is null ? null : int.Parse(procedimiento.Groups[2].Value));
    }
}