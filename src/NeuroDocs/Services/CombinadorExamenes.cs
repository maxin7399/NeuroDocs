using System.Globalization;
using System.IO;
using System.Text;
using NeuroDocs.Models;

namespace NeuroDocs.Services;

/// <summary>Un examen leído: su ruta, los datos del paciente y cuánto texto trae el archivo.</summary>
public sealed record ExamenLeido(string Ruta, DatosPaciente Paciente, int LongitudTexto);

public sealed record ExamenesCombinados(
    ExamenLeido Principal,
    DatosPaciente Paciente,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Cuando el paciente trae dos exámenes (p. ej. ECC y AD5): se usa como principal el de más contenido,
/// los campos que le faltan se completan con el otro y se avisa de las diferencias en los datos principales.
/// </summary>
public static class CombinadorExamenes
{
    /// <summary>Campos de identificación que deberían coincidir entre exámenes del mismo paciente.</summary>
    public static readonly IReadOnlyList<string> CamposComparados =
    [
        CamposPaciente.Nombre, CamposPaciente.Identificacion, CamposPaciente.LugarFechaNacimiento,
        CamposPaciente.Edad, CamposPaciente.Genero, CamposPaciente.Escolaridad, CamposPaciente.Lateralidad,
        CamposPaciente.Ocupacion, CamposPaciente.EstadoCivil, CamposPaciente.Entidad
    ];

    public static ExamenesCombinados Combinar(ExamenLeido examen, ExamenLeido? otro)
    {
        if (otro is null) return new ExamenesCombinados(examen, examen.Paciente, []);

        var (principal, secundario) = otro.LongitudTexto > examen.LongitudTexto ? (otro, examen) : (examen, otro);
        string nombrePrincipal = Path.GetFileName(principal.Ruta);
        string nombreSecundario = Path.GetFileName(secundario.Ruta);

        var campos = new Dictionary<string, string>(principal.Paciente.Campos);
        var completados = new List<string>();
        foreach (var (campo, valor) in secundario.Paciente.Campos)
        {
            if (!campos.TryGetValue(campo, out var actual) || string.IsNullOrWhiteSpace(actual))
            {
                campos[campo] = valor;
                completados.Add(campo);
            }
        }

        var diagnosticos = principal.Paciente.Diagnosticos.Count > 0
            ? principal.Paciente.Diagnosticos
            : secundario.Paciente.Diagnosticos;
        if (principal.Paciente.Diagnosticos.Count == 0 && secundario.Paciente.Diagnosticos.Count > 0)
            completados.Add("Impresión Diagnóstica");

        var avisos = new List<string>
        {
            $"Se usaron los datos del examen «{nombrePrincipal}» (tiene más contenido que «{nombreSecundario}»)."
        };
        if (completados.Count > 0)
            avisos.Add($"Completado con «{nombreSecundario}»: {string.Join(", ", completados)}.");

        foreach (var campo in CamposComparados)
        {
            if (principal.Paciente.Campos.TryGetValue(campo, out var a) &&
                secundario.Paciente.Campos.TryGetValue(campo, out var b) &&
                Normalizar(a) != Normalizar(b))
            {
                avisos.Add($"Diferencia en {campo}: «{a.Trim()}» (usado) / «{b.Trim()}».");
            }
        }

        return new ExamenesCombinados(principal, new DatosPaciente(campos, diagnosticos), avisos);
    }

    /// <summary>Ignora mayúsculas, tildes, espacios y puntuación: "C.C. 123." = "cc 123".</summary>
    private static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (char c in texto.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
