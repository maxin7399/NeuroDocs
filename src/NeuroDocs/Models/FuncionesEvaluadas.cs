namespace NeuroDocs.Models;

public sealed record FuncionesEvaluadas(
    IReadOnlyList<string> Conservados,
    IReadOnlyList<string> Alterados);