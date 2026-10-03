using System.Text.RegularExpressions;

namespace NeuroDocs.Services;

public static class ListaTexto
{
    // Viñetas que llegan al pegar desde Word: •, ·, ▪, ◦, -, *, los símbolos privados del font Symbol
    // (\uF0B7, \uF0A7), "o" seguida de tabulación (viñeta de segundo nivel) y numeraciones tipo "1." o "1)".
    private static readonly Regex PrefijoViñeta = new(
        @"^\s*(?:[•·▪◦\-\*\uF0B7\uF0A7]|o\t|\d+[.)])\s*",
        RegexOptions.Compiled);

    /// <summary>Convierte texto multilínea en ítems: una línea = un ítem, sin viñetas ni líneas vacías.</summary>
    public static IReadOnlyList<string> ParsearItems(string texto) =>
        texto.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
             .Select(linea => PrefijoViñeta.Replace(linea, "").Trim())
             .Where(linea => linea.Length > 0)
             .ToList();
}