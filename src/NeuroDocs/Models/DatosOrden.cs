namespace NeuroDocs.Models;

public enum TipoOrden { SolicitudServicio, OrdenCobro }

public sealed record DatosOrden(
    TipoOrden Tipo,
    string? NumeroDocumento,
    int? SesionesMensuales,
    int? Meses,
    string? Cie10Codigo,
    string? Cie10Descripcion,
    int? CantidadAutorizada = null,
    bool TiempoPorDefecto = false);