namespace NeuroDocs.Models;

public sealed record DatosPlan(
    DatosPaciente Paciente,
    DatosOrden Orden,
    DateOnly EmisionPlan,
    FuncionesEvaluadas Funciones,
    Firma? Firma);