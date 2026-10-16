using NeuroDocs.Models;

namespace NeuroDocs.Services;

/// <summary>Prioridad: Impresión Diagnóstica del ECC; si no la trae, la descripción CIE-10 de la orden.</summary>
public static class ResolverDiagnostico
{
    public static string Texto(DatosPaciente paciente, DatosOrden orden) =>
        paciente.Diagnosticos.Count > 0
            ? string.Join("; ", paciente.Diagnosticos.Select(d => d.TrimEnd().TrimEnd('.')))
            : orden.Cie10Descripcion is { } descripcion ? TextoUtil.MayusculaInicial(descripcion) : "";

    public static string Origen(DatosPaciente paciente, DatosOrden orden) =>
        paciente.Diagnosticos.Count > 0 ? "historia clínica"
        : orden.Cie10Descripcion is not null ? "orden de servicio (CIE-10)"
        : "no encontrado";
}