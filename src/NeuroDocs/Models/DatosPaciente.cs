namespace NeuroDocs.Models;

public sealed record DatosPaciente(
    IReadOnlyDictionary<string, string> Campos,
    IReadOnlyList<string> Diagnosticos);