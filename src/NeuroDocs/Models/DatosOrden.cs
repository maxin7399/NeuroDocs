namespace NeuroDocs.Models;

public sealed record DatosOrden(
    string? NumeroDocumento,
    int? SesionesMensuales,
    int? Meses,
    string? Cie10Codigo,
    string? Cie10Descripcion);