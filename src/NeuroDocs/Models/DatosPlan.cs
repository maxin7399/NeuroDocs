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
    public string Diagnostico => ResolverDiagnostico.Texto(Paciente, Orden);
    public string OrigenDiagnostico => ResolverDiagnostico.Origen(Paciente, Orden);
}