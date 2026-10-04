using NeuroDocs.Services;

namespace NeuroDocs.Models;

public sealed record DatosPlan(
    DatosPaciente Paciente,
    DatosOrden Orden,
    DateOnly EmisionPlan,
    FuncionesEvaluadas Funciones,
    Firma? Firma)
{
    /// <summary>
    /// Prioridad: Impresión Diagnóstica del ECC (más completa, incluye DSM-5);
    /// si el ECC no la trae, la descripción CIE-10 de la orden de servicio.
    /// </summary>
    public string Diagnostico =>
        Paciente.Diagnosticos.Count > 0
            ? string.Join("; ", Paciente.Diagnosticos.Select(d => d.TrimEnd().TrimEnd('.')))
            : Orden.Cie10Descripcion is { } descripcion ? TextoUtil.MayusculaInicial(descripcion) : "";

    public string OrigenDiagnostico =>
        Paciente.Diagnosticos.Count > 0 ? "historia clínica"
        : Orden.Cie10Descripcion is not null ? "orden de servicio (CIE-10)"
        : "no encontrado";
}