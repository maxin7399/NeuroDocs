using System.Diagnostics.CodeAnalysis;
using NeuroDocs.Models;
using static NeuroDocs.Services.TextoUtil;

namespace NeuroDocs.Services;

/// <summary>Identifica si el PDF es una solicitud de orden de servicio (CUPS) o una orden de cobro, y lo lee.</summary>
public sealed class LectorOrden
{
    // Lo habitual en el centro cuando la orden no indica el tiempo de terapia.
    public const int SesionesPorDefecto = 10;
    public const int MesesPorDefecto = 3;

    public const string MensajeNoReconocido =
        "El PDF de la orden no es una solicitud de orden de servicio (CUPS) ni una orden de cobro. " +
        "Revisa que hayas seleccionado el archivo correcto.";

    private readonly OrdenServicioParser _servicio = new();
    private readonly OrdenCobroParser _cobro = new();

    /// <summary>Devuelve false si el PDF no es un tipo de orden conocido (error del usuario, no excepcional).</summary>
    public bool TryParse(IReadOnlyList<PdfLine> lines, [NotNullWhen(true)] out DatosOrden? orden)
    {
        orden = Detectar(lines) switch
        {
            TipoOrden.OrdenCobro => _cobro.Parse(lines),
            TipoOrden.SolicitudServicio => _servicio.Parse(lines),
            _ => null
        };

        if (orden is { SesionesMensuales: null } or { Meses: null })
        {
            orden = orden with
            {
                SesionesMensuales = SesionesPorDefecto,
                Meses = MesesPorDefecto,
                TiempoPorDefecto = true
            };
        }

        return orden is not null;
    }

    public static TipoOrden? Detectar(IReadOnlyList<PdfLine> lines)
    {
        var textos = lines.Select(l => Normalizar(l.Text)).ToList();

        if (textos.Any(t => t.Contains("orden de cobro"))) return TipoOrden.OrdenCobro;
        if (textos.Any(t => t.Contains("solicitud de orden de servicio"))) return TipoOrden.SolicitudServicio;
        return null;
    }
}