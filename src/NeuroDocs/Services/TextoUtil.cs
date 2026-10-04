using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NeuroDocs.Services;

internal static class TextoUtil
{
    private static readonly CultureInfo Espanol = new("es-CO");
    private static readonly Regex Numeracion = new(@"^\s*\d+(\.\d+)*\.?\s+", RegexOptions.Compiled);

    /// <summary>"9. IMPRESIÓN DIAGNÓSTICA" → "IMPRESIÓN DIAGNÓSTICA"</summary>
    public static string QuitarNumeracion(string titulo) => Numeracion.Replace(titulo, "");

    /// <summary>"TRASTORNO DE LA CONDUCTA SOCIABLE" → "Trastorno de la conducta sociable"</summary>
    public static string MayusculaInicial(string texto)
    {
        string t = texto.Trim().ToLower(Espanol);
        return t.Length == 0 ? t : char.ToUpper(t[0], Espanol) + t[1..];
    }
    /// <summary>Minúsculas, sin tildes y sin espacios repetidos, para comparar etiquetas con tolerancia.</summary>
    public static string Normalizar(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return Regex.Replace(sb.ToString().ToLowerInvariant(), @"\s+", " ").Trim();
    }
    /// <summary>True si el texto empieza con la etiqueta seguida de ':' ("Dirección" no coincide con "Dirección Completa:").</summary>
    public static bool EmpiezaConEtiqueta(string texto, string etiqueta)
    {
        string t = Normalizar(texto);
        string e = Normalizar(etiqueta);
        return t.StartsWith(e) && t[e.Length..].TrimStart().StartsWith(':');
    }
}