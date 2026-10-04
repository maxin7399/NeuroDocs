using System.Text.RegularExpressions;
using NeuroDocs.Models;
using static NeuroDocs.Services.TextoUtil;

namespace NeuroDocs.Services;

public sealed class HistoriaClinicaParser
{
    // Campo de la plantilla → etiquetas con las que puede aparecer en la historia clínica.


    public DatosPaciente Parse(IReadOnlyList<PdfLine> lines)
    {
        var campos = ExtraerCampos(lines);
        var diagnosticos = ExtraerSeccion(lines, "Impresión Diagnóstica");
        return new DatosPaciente(campos, diagnosticos);
    }

    private static Dictionary<string, string> ExtraerCampos(IReadOnlyList<PdfLine> lines)
    {
        var campos = new Dictionary<string, string>();
        string? campoActual = null;

        foreach (var line in lines)
        {
            if (!line.StartsBold)
            {
                // Línea sin etiqueta: continuación del valor anterior (p. ej. una dirección larga).
                if (campoActual is not null)
                {
                    campos[campoActual] += " " + line.Text.Trim();
                }
                continue;
            }

            campoActual = null;

            if (TryMatchCampo(line.Text, out string campo, out string valor) && campos.TryAdd(campo, valor))
            {
                campoActual = campo;
            }
        }

        return campos;
    }

    private static bool TryMatchCampo(string text, out string campo, out string valor)
    {
        string normalizado = Normalizar(text);

        foreach (var (nombreCampo, etiquetas) in CamposPaciente.Etiquetas)
        {
            foreach (var etiqueta in etiquetas)
            {
                string etiquetaNorm = Normalizar(etiqueta);
                if (!normalizado.StartsWith(etiquetaNorm)) continue;

                // Exige ':' justo después de la etiqueta, para que "Dirección" no capture "Dirección Completa:".
                if (!normalizado[etiquetaNorm.Length..].TrimStart().StartsWith(':')) continue;

                campo = nombreCampo;
                valor = text[(text.IndexOf(':') + 1)..].Trim();
                return true;
            }
        }

        campo = valor = string.Empty;
        return false;
    }

    /// <summary>Devuelve los ítems bajo un título en negrita, hasta el siguiente título en negrita.</summary>
    private static List<string> ExtraerSeccion(IReadOnlyList<PdfLine> lines, string titulo)
    {
        var items = new List<string>();
        string tituloNorm = Normalizar(titulo);

        int inicio = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsAllBold && Normalizar(QuitarNumeracion(lines[i].Text)) == tituloNorm)
            {
                inicio = i + 1;
                break;
            }
        }
        if (inicio < 0) return items;

        for (int i = inicio; i < lines.Count && !lines[i].IsAllBold; i++)
        {
            string text = lines[i].Text.Trim();
            if (text.StartsWith('•'))
            {
                items.Add(text.TrimStart('•').Trim());
            }
            else if (items.Count > 0)
            {
                items[^1] += " " + text; // viñeta que continúa en la línea siguiente
            }
            else
            {
                items.Add(text);
            }
        }

        return items;
    }
}