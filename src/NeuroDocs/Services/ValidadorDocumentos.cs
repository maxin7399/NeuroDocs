using System.Text.RegularExpressions;
using NeuroDocs.Models;

namespace NeuroDocs.Services;

public static class ValidadorDocumentos
{
    public static IReadOnlyList<string> Validar(DatosPaciente paciente, DatosOrden orden)
    {
        var advertencias = new List<string>();

        if (paciente.Campos.TryGetValue(CamposPaciente.Identificacion, out var identificacion)
            && orden.NumeroDocumento is { } documentoOrden)
        {
            // "C.C. 8601373, Repelón – Atlántico." → "8601373" (tolera puntos: 8.601.373)
            string documentoHistoria = Regex.Replace(
                Regex.Match(identificacion, @"\d[\d.]{4,}").Value, @"\D", "");

            if (documentoHistoria.Length > 0 && documentoHistoria != documentoOrden)
            {
                advertencias.Add(
                    $"El número de documento no coincide: historia clínica {documentoHistoria}, orden {documentoOrden}.");
            }
        }
        if (paciente.Diagnosticos.Count == 0 && orden.Cie10Descripcion is null)
        {
            advertencias.Add("No se encontró diagnóstico ni en la historia clínica ni en la orden de servicio.");
        }
        return advertencias;
    }
}