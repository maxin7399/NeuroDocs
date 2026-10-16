namespace NeuroDocs.Models;

public enum TipoInforme { EvolucionMensual, Final }

/// <summary>Lo que redacta la IA.</summary>
public sealed record TextoInforme(string CondicionActual, string PlanActual);

/// <summary>Todo lo necesario para generar el informe de evolución.</summary>
public sealed record DatosInforme(
    DatosPaciente Paciente,
    DatosOrden Orden,
    DateOnly EmisionPlan,
    DateOnly FechaElaboracion,
    string CondicionActual,
    string PlanActual);